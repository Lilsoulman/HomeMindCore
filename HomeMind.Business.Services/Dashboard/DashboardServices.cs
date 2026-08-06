using HomeMind.Business.IServices.Dashboard;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.Dashboard;
using HomeMind.Common.Model.ViewModel.Data.SmartHome;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;

namespace HomeMind.Business.Services.Dashboard;

/// <summary>仪表板聚合服务；各模块独立读取并降级，不暴露连接器或厂商细节。</summary>
public sealed class DashboardServices : IDashboardServices
{
    private readonly HomeMindDbContext _db;

    public DashboardServices(HomeMindDbContext db) => _db = db;

    public async Task<ServiceResult> GetAsync(long userId, long tenantId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var home = await ReadModuleAsync(() => ReadHomeAsync(tenantId, cancellationToken), EmptyHome(), cancellationToken);
        var pendingConfirmations = await ReadModuleAsync(() => ReadPendingConfirmationsAsync(tenantId, now, cancellationToken), Array.Empty<DashboardConfirmationView>(), cancellationToken);
        var stewardActivities = await ReadModuleAsync(() => ReadStewardActivitiesAsync(tenantId, cancellationToken), Array.Empty<DashboardStewardActivityView>(), cancellationToken);
        var scenes = await ReadModuleAsync(() => ReadScenesAsync(tenantId, now, cancellationToken), Array.Empty<SceneView>(), cancellationToken);
        var todos = await ReadModuleAsync(() => ReadTodosAsync(userId, tenantId, now, cancellationToken), Array.Empty<DashboardTodoView>(), cancellationToken);
        var calendar = await ReadModuleAsync(() => ReadCalendarAsync(userId, tenantId, now, cancellationToken), Array.Empty<DashboardCalendarEventView>(), cancellationToken);
        var suggestion = await ReadModuleAsync(() => ReadSuggestionAsync(userId, tenantId, cancellationToken), null, cancellationToken);
        var partialFailure = new[] { home.Status, pendingConfirmations.Status, stewardActivities.Status, scenes.Status, todos.Status, calendar.Status, suggestion.Status }.Any(x => x != "available");

        return new ServiceResult(200, partialFailure ? "看板已返回，部分数据暂不可用。" : "查询成功。",
            new DashboardView(now, partialFailure, home, pendingConfirmations, stewardActivities, scenes, todos, calendar, suggestion));
    }

    /// <summary>读取待确认事项模块：仅返回未过期且仍待处理的确认项，按到期时间升序取前 6 条。</summary>
    private async Task<DashboardModule<IReadOnlyList<DashboardConfirmationView>>> ReadPendingConfirmationsAsync(long tenantId, DateTime now, CancellationToken cancellationToken)
    {
        var items = await _db.ConfirmationItems.Where(x => x.HomeId == tenantId && x.Status == "pending" && (x.ExpiresAt == null || x.ExpiresAt > now))
            .OrderBy(x => x.ExpiresAt == null).ThenBy(x => x.ExpiresAt).ThenBy(x => x.Id).Take(6)
            .Select(x => new DashboardConfirmationView(x.Id, x.RiskLevel, x.Title, x.ImpactSummary, x.Status, x.ExpiresAt, x.UpdatedAt)).ToListAsync(cancellationToken);
        return Available<IReadOnlyList<DashboardConfirmationView>>(items, items.Select(x => x.UpdatedAt).DefaultIfEmpty().Max());
    }

    /// <summary>读取管家动态模块：按创建时间倒序取最近 6 条活动。</summary>
    private async Task<DashboardModule<IReadOnlyList<DashboardStewardActivityView>>> ReadStewardActivitiesAsync(long tenantId, CancellationToken cancellationToken)
    {
        var items = await _db.StewardActivities.Where(x => x.HomeId == tenantId)
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Take(6)
            .Select(x => new DashboardStewardActivityView(x.Id, x.Category, x.Title, x.RiskLevel, x.Status, x.ResultSummary, x.CreatedAt)).ToListAsync(cancellationToken);
        return Available<IReadOnlyList<DashboardStewardActivityView>>(items, items.Select(x => x.CreatedAt).DefaultIfEmpty().Max());
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
