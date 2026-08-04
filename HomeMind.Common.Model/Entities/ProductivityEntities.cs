using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HomeMind.Common.Model.Entities;

/// <summary>待办事项表。</summary>
[Table("todos")]
public sealed class Todo
{
    [Key, Column("id")] public long Id { get; set; }
    [Column("tenant_id")] public long TenantId { get; set; }
    [Column("user_id")] public long UserId { get; set; }
    [Column("list_id")] public long? ListId { get; set; }
    [Column("parent_id")] public long? ParentId { get; set; }
    [Column("title")] public string Title { get; set; } = null!;
    [Column("description")] public string? Description { get; set; }
    [Column("type")] public string? Type { get; set; }
    [Column("priority")] public string? Priority { get; set; }
    [Column("color")] public string? Color { get; set; }
    [Column("status")] public string Status { get; set; } = "pending";
    [Column("due_at")] public DateTime? DueAt { get; set; }
    [Column("remind_at")] public DateTime? RemindAt { get; set; }
    [Column("completed_at")] public DateTime? CompletedAt { get; set; }
    [Column("pinned")] public bool Pinned { get; set; }
    [Column("sort_order")] public int SortOrder { get; set; }
    [Column("repeat_rule")] public string? RepeatRule { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
    [Column("deleted_at")] public DateTime? DeletedAt { get; set; }
}

[Table("subtasks")]
public sealed class Subtask
{
    [Key, Column("id")] public long Id { get; set; }
    [Column("tenant_id")] public long TenantId { get; set; }
    [Column("todo_id")] public long TodoId { get; set; }
    [Column("text")] public string Text { get; set; } = null!;
    [Column("done")] public bool Done { get; set; }
    [Column("seq")] public int Seq { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
    [Column("deleted_at")] public DateTime? DeletedAt { get; set; }
}

[Table("calendar_events")]
public sealed class CalendarEvent
{
    [Key, Column("id")] public long Id { get; set; }
    [Column("tenant_id")] public long TenantId { get; set; }
    [Column("user_id")] public long UserId { get; set; }
    [Column("title")] public string Title { get; set; } = null!;
    [Column("description")] public string? Description { get; set; }
    [Column("location")] public string? Location { get; set; }
    [Column("start_at")] public DateTime StartAt { get; set; }
    [Column("end_at")] public DateTime? EndAt { get; set; }
    [Column("timezone")] public string? Timezone { get; set; }
    [Column("all_day")] public bool AllDay { get; set; }
    [Column("color")] public string? Color { get; set; }
    [Column("opacity")] public decimal? Opacity { get; set; }
    [Column("repeat_rule")] public string? RepeatRule { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
    [Column("deleted_at")] public DateTime? DeletedAt { get; set; }
}

[Table("calendar_subscriptions")]
public sealed class CalendarSubscription
{
    [Key, Column("id")] public long Id { get; set; }
    [Column("tenant_id")] public long TenantId { get; set; }
    [Column("user_id")] public long UserId { get; set; }
    [Column("name")] public string Name { get; set; } = null!;
    [Column("source_url_encrypted")] public byte[] SourceUrlEncrypted { get; set; } = null!;
    [Column("source_url_hash")] public byte[] SourceUrlHash { get; set; } = null!;
    [Column("enabled")] public bool Enabled { get; set; }
    [Column("refresh_interval_min")] public int RefreshIntervalMin { get; set; }
    [Column("last_fetch_at")] public DateTime? LastFetchAt { get; set; }
    [Column("last_error")] public string? LastError { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
    [Column("deleted_at")] public DateTime? DeletedAt { get; set; }
}
