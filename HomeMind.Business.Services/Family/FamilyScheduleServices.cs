using HomeMind.Business.IServices.Family;
using HomeMind.Common.Model.Entities.Family;
using HomeMind.Common.Model.Entities.Steward;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.Family;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;

namespace HomeMind.Business.Services.Family;

/// <summary>家庭日程协同管家服务，以成员日历和到期事实源生成只读协同视图。</summary>
public sealed class FamilyScheduleServices : IFamilyScheduleServices
{
    private readonly HomeMindDbContext _db;
    private readonly IFamilyAuditLogger _audit;

    /// <summary>构造家庭日程协同管家服务。</summary>
    public FamilyScheduleServices(HomeMindDbContext db, IFamilyAuditLogger audit)
    {
        _db = db;
        _audit = audit;
    }

    /// <inheritdoc />
    public async Task<ServiceResult> ListEventsAsync(long homeId, DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
    {
        if (!TryNormalizeWindow(from, to, out var start, out var end)) return new ServiceResult(422, "日历查询窗口无效，最长不超过 31 天。");
        return new ServiceResult(200, "查询成功。", await EventsAsync(homeId, start, end, cancellationToken));
    }

    /// <inheritdoc />
    public async Task<ServiceResult> ListConflictsAsync(long homeId, DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
    {
        if (!TryNormalizeWindow(from, to, out var start, out var end)) return new ServiceResult(422, "日历查询窗口无效，最长不超过 31 天。");
        return new ServiceResult(200, "查询成功。", FindConflicts(await EventsAsync(homeId, start, end, cancellationToken)));
    }

    /// <inheritdoc />
    public async Task<ServiceResult> ListAvailabilityAsync(long homeId, DateTime? from, DateTime? to, int durationMinutes, CancellationToken cancellationToken = default)
    {
        if (!TryNormalizeWindow(from, to, out var start, out var end) || durationMinutes is < 15 or > 480) return new ServiceResult(422, "日历查询窗口或空档时长无效。");
        var events = await EventsAsync(homeId, start, end, cancellationToken);
        var occupied = events.Select(item => (Start: Max(item.StartAt, start), End: Min(EventEnd(item), end))).OrderBy(item => item.Start).ToList();
        var cursor = start;
        var slots = new List<FamilyScheduleAvailabilityView>();
        foreach (var item in occupied)
        {
            if (item.Start > cursor && (item.Start - cursor).TotalMinutes >= durationMinutes) slots.Add(new FamilyScheduleAvailabilityView(cursor, item.Start));
            if (item.End > cursor) cursor = item.End;
        }
        if (end > cursor && (end - cursor).TotalMinutes >= durationMinutes) slots.Add(new FamilyScheduleAvailabilityView(cursor, end));
        return new ServiceResult(200, "查询成功。", slots);
    }

    /// <inheritdoc />
    public async Task<ServiceResult> CreateDocumentDeadlineAsync(long homeId, long actorUserId, FamilyDocumentDeadlineCreateRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null || !FamilyDocumentTypes.All.Contains(request.DocumentType ?? string.Empty) || string.IsNullOrWhiteSpace(request.DisplayName) || request.DisplayName.Trim().Length > 128 || request.ExpiresOn is null) return new ServiceResult(422, "证件类型、展示名称和到期日期不符合约束。");
        if (request.HolderUserId is not null && !await _db.TenantMembers.AnyAsync(item => item.TenantId == homeId && item.UserId == request.HolderUserId && item.Status == "active", cancellationToken)) return new ServiceResult(422, "持有人必须是当前家庭的活跃成员。");
        var now = DateTime.UtcNow;
        var deadline = new FamilyDocumentDeadline { HomeId = homeId, HolderUserId = request.HolderUserId, DocumentType = request.DocumentType!, DisplayName = request.DisplayName.Trim(), ExpiresOn = request.ExpiresOn.Value.Date, CreatedByUserId = actorUserId, CreatedAt = now, UpdatedAt = now };
        _db.FamilyDocumentDeadlines.Add(deadline);
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync(homeId, actorUserId, FamilyAuditActions.ScheduleDocumentDeadlineCreate, FamilyAuditTargetTypes.ScheduleDocumentDeadline, deadline.Id, null, new { deadline.Id, deadline.DocumentType, deadline.DisplayName, deadline.HolderUserId, deadline.ExpiresOn }, "创建家庭证件到期提醒", null, cancellationToken);
        return new ServiceResult(201, "证件到期提醒已创建。", await DeadlineViewAsync(deadline, cancellationToken));
    }

    /// <inheritdoc />
    public async Task<ServiceResult> ListDocumentDeadlinesAsync(long homeId, CancellationToken cancellationToken = default)
    {
        var deadlines = await _db.FamilyDocumentDeadlines.Where(item => item.HomeId == homeId && item.IsActive).OrderBy(item => item.ExpiresOn).ToListAsync(cancellationToken);
        var views = new List<FamilyDocumentDeadlineView>();
        foreach (var deadline in deadlines) views.Add(await DeadlineViewAsync(deadline, cancellationToken));
        return new ServiceResult(200, "查询成功。", views);
    }

    /// <inheritdoc />
    public async Task<ServiceResult> ListRemindersAsync(long homeId, DateTime? asOf = null, CancellationToken cancellationToken = default)
    {
        var today = (asOf ?? DateTime.UtcNow).Date;
        var reminders = new List<FamilyScheduleReminderView>();
        var billings = await _db.BillingAccounts.Where(item => item.HomeId == homeId && item.IsActive && item.NextDueDate >= today && item.NextDueDate <= today.AddDays(3)).OrderBy(item => item.NextDueDate).ToListAsync(cancellationToken);
        foreach (var item in billings)
        {
            var daysRemaining = (int)(item.NextDueDate.Date - today).TotalDays;
            var title = $"缴费提醒：{item.Label} 将于 {item.NextDueDate:yyyy-MM-dd} 到期（提前{daysRemaining}天）";
            reminders.Add(new FamilyScheduleReminderView("billing", item.Id, title, item.NextDueDate.Date, daysRemaining, await EnsureConfirmationAsync(homeId, "billing", item.Id, title, item.NextDueDate.Date, cancellationToken)));
        }
        var documents = await _db.FamilyDocumentDeadlines.Where(item => item.HomeId == homeId && item.IsActive && item.ExpiresOn >= today && item.ExpiresOn <= today.AddDays(30)).OrderBy(item => item.ExpiresOn).ToListAsync(cancellationToken);
        foreach (var item in documents) reminders.Add(new FamilyScheduleReminderView("document", item.Id, $"证件提醒：{item.DisplayName} 将于 {item.ExpiresOn:yyyy-MM-dd} 到期", item.ExpiresOn.Date, (int)(item.ExpiresOn.Date - today).TotalDays, await EnsureConfirmationAsync(homeId, "document", item.Id, $"证件提醒：{item.DisplayName} 将于 {item.ExpiresOn:yyyy-MM-dd} 到期", item.ExpiresOn.Date, cancellationToken)));
        return new ServiceResult(200, "查询成功。", reminders.OrderBy(item => item.DueDate).ToList());
    }

    /// <inheritdoc />
    public async Task<ServiceResult> GetTomorrowPreviewAsync(long homeId, DateTime? asOf = null, CancellationToken cancellationToken = default)
    {
        var tomorrow = (asOf ?? DateTime.UtcNow).Date.AddDays(1);
        var events = await EventsAsync(homeId, tomorrow, tomorrow.AddDays(1), cancellationToken);
        var reminders = (IReadOnlyList<FamilyScheduleReminderView>)(await ListRemindersAsync(homeId, tomorrow, cancellationToken)).Data!;
        return new ServiceResult(200, "查询成功。", new FamilyTomorrowPreviewView(tomorrow, events, FindConflicts(events), reminders));
    }

    private async Task<List<FamilyScheduleEventView>> EventsAsync(long homeId, DateTime start, DateTime end, CancellationToken cancellationToken) =>
        await (from calendarEvent in _db.CalendarEvents
               join member in _db.TenantMembers on calendarEvent.UserId equals member.UserId
               join user in _db.Users on calendarEvent.UserId equals user.Id
               where calendarEvent.TenantId == homeId && member.TenantId == homeId && member.Status == "active" && calendarEvent.DeletedAt == null && calendarEvent.StartAt < end && (calendarEvent.EndAt == null || calendarEvent.EndAt > start)
               orderby calendarEvent.StartAt
               select new FamilyScheduleEventView(calendarEvent.Id, calendarEvent.UserId, user.DisplayName, calendarEvent.Title, calendarEvent.StartAt, calendarEvent.EndAt, calendarEvent.AllDay)).ToListAsync(cancellationToken);

    private static List<FamilyScheduleConflictView> FindConflicts(IReadOnlyList<FamilyScheduleEventView> events)
    {
        var conflicts = new List<FamilyScheduleConflictView>();
        for (var firstIndex = 0; firstIndex < events.Count; firstIndex++)
        for (var secondIndex = firstIndex + 1; secondIndex < events.Count; secondIndex++)
        {
            var first = events[firstIndex]; var second = events[secondIndex];
            var overlapStart = Max(first.StartAt, second.StartAt); var overlapEnd = Min(EventEnd(first), EventEnd(second));
            if (overlapEnd > overlapStart) conflicts.Add(new FamilyScheduleConflictView(first, second, overlapStart, overlapEnd));
        }
        return conflicts;
    }

    private async Task<FamilyDocumentDeadlineView> DeadlineViewAsync(FamilyDocumentDeadline deadline, CancellationToken cancellationToken)
    {
        var holderName = deadline.HolderUserId is null ? null : await _db.Users.Where(item => item.Id == deadline.HolderUserId).Select(item => item.DisplayName).SingleOrDefaultAsync(cancellationToken);
        return new FamilyDocumentDeadlineView(deadline.Id, deadline.DocumentType, deadline.DisplayName, deadline.HolderUserId, holderName, deadline.ExpiresOn, deadline.IsActive);
    }

    private async Task<long> EnsureConfirmationAsync(long homeId, string type, long sourceId, string title, DateTime dueDate, CancellationToken cancellationToken)
    {
        var existing = await _db.ConfirmationItems.SingleOrDefaultAsync(item => item.HomeId == homeId && item.Title == title && item.Status == ConfirmationItemStatus.Pending, cancellationToken);
        if (existing is not null) return existing.Id;
        var now = DateTime.UtcNow;
        var confirmation = new ConfirmationItem { HomeId = homeId, RiskLevel = ConfirmationRiskLevel.L1, Title = title, Description = $"家庭日程协同发现 {type} 到期事项。", ImpactSummary = "仅生成家庭内提醒，不会自动缴费、续期或访问第三方服务。", SuggestedAction = "确认后由用户自行安排处理。", Status = ConfirmationItemStatus.Pending, ExpiresAt = dueDate.AddDays(1), CreatedAt = now, UpdatedAt = now };
        _db.ConfirmationItems.Add(confirmation);
        await _db.SaveChangesAsync(cancellationToken);
        return confirmation.Id;
    }

    private static bool TryNormalizeWindow(DateTime? from, DateTime? to, out DateTime start, out DateTime end)
    {
        start = (from ?? DateTime.UtcNow.Date).ToUniversalTime(); end = (to ?? start.AddDays(1)).ToUniversalTime();
        return end > start && end - start <= TimeSpan.FromDays(31);
    }
    private static DateTime EventEnd(FamilyScheduleEventView item) => item.EndAt ?? (item.AllDay ? item.StartAt.Date.AddDays(1) : item.StartAt.AddMinutes(30));
    private static DateTime Max(DateTime first, DateTime second) => first >= second ? first : second;
    private static DateTime Min(DateTime first, DateTime second) => first <= second ? first : second;
}
