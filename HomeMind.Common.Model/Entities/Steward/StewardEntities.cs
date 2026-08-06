using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HomeMind.Common.Model.Entities.Steward;

/// <summary>管家活动记录，关联运行并向用户呈现的执行流。</summary>
[Table("steward_activities")]
public sealed class StewardActivity
{
    /// <summary>活动主键。</summary>
    [Key, Column("id")] public long Id { get; set; }
    /// <summary>所属家庭主键。</summary>
    [Column("home_id")] public long HomeId { get; set; }
    /// <summary>关联的 AgentRun 主键，可为空表示非运行期活动。</summary>
    [Column("run_id")] public long? RunId { get; set; }
    /// <summary>活动分类，参见 <see cref="StewardActivityCategory"/>。</summary>
    [Column("category")] public string Category { get; set; } = null!;
    /// <summary>活动标题。</summary>
    [Column("title")] public string Title { get; set; } = null!;
    /// <summary>活动描述。</summary>
    [Column("description")] public string? Description { get; set; }
    /// <summary>风险等级，参见 <see cref="ConfirmationRiskLevel"/>。</summary>
    [Column("risk_level")] public string RiskLevel { get; set; } = null!;
    /// <summary>活动状态，参见 <see cref="StewardActivityStatus"/>。</summary>
    [Column("status")] public string Status { get; set; } = StewardActivityStatus.Pending;
    /// <summary>结果摘要。</summary>
    [Column("result_summary")] public string? ResultSummary { get; set; }
    /// <summary>是否可被撤销。</summary>
    [Column("undoable")] public bool Undoable { get; set; }
    /// <summary>撤销时间（UTC）。</summary>
    [Column("undone_at")] public DateTime? UndoneAt { get; set; }
    /// <summary>创建时间（UTC）。</summary>
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    /// <summary>更新时间（UTC）。</summary>
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
    /// <summary>乐观锁版本号。</summary>
    [Column("row_version")] public long RowVersion { get; set; } = 1;
    /// <summary>同步版本号。</summary>
    [Column("sync_version")] public long SyncVersion { get; set; } = 1;
}

/// <summary>确认项实体，向用户呈现的待确认动作。</summary>
[Table("confirmation_items")]
public sealed class ConfirmationItem
{
    /// <summary>确认项主键。</summary>
    [Key, Column("id")] public long Id { get; set; }
    /// <summary>所属家庭主键。</summary>
    [Column("home_id")] public long HomeId { get; set; }
    /// <summary>关联的管家活动主键，可为空表示由系统直接生成。</summary>
    [Column("activity_id")] public long? ActivityId { get; set; }
    /// <summary>风险等级，参见 <see cref="ConfirmationRiskLevel"/>。</summary>
    [Column("risk_level")] public string RiskLevel { get; set; } = null!;
    /// <summary>确认项标题。</summary>
    [Column("title")] public string Title { get; set; } = null!;
    /// <summary>确认项描述。</summary>
    [Column("description")] public string? Description { get; set; }
    /// <summary>影响摘要。</summary>
    [Column("impact_summary")] public string? ImpactSummary { get; set; }
    /// <summary>建议动作文案。</summary>
    [Column("suggested_action")] public string? SuggestedAction { get; set; }
    /// <summary>确认项状态，参见 <see cref="ConfirmationItemStatus"/>。</summary>
    [Column("status")] public string Status { get; set; } = ConfirmationItemStatus.Pending;
    /// <summary>到期时间（UTC），到期后系统将自动取消。</summary>
    [Column("expires_at")] public DateTime? ExpiresAt { get; set; }
    /// <summary>确认操作用户标识。</summary>
    [Column("confirmed_by_user_id")] public long? ConfirmedByUserId { get; set; }
    /// <summary>确认时间（UTC）。</summary>
    [Column("confirmed_at")] public DateTime? ConfirmedAt { get; set; }
    /// <summary>拒绝操作用户标识。</summary>
    [Column("denied_by_user_id")] public long? DeniedByUserId { get; set; }
    /// <summary>拒绝时间（UTC）。</summary>
    [Column("denied_at")] public DateTime? DeniedAt { get; set; }
    /// <summary>拒绝原因，便于审计。</summary>
    [Column("denial_reason")] public string? DenialReason { get; set; }
    /// <summary>过期时间戳，由系统按策略回填。</summary>
    [Column("expired_at")] public DateTime? ExpiredAt { get; set; }
    /// <summary>创建时间（UTC）。</summary>
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    /// <summary>更新时间（UTC）。</summary>
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
    /// <summary>乐观锁版本号。</summary>
    [Column("row_version")] public long RowVersion { get; set; } = 1;
    /// <summary>同步版本号。</summary>
    [Column("sync_version")] public long SyncVersion { get; set; } = 1;
}

