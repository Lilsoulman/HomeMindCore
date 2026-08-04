using HomeMind.Business.IServices.Dashboard;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.Dashboard;
using HomeMind.Common.Model.ViewModel.Data.SmartHome;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;

namespace HomeMind.Business.Services.Dashboard;

/// <summary>Reads independent dashboard modules without exposing connector or provider details.</summary>
public sealed class DashboardServices : IDashboardServices
{
    private readonly HomeMindDbContext _db;

    public DashboardServices(HomeMindDbContext db) => _db = db;

    public async Task<ServiceResult> GetAsync(long userId, long tenantId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var home = await ReadModuleAsync(() => ReadHomeAsync(tenantId, cancellationToken), EmptyHome(), cancellationToken);
        var scenes = await ReadModuleAsync(() => ReadScenesAsync(tenantId, now, cancellationToken), Array.Empty<SceneView>(), cancellationToken);
        var todos = await ReadModuleAsync(() => ReadTodosAsync(userId, tenantId, now, cancellationToken), Array.Empty<DashboardTodoView>(), cancellationToken);
        var calendar = await ReadModuleAsync(() => ReadCalendarAsync(userId, tenantId, now, cancellationToken), Array.Empty<DashboardCalendarEventView>(), cancellationToken);
        var suggestion = await ReadModuleAsync(() => ReadSuggestionAsync(userId, tenantId, cancellationToken), null, cancellationToken);
        var partialFailure = new[] { home.Status, scenes.Status, todos.Status, calendar.Status, suggestion.Status }.Any(x => x != "available");

        return new ServiceResult(200, partialFailure ? "看板已返回，部分数据暂不可用。" : "查询成功。", new DashboardView(now, partialFailure, home, scenes, todos, calendar, suggestion));
    }

    private async Task<DashboardModule<DashboardHomeView>> ReadHomeAsync(long tenantId, CancellationToken cancellationToken)
    {
        var spaces = await _db.SmartHomeSpaces.Where(x => x.TenantId == tenantId && x.DeletedAt == null)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToListAsync(cancellationToken);
        var devices = await _db.SmartHomeDevices.Where(x => x.TenantId == tenantId && x.DeletedAt == null).ToListAsync(cancellationToken);
        var deviceIds = devices.Select(x => x.Id).ToArray();
        var stateUpdates = deviceIds.Length == 0
            ? new Dictionary<long, DateTime>()
            : await _db.DeviceStates.Where(x => deviceIds.Contains(x.DeviceId)).GroupBy(x => x.DeviceId)
                .Select(x => new { DeviceId = x.Key, SampledAt = x.Max(s => s.SampledAt) })
                .ToDictionaryAsync(x => x.DeviceId, x => x.SampledAt, cancellationToken);
        var summaries = spaces.Select(space =>
        {
            var spaceDevices = devices.Where(device => device.SpaceId == space.Id).ToArray();
            var latest = spaceDevices.Select(device => stateUpdates.GetValueOrDefault(device.Id)).DefaultIfEmpty().Max();
            return new DashboardSpaceSummaryView(space.Id, space.Name, space.SpaceType, space.Summary, spaceDevices.Length,
                spaceDevices.Count(device => device.OnlineStatus == "online"), spaceDevices.Count(device => device.OnlineStatus == "offline"),
                latest == default ? null : latest, space.UpdatedAt);
        }).ToArray();
        var latestUpdate = summaries.Select(x => x.StateUpdatedAt ?? x.UpdatedAt).DefaultIfEmpty().Max();
        return Available(new DashboardHomeView(summaries.Length, devices.Count, devices.Count(x => x.OnlineStatus == "online"), devices.Count(x => x.OnlineStatus == "offline"), summaries), latestUpdate == default ? null : latestUpdate);
    }

