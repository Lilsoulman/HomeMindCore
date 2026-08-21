namespace HomeMind.Common.Model.ViewModel.Data.Family;

/// <summary>家庭聚合日历事件视图，仅包含协同排程所需字段。</summary>
public sealed record FamilyScheduleEventView(long Id, long UserId, string MemberName, string Title, DateTime StartAt, DateTime? EndAt, bool AllDay);

/// <summary>两个家庭成员日程相交的冲突视图。</summary>
public sealed record FamilyScheduleConflictView(FamilyScheduleEventView First, FamilyScheduleEventView Second, DateTime OverlapStartAt, DateTime OverlapEndAt);

/// <summary>全体家庭成员都空闲的候选时段。</summary>
public sealed record FamilyScheduleAvailabilityView(DateTime StartAt, DateTime EndAt);

/// <summary>新增家庭证件到期提醒的请求体。</summary>
/// <param name="DocumentType">证件类型：identity_card、passport、driver_license、residence_permit 或 other。</param>
/// <param name="DisplayName">家庭内展示名称，不得填写证件号码、照片链接或证件原文。</param>
/// <param name="HolderUserId">持有人家庭账号主键，可空。</param>
/// <param name="ExpiresOn">证件到期日期。</param>
public sealed record FamilyDocumentDeadlineCreateRequest(string? DocumentType, string? DisplayName, long? HolderUserId, DateTime? ExpiresOn);

/// <summary>家庭证件到期提醒视图。</summary>
public sealed record FamilyDocumentDeadlineView(long Id, string DocumentType, string DisplayName, long? HolderUserId, string? HolderName, DateTime ExpiresOn, bool IsActive);

/// <summary>家庭日程或到期事项提醒视图。</summary>
public sealed record FamilyScheduleReminderView(string Type, long SourceId, string Title, DateTime DueDate, int DaysRemaining, long ConfirmationId);

/// <summary>睡前明日预览视图。</summary>
public sealed record FamilyTomorrowPreviewView(DateTime Date, IReadOnlyList<FamilyScheduleEventView> Events, IReadOnlyList<FamilyScheduleConflictView> Conflicts, IReadOnlyList<FamilyScheduleReminderView> Reminders);
