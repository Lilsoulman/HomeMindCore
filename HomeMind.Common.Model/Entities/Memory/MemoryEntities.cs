using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HomeMind.Common.Model.Entities.Memory;

/// <summary>AI 后台复盘生成的待审核记忆候选。</summary>
[Table("memory_candidates")]
public sealed class MemoryCandidate
{
    /// <summary>候选主键。</summary>
    [Key, Column("id")] public long Id { get; set; }
    /// <summary>所属家庭主键。</summary>
    [Column("home_id")] public long HomeId { get; set; }
    /// <summary>个人候选归属用户；家庭候选为空。</summary>
    [Column("owner_user_id")] public long? OwnerUserId { get; set; }
    /// <summary>来源运行主键。</summary>
    [Column("source_run_id")] public long? SourceRunId { get; set; }
    /// <summary>候选类型。</summary>
    [Column("kind")] public string Kind { get; set; } = null!;
    /// <summary>候选可见性。</summary>
    [Column("visibility")] public string Visibility { get; set; } = null!;
    /// <summary>候选键。</summary>
    [Column("memory_key")] public string Key { get; set; } = null!;
    /// <summary>建议写入的值。</summary>
    [Column("proposed_value")] public string ProposedValue { get; set; } = null!;
    /// <summary>安全的展示摘要。</summary>
    [Column("display_summary")] public string DisplaySummary { get; set; } = null!;
    /// <summary>家庭知识分类。</summary>
    [Column("category")] public string? Category { get; set; }
    /// <summary>置信度。</summary>
    [Column("confidence")] public decimal Confidence { get; set; }
    /// <summary>证据引用 JSON，仅服务端审计使用。</summary>
    [Column("evidence_refs_json")] public string? EvidenceRefsJson { get; set; }
    /// <summary>候选风险等级。</summary>
    [Column("risk_level")] public string RiskLevel { get; set; } = null!;
    /// <summary>候选状态。</summary>
    [Column("status")] public string Status { get; set; } = null!;
    /// <summary>解决候选的用户标识。</summary>
    [Column("resolved_by_user_id")] public long? ResolvedByUserId { get; set; }
    /// <summary>解决时间。</summary>
    [Column("resolved_at")] public DateTime? ResolvedAt { get; set; }
    /// <summary>过期时间。</summary>
    [Column("expires_at")] public DateTime? ExpiresAt { get; set; }
    /// <summary>创建时间。</summary>
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    /// <summary>更新时间。</summary>
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
}

/// <summary>Records that a completed Run has been reviewed for memory proposals, including an empty result.</summary>
[Table("memory_review_receipts")]
public sealed class MemoryReviewReceipt
{
    /// <summary>Receipt primary key.</summary>
    [Key, Column("id")] public long Id { get; set; }
    /// <summary>Reviewed source Run; one receipt is allowed per Run.</summary>
    [Column("source_run_id")] public long SourceRunId { get; set; }
    /// <summary>Number of pending candidates created from the Run.</summary>
    [Column("candidate_count")] public int CandidateCount { get; set; }
    /// <summary>UTC time at which the Run was reviewed.</summary>
    [Column("reviewed_at")] public DateTime ReviewedAt { get; set; }
}

/// <summary>成员个人可召回偏好事实，不向其他成员公开。</summary>
[Table("personal_memory_preferences")]
public sealed class PersonalMemoryPreference
{
    /// <summary>偏好事实主键。</summary>
    [Key, Column("id")] public long Id { get; set; }
    /// <summary>所属家庭主键。</summary>
    [Column("home_id")] public long HomeId { get; set; }
    /// <summary>归属用户主键。</summary>
    [Column("owner_user_id")] public long OwnerUserId { get; set; }
    /// <summary>偏好键。</summary>
    [Column("preference_key")] public string Key { get; set; } = null!;
    /// <summary>偏好值。</summary>
    [Column("preference_value")] public string Value { get; set; } = null!;
    /// <summary>展示摘要。</summary>
    [Column("display_summary")] public string DisplaySummary { get; set; } = null!;
    /// <summary>状态。</summary>
    [Column("status")] public string Status { get; set; } = MemoryRecordStatus.Active;
    /// <summary>创建时间。</summary>
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    /// <summary>更新时间。</summary>
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
}

/// <summary>面向客户端的已接受学习记忆投影。</summary>
[Table("learning_memory_records")]
public sealed class LearningMemoryRecord
{
    /// <summary>投影主键。</summary>
    [Key, Column("id")] public long Id { get; set; }
    /// <summary>所属家庭主键。</summary>
    [Column("home_id")] public long HomeId { get; set; }
    /// <summary>个人记忆归属用户；家庭记忆为空。</summary>
    [Column("owner_user_id")] public long? OwnerUserId { get; set; }
    /// <summary>候选主键，同一候选仅能生成一个投影。</summary>
    [Column("candidate_id")] public long CandidateId { get; set; }
    /// <summary>目标事实源类型。</summary>
    [Column("target_type")] public string TargetType { get; set; } = null!;
    /// <summary>目标事实源主键。</summary>
    [Column("target_id")] public long TargetId { get; set; }
    /// <summary>记忆类型。</summary>
    [Column("kind")] public string Kind { get; set; } = null!;
    /// <summary>可见性。</summary>
    [Column("visibility")] public string Visibility { get; set; } = null!;
    /// <summary>展示摘要。</summary>
    [Column("display_summary")] public string DisplaySummary { get; set; } = null!;
    /// <summary>稳定性。</summary>
    [Column("stability")] public decimal Stability { get; set; }
    /// <summary>状态。</summary>
    [Column("status")] public string Status { get; set; } = MemoryRecordStatus.Active;
    /// <summary>来源运行主键。</summary>
    [Column("source_run_id")] public long? SourceRunId { get; set; }
    /// <summary>学习时间。</summary>
    [Column("learned_at")] public DateTime LearnedAt { get; set; }
    /// <summary>过期时间。</summary>
    [Column("expires_at")] public DateTime? ExpiresAt { get; set; }
    /// <summary>归档时间。</summary>
    [Column("archived_at")] public DateTime? ArchivedAt { get; set; }
    /// <summary>创建时间。</summary>
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    /// <summary>更新时间。</summary>
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
}

/// <summary>记忆可见性常量。</summary>
public static class MemoryVisibility
{
    /// <summary>仅归属用户可见。</summary>
    public const string Personal = "personal";
    /// <summary>家庭成员可见。</summary>
    public const string Family = "family";
}

/// <summary>记忆候选状态常量。</summary>
public static class MemoryCandidateStatus
{
    /// <summary>等待审核。</summary>
    public const string Pending = "pending";
    /// <summary>已接受并写入事实源。</summary>
    public const string Accepted = "accepted";
    /// <summary>已拒绝。</summary>
    public const string Rejected = "rejected";
    /// <summary>已过期。</summary>
    public const string Expired = "expired";
}

/// <summary>学习记忆状态常量。</summary>
public static class MemoryRecordStatus
{
    /// <summary>当前可召回。</summary>
    public const string Active = "active";
    /// <summary>保留历史但不再召回。</summary>
    public const string Archived = "archived";
    /// <summary>到期且不再召回。</summary>
    public const string Expired = "expired";
}