/// <summary>管家活动分类常量集合。</summary>
public static class StewardActivityCategory
{
    /// <summary>感知阶段：环境与上下文采集。</summary>
    public const string Sensing = "sensing";
    /// <summary>规划阶段：分析并生成动作草稿。</summary>
    public const string Planning = "planning";
    /// <summary>执行阶段：已确认动作正在执行。</summary>
    public const string Executing = "executing";
    /// <summary>汇报阶段：完成或失败的结果展示。</summary>
    public const string Reporting = "reporting";
}

/// <summary>管家活动状态常量集合。</summary>
public static class StewardActivityStatus
{
    /// <summary>等待用户确认。</summary>
    public const string Pending = "pending";
    /// <summary>已被用户确认。</summary>
    public const string Confirmed = "confirmed";
    /// <summary>正在执行。</summary>
    public const string Executing = "executing";
    /// <summary>已成功完成。</summary>
    public const string Completed = "completed";
    /// <summary>执行失败。</summary>
    public const string Failed = "failed";
    /// <summary>已取消。</summary>
    public const string Cancelled = "cancelled";
}

/// <summary>确认项状态常量集合。</summary>
public static class ConfirmationItemStatus
{
    /// <summary>等待用户处理。</summary>
    public const string Pending = "pending";
    /// <summary>已确认。</summary>
    public const string Confirmed = "confirmed";
    /// <summary>已拒绝。</summary>
    public const string Denied = "denied";
    /// <summary>已过期。</summary>
    public const string Expired = "expired";
    /// <summary>已取消。</summary>
    public const string Cancelled = "cancelled";
}

/// <summary>L1 批量确认的幂等记录；以 (home_id, idempotency_key) 唯一保存首次请求的确认项集合与结果，供重复请求重放。</summary>
[Table("confirmation_batch_records")]
public sealed class ConfirmationBatchRecord
{
    /// <summary>幂等记录主键。</summary>
    [Key, Column("id")] public long Id { get; set; }
    /// <summary>所属家庭主键。</summary>
    [Column("home_id")] public long HomeId { get; set; }
    /// <summary>幂等键（UUID），重复请求必须复用首次请求的键。</summary>
    [Column("idempotency_key")] public string IdempotencyKey { get; set; } = null!;
    /// <summary>首次请求的确认项 ID 数组 JSON，用于同键比对集合是否一致。</summary>
    [Column("confirmation_ids_json")] public string ConfirmationIdsJson { get; set; } = null!;
    /// <summary>首次确认的结果视图 JSON，重复请求直接重放该结果。</summary>
    [Column("result_json")] public string ResultJson { get; set; } = null!;
    /// <summary>创建时间（UTC）。</summary>
    [Column("created_at")] public DateTime CreatedAt { get; set; }
}

/// <summary>确认项风险等级常量集合。</summary>
public static class ConfirmationRiskLevel
{
    /// <summary>低风险，允许批量确认。</summary>
    public const string L1 = "L1";
    /// <summary>中风险，单项确认并附加影响说明。</summary>
    public const string L2 = "L2";
    /// <summary>高风险，单项确认并强制实时复核。</summary>
    public const string L3 = "L3";
}