    private async Task<DashboardModule<IReadOnlyList<SceneView>>> ReadScenesAsync(long tenantId, DateTime now, CancellationToken cancellationToken)
    {
        var configured = await _db.Scenes.Where(x => x.TenantId == tenantId && x.DeletedAt == null && x.Status == "active")
            .OrderBy(x => x.Name).Select(x => new SceneView(x.Id, x.SceneKey, x.Name, x.Summary, x.Status, x.UpdatedAt)).ToListAsync(cancellationToken);
        var standard = SmartHomeSceneDefinitions.CreateViews(now);
        var scenes = standard.Concat(configured.Where(x => standard.All(definition => definition.Key != x.Key))).ToArray();
        return Available<IReadOnlyList<SceneView>>(scenes, scenes.Select(x => x.UpdatedAt).DefaultIfEmpty().Max());
    }

    private async Task<DashboardModule<IReadOnlyList<DashboardTodoView>>> ReadTodosAsync(long userId, long tenantId, DateTime now, CancellationToken cancellationToken)
    {
        var tomorrow = now.Date.AddDays(1);
        var todos = await _db.Todos.Where(x => x.UserId == userId && x.TenantId == tenantId && x.DeletedAt == null && x.Status != "completed" && (x.DueAt == null || x.DueAt < tomorrow))
            .OrderByDescending(x => x.Pinned).ThenBy(x => x.DueAt == null).ThenBy(x => x.DueAt).ThenBy(x => x.Id).Take(6)
            .Select(x => new DashboardTodoView(x.Id, x.Title, x.Status, x.Priority, x.DueAt, x.Pinned, x.UpdatedAt)).ToListAsync(cancellationToken);
        return Available<IReadOnlyList<DashboardTodoView>>(todos, todos.Select(x => x.UpdatedAt).DefaultIfEmpty().Max());
    }

    private async Task<DashboardModule<IReadOnlyList<DashboardCalendarEventView>>> ReadCalendarAsync(long userId, long tenantId, DateTime now, CancellationToken cancellationToken)
    {
        var tomorrow = now.Date.AddDays(1);
        var events = await _db.CalendarEvents.Where(x => x.UserId == userId && x.TenantId == tenantId && x.DeletedAt == null && x.StartAt >= now.Date && x.StartAt < tomorrow)
            .OrderBy(x => x.StartAt).ThenBy(x => x.Id).Take(6)
            .Select(x => new DashboardCalendarEventView(x.Id, x.Title, x.StartAt, x.EndAt, x.AllDay, x.UpdatedAt)).ToListAsync(cancellationToken);
        return Available<IReadOnlyList<DashboardCalendarEventView>>(events, events.Select(x => x.UpdatedAt).DefaultIfEmpty().Max());
    }

    private async Task<DashboardModule<DashboardSuggestionView?>> ReadSuggestionAsync(long userId, long tenantId, CancellationToken cancellationToken)
    {
        var suggestion = await _db.AgentRuns.Where(x => x.UserId == userId && x.TenantId == tenantId && x.SourceType == "expert" && x.Status == "completed")
            .OrderByDescending(x => x.CreatedAt).Select(x => new DashboardSuggestionView(x.Id, x.ResultSummary, x.Status, x.CreatedAt)).FirstOrDefaultAsync(cancellationToken);
        return Available<DashboardSuggestionView?>(suggestion, suggestion?.CreatedAt);
    }

    private static DashboardModule<T> Available<T>(T data, DateTime? updatedAt) => new("available", data, updatedAt, null);
    private static DashboardHomeView EmptyHome() => new(0, 0, 0, 0, Array.Empty<DashboardSpaceSummaryView>());

    private static async Task<DashboardModule<T>> ReadModuleAsync<T>(Func<Task<DashboardModule<T>>> read, T fallback, CancellationToken cancellationToken)
    {
        try { return await read(); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { return new DashboardModule<T>("unavailable", fallback, null, "暂时无法读取该模块。请稍后刷新。"); }
    }
}
