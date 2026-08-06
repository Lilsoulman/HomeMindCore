using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HomeMind.Common.Model.Entities;

/// <summary>待办事项表。</summary>
[Table("todos")]
public sealed class Todo
{
    /// <summary>待办主键。</summary>
    [Key, Column("id")] public long Id { get; set; }
    /// <summary>所属租户标识。</summary>
    [Column("tenant_id")] public long TenantId { get; set; }
    /// <summary>创建者用户标识。</summary>
    [Column("user_id")] public long UserId { get; set; }
    /// <summary>所属列表主键，可为空表示未分组。</summary>
    [Column("list_id")] public long? ListId { get; set; }
    /// <summary>父待办主键，用于子任务。</summary>
    [Column("parent_id")] public long? ParentId { get; set; }
    /// <summary>待办标题。</summary>
    [Column("title")] public string Title { get; set; } = null!;
    /// <summary>待办详细描述，可为空。</summary>
    [Column("description")] public string? Description { get; set; }
    /// <summary>待办类型，如"task""habit"等。</summary>
    [Column("type")] public string? Type { get; set; }
    /// <summary>优先级，参见 <see cref="TodoPriority"/>。</summary>
    [Column("priority")] public string? Priority { get; set; }
    /// <summary>前端展示色，HEX 字符串。</summary>
    [Column("color")] public string? Color { get; set; }
    /// <summary>待办状态，参见 <see cref="TodoStatus"/>。</summary>
    [Column("status")] public string Status { get; set; } = "pending";
    /// <summary>截止时间（UTC），可为空表示无截止。</summary>
    [Column("due_at")] public DateTime? DueAt { get; set; }
    /// <summary>提醒时间（UTC），可为空表示不提醒。</summary>
    [Column("remind_at")] public DateTime? RemindAt { get; set; }
    /// <summary>完成时间（UTC）。</summary>
    [Column("completed_at")] public DateTime? CompletedAt { get; set; }
    /// <summary>是否置顶。</summary>
    [Column("pinned")] public bool Pinned { get; set; }
    /// <summary>列表内排序值，越小越靠前。</summary>
    [Column("sort_order")] public int SortOrder { get; set; }
    /// <summary>重复规则，使用 RFC 5545 RRULE 子集。</summary>
    [Column("repeat_rule")] public string? RepeatRule { get; set; }
    /// <summary>创建时间（UTC）。</summary>
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    /// <summary>更新时间（UTC）。</summary>
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
    /// <summary>软删除时间戳。</summary>
    [Column("deleted_at")] public DateTime? DeletedAt { get; set; }
}

/// <summary>待办子任务表。</summary>
[Table("subtasks")]
public sealed class Subtask
{
    /// <summary>子任务主键。</summary>
    [Key, Column("id")] public long Id { get; set; }
    /// <summary>所属租户标识。</summary>
    [Column("tenant_id")] public long TenantId { get; set; }
    /// <summary>所属待办主键。</summary>
    [Column("todo_id")] public long TodoId { get; set; }
    /// <summary>子任务文本。</summary>
    [Column("text")] public string Text { get; set; } = null!;
    /// <summary>子任务完成状态。</summary>
    [Column("done")] public bool Done { get; set; }
    /// <summary>展示顺序。</summary>
    [Column("seq")] public int Seq { get; set; }
    /// <summary>创建时间（UTC）。</summary>
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    /// <summary>更新时间（UTC）。</summary>
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
    /// <summary>软删除时间戳。</summary>
    [Column("deleted_at")] public DateTime? DeletedAt { get; set; }
}

/// <summary>日历事件表。</summary>
[Table("calendar_events")]
public sealed class CalendarEvent
{
    /// <summary>日历事件主键。</summary>
    [Key, Column("id")] public long Id { get; set; }
    /// <summary>所属租户标识。</summary>
    [Column("tenant_id")] public long TenantId { get; set; }
    /// <summary>创建者用户标识。</summary>
    [Column("user_id")] public long UserId { get; set; }
    /// <summary>事件标题。</summary>
    [Column("title")] public string Title { get; set; } = null!;
    /// <summary>事件详细描述。</summary>
    [Column("description")] public string? Description { get; set; }
    /// <summary>事件地点。</summary>
    [Column("location")] public string? Location { get; set; }
    /// <summary>开始时间（UTC）。</summary>
    [Column("start_at")] public DateTime StartAt { get; set; }
    /// <summary>结束时间（UTC），可为空表示无结束。</summary>
    [Column("end_at")] public DateTime? EndAt { get; set; }
    /// <summary>事件显示时区，使用 IANA 时区标识。</summary>
    [Column("timezone")] public string? Timezone { get; set; }
    /// <summary>是否全天事件。</summary>
    [Column("all_day")] public bool AllDay { get; set; }
    /// <summary>前端展示色，HEX 字符串。</summary>
    [Column("color")] public string? Color { get; set; }
    /// <summary>事件不透明度（0-1）。</summary>
    [Column("opacity")] public decimal? Opacity { get; set; }
    /// <summary>重复规则，使用 RFC 5545 RRULE 子集。</summary>
    [Column("repeat_rule")] public string? RepeatRule { get; set; }
    /// <summary>创建时间（UTC）。</summary>
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    /// <summary>更新时间（UTC）。</summary>
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
    /// <summary>软删除时间戳。</summary>
    [Column("deleted_at")] public DateTime? DeletedAt { get; set; }
}

/// <summary>日历订阅表，保存 iCal 远端源与刷新策略。</summary>
[Table("calendar_subscriptions")]
public sealed class CalendarSubscription
{
    /// <summary>订阅主键。</summary>
    [Key, Column("id")] public long Id { get; set; }
    /// <summary>所属租户标识。</summary>
    [Column("tenant_id")] public long TenantId { get; set; }
    /// <summary>订阅者用户标识。</summary>
    [Column("user_id")] public long UserId { get; set; }
    /// <summary>订阅名称。</summary>
    [Column("name")] public string Name { get; set; } = null!;
    /// <summary>源 URL 加密后的密文，API 不返回明文。</summary>
    [Column("source_url_encrypted")] public byte[] SourceUrlEncrypted { get; set; } = null!;
    /// <summary>源 URL 的 SHA-256 摘要，用于去重与审计检索。</summary>
    [Column("source_url_hash")] public byte[] SourceUrlHash { get; set; } = null!;
    /// <summary>是否启用。</summary>
    [Column("enabled")] public bool Enabled { get; set; }
    /// <summary>刷新间隔（分钟）。</summary>
    [Column("refresh_interval_min")] public int RefreshIntervalMin { get; set; }
    /// <summary>最近一次抓取时间（UTC）。</summary>
    [Column("last_fetch_at")] public DateTime? LastFetchAt { get; set; }
    /// <summary>最近一次抓取错误信息。</summary>
    [Column("last_error")] public string? LastError { get; set; }
    /// <summary>创建时间（UTC）。</summary>
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    /// <summary>更新时间（UTC）。</summary>
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
    /// <summary>软删除时间戳。</summary>
    [Column("deleted_at")] public DateTime? DeletedAt { get; set; }
}
