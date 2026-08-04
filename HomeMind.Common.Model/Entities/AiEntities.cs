using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HomeMind.Common.Model.Entities;

/// <summary>用户自定义 AI 技能表。</summary>
[Table("ai_skills")]
public sealed class AiSkill
{
    [Key, Column("id")] public long Id { get; set; }
    [Column("tenant_id")] public long TenantId { get; set; }
    [Column("user_id")] public long UserId { get; set; }
    [Column("name")] public string Name { get; set; } = null!;
    [Column("prompt")] public string Prompt { get; set; } = null!;
    [Column("scopes")] public string Scopes { get; set; } = "[]";
    [Column("is_builtin")] public bool IsBuiltin { get; set; }
    [Column("is_active")] public bool IsActive { get; set; } = true;
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
    [Column("deleted_at")] public DateTime? DeletedAt { get; set; }
}

[Table("experts")]
public sealed class Expert
{
    [Key, Column("id")] public long Id { get; set; }
    [Column("tenant_id")] public long TenantId { get; set; }
    [Column("owner_user_id")] public long? OwnerUserId { get; set; }
    [Column("code")] public string Code { get; set; } = null!;
    [Column("name")] public string Name { get; set; } = null!;
    [Column("category")] public string Category { get; set; } = null!;
    [Column("expert_type")] public string ExpertType { get; set; } = "builtin";
    [Column("status")] public string Status { get; set; } = "active";
    [Column("description")] public string? Description { get; set; }
    [Column("privacy_scope_json")] public string? PrivacyScope { get; set; }
}

[Table("expert_versions")]
public sealed class ExpertVersion
{
    [Key, Column("id")] public long Id { get; set; }
    [Column("tenant_id")] public long TenantId { get; set; }
    [Column("expert_id")] public long ExpertId { get; set; }
    [Column("version")] public int Version { get; set; }
    [Column("status")] public string Status { get; set; } = "published";
    [Column("persona")] public string Persona { get; set; } = null!;
    [Column("methodology")] public string Methodology { get; set; } = null!;
    [Column("prompt_template")] public string PromptTemplate { get; set; } = null!;
    [Column("tool_policy_json")] public string? ToolPolicy { get; set; }
    [Column("output_schema_json")] public string? OutputSchema { get; set; }
    [Column("estimated_credits")] public decimal EstimatedCredits { get; set; }
}

[Table("expert_groups")]
public sealed class ExpertGroup
{
    [Key, Column("id")] public long Id { get; set; }
    [Column("tenant_id")] public long TenantId { get; set; }
    [Column("owner_user_id")] public long? OwnerUserId { get; set; }
    [Column("code")] public string Code { get; set; } = null!;
    [Column("name")] public string Name { get; set; } = null!;
    [Column("category")] public string Category { get; set; } = null!;
    [Column("captain_expert_id")] public long CaptainExpertId { get; set; }
    [Column("status")] public string Status { get; set; } = "active";
    [Column("description")] public string? Description { get; set; }
}

[Table("expert_group_versions")]
public sealed class ExpertGroupVersion
{
    [Key, Column("id")] public long Id { get; set; }
    [Column("tenant_id")] public long TenantId { get; set; }
    [Column("group_id")] public long GroupId { get; set; }
    [Column("version")] public int Version { get; set; }
    [Column("status")] public string Status { get; set; } = "published";
    [Column("orchestration_policy_json")] public string? OrchestrationPolicy { get; set; }
    [Column("output_schema_json")] public string? OutputSchema { get; set; }
    [Column("estimated_credits")] public decimal EstimatedCredits { get; set; }
}

/// <summary>
/// AI Agent 的一次可审计运行。物理表保留为 expert_runs，以兼容既有 Todo、Calendar 和 Action 外键。
/// </summary>
[Table("expert_runs")]
public sealed class AgentRun
{
    [Key, Column("id")] public long Id { get; set; }
    [Column("tenant_id")] public long TenantId { get; set; }
    [Column("user_id")] public long UserId { get; set; }
    [Column("source_type")] public string SourceType { get; set; } = null!;
    [Column("expert_version_id")] public long? ExpertVersionId { get; set; }
    [Column("group_version_id")] public long? GroupVersionId { get; set; }
    [Column("request_idempotency_key")] public string RequestIdempotencyKey { get; set; } = null!;
    [Column("input_json")] public string Input { get; set; } = null!;
    [Column("status")] public string Status { get; set; } = "queued";
    [Column("result_json")] public string? Result { get; set; }
    [Column("result_summary")] public string? ResultSummary { get; set; }
    [Column("estimated_credits")] public decimal EstimatedCredits { get; set; }
    [Column("actual_credits")] public decimal ActualCredits { get; set; }
    [Column("cancel_requested_at")] public DateTime? CancelRequestedAt { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    [Column("started_at")] public DateTime? StartedAt { get; set; }
    [Column("finished_at")] public DateTime? FinishedAt { get; set; }
}

[Table("expert_jobs")]
public sealed class ExpertJob
{
    [Key, Column("id")] public long Id { get; set; }
    [Column("tenant_id")] public long TenantId { get; set; }
    [Column("run_id")] public long RunId { get; set; }
    [Column("job_type")] public string JobType { get; set; } = "plan";
    [Column("status")] public string Status { get; set; } = "queued";
    [Column("idempotency_key")] public string IdempotencyKey { get; set; } = null!;
}

[Table("run_events")]
public sealed class RunEvent
{
    [Key, Column("id")] public long Id { get; set; }
    [Column("tenant_id")] public long TenantId { get; set; }
    [Column("run_id")] public long RunId { get; set; }
    [Column("sequence")] public int Sequence { get; set; }
    [Column("event_type")] public string EventType { get; set; } = null!;
    [Column("display_payload_json")] public string Payload { get; set; } = null!;
    [Column("created_at")] public DateTime CreatedAt { get; set; }
}

[Table("expert_run_actions")]
public sealed class ExpertRunAction
{
    [Key, Column("id")] public long Id { get; set; }
    [Column("run_id")] public long RunId { get; set; }
    [Column("tenant_id")] public long TenantId { get; set; }
    [Column("user_id")] public long UserId { get; set; }
    [Column("action_type")] public string ActionType { get; set; } = null!;
    [Column("request_idempotency_key")] public string RequestIdempotencyKey { get; set; } = null!;
    [Column("request_json")] public string RequestJson { get; set; } = "{}";
    [Column("status")] public string Status { get; set; } = "pending";
    [Column("result_json")] public string? Result { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
}

[Table("action_execution_audits")]
public sealed class ActionExecutionAudit
{
    [Key, Column("id")] public long Id { get; set; }
    [Column("tenant_id")] public long TenantId { get; set; }
    [Column("run_action_id")] public long RunActionId { get; set; }
    [Column("operator_user_id")] public long OperatorUserId { get; set; }
    [Column("workspace_connector_id")] public long WorkspaceConnectorId { get; set; }
    [Column("device_id")] public long DeviceId { get; set; }
    [Column("idempotency_key")] public string IdempotencyKey { get; set; } = null!;
    [Column("status")] public string Status { get; set; } = "executing";
    [Column("command_json")] public string Command { get; set; } = "{}";
    [Column("result_json")] public string? Result { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
}

[Table("expert_files")]
public sealed class ExpertFile
{
    [Key, Column("id")] public long Id { get; set; }
    [Column("tenant_id")] public long TenantId { get; set; }
    [Column("owner_user_id")] public long OwnerUserId { get; set; }
    [Column("name")] public string Name { get; set; } = null!;
    [Column("mime_type")] public string MimeType { get; set; } = null!;
    [Column("size_bytes")] public long SizeBytes { get; set; }
    [Column("sha256")] public string Sha256 { get; set; } = null!;
    [Column("status")] public string Status { get; set; } = "pending_upload";
    [Column("scan_provider")] public string? ScanProvider { get; set; }
    [Column("scan_completed_at")] public DateTime? ScanCompletedAt { get; set; }
    [Column("rejection_reason")] public string? RejectionReason { get; set; }
    [Column("quota_bytes")] public long QuotaBytes { get; set; }
    [Column("expires_at")] public DateTime? ExpiresAt { get; set; }
    [Column("soft_deleted_at")] public DateTime? SoftDeletedAt { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
    [Column("row_version")] public long RowVersion { get; set; } = 1;
    [Column("sync_version")] public long SyncVersion { get; set; } = 1;
}

[Table("expert_file_objects")]
public sealed class ExpertFileObject
{
    [Key, Column("id")] public long Id { get; set; }
    [Column("expert_file_id")] public long ExpertFileId { get; set; }
    [Column("object_key")] public string ObjectKey { get; set; } = null!;
    [Column("size_bytes")] public long SizeBytes { get; set; }
    [Column("offset_bytes")] public long OffsetBytes { get; set; }
    [Column("uploaded_at")] public DateTime UploadedAt { get; set; }
}

[Table("expert_file_attachments")]
public sealed class ExpertFileAttachment
{
    [Key, Column("id")] public long Id { get; set; }
    [Column("tenant_id")] public long TenantId { get; set; }
    [Column("expert_file_id")] public long ExpertFileId { get; set; }
    [Column("expert_id")] public long? ExpertId { get; set; }
    [Column("agent_run_id")] public long? AgentRunId { get; set; }
    [Column("attached_by_user_id")] public long AttachedByUserId { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
}

[Table("team_run_templates")]
public sealed class TeamRunTemplate
{
    [Key, Column("id")] public long Id { get; set; }
    [Column("tenant_id")] public long TenantId { get; set; }
    [Column("owner_user_id")] public long OwnerUserId { get; set; }
    [Column("name")] public string Name { get; set; } = null!;
    [Column("team_version")] public int TeamVersion { get; set; }
    [Column("mode")] public string Mode { get; set; } = "sequential";
    [Column("graph_json")] public string GraphJson { get; set; } = "{}";
    [Column("approval_policy")] public string ApprovalPolicy { get; set; } = "manual_confirmation";
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
    [Column("row_version")] public long RowVersion { get; set; } = 1;
    [Column("sync_version")] public long SyncVersion { get; set; } = 1;
}

[Table("team_run_template_versions")]
public sealed class TeamRunTemplateVersion
{
    [Key, Column("id")] public long Id { get; set; }
    [Column("team_run_template_id")] public long TeamRunTemplateId { get; set; }
    [Column("tenant_id")] public long TenantId { get; set; }
    [Column("version")] public int Version { get; set; }
    [Column("members_json")] public string MembersJson { get; set; } = "[]";
    [Column("file_refs_json")] public string FileRefsJson { get; set; } = "[]";
    [Column("permission_intersections_json")] public string PermissionIntersectionsJson { get; set; } = "{}";
    [Column("graph_json")] public string GraphJson { get; set; } = "{}";
    [Column("created_at")] public DateTime CreatedAt { get; set; }
}

[Table("team_runs")]
public sealed class TeamRun
{
    [Key, Column("id")] public long Id { get; set; }
    [Column("tenant_id")] public long TenantId { get; set; }
    [Column("parent_agent_run_id")] public long ParentAgentRunId { get; set; }
    [Column("team_run_template_id")] public long TeamRunTemplateId { get; set; }
    [Column("team_run_template_version_id")] public long TeamRunTemplateVersionId { get; set; }
    [Column("team_version")] public int TeamVersion { get; set; }
    [Column("mode")] public string Mode { get; set; } = "sequential";
    [Column("status")] public string Status { get; set; } = "pending";
    [Column("synthesis_result_json")] public string? SynthesisResultJson { get; set; }
    [Column("last_error_code")] public string? LastErrorCode { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
    [Column("row_version")] public long RowVersion { get; set; } = 1;
    [Column("sync_version")] public long SyncVersion { get; set; } = 1;
}

[Table("team_run_members")]
public sealed class TeamRunMember
{
    [Key, Column("id")] public long Id { get; set; }
    [Column("tenant_id")] public long TenantId { get; set; }
    [Column("team_run_id")] public long TeamRunId { get; set; }
    [Column("expert_version_id")] public long ExpertVersionId { get; set; }
    [Column("child_agent_run_id")] public long? ChildAgentRunId { get; set; }
    [Column("display_name")] public string DisplayName { get; set; } = null!;
    [Column("stage_order")] public int StageOrder { get; set; }
    [Column("permission_intersection_json")] public string PermissionIntersectionJson { get; set; } = "{}";
    [Column("status")] public string Status { get; set; } = "pending";
    [Column("last_error_code")] public string? LastErrorCode { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
}

[Table("team_run_audits")]
public sealed class TeamRunAudit
{
    [Key, Column("id")] public long Id { get; set; }
    [Column("tenant_id")] public long TenantId { get; set; }
    [Column("actor_user_id")] public long? ActorUserId { get; set; }
    [Column("team_run_id")] public long? TeamRunId { get; set; }
    [Column("expert_file_id")] public long? ExpertFileId { get; set; }
    [Column("team_run_member_id")] public long? TeamRunMemberId { get; set; }
    [Column("action")] public string Action { get; set; } = null!;
    [Column("result")] public string Result { get; set; } = "success";
    [Column("error_code")] public string? ErrorCode { get; set; }
    [Column("payload_json")] public string? PayloadJson { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
}

public static class ExpertFileStatus
{
    public const string PendingUpload = "pending_upload";
    public const string Scanning = "scanning";
    public const string Ready = "ready";
    public const string Rejected = "rejected";
    public const string Deleted = "deleted";
}

public static class TeamRunMode
{
    public const string Sequential = "sequential";
    public const string Parallel = "parallel";
    public const string Synthesis = "synthesis";
}

public static class TeamRunStatus
{
    public const string Pending = "pending";
    public const string Running = "running";
    public const string Completed = "completed";
    public const string Failed = "failed";
    public const string Cancelled = "cancelled";
}

public static class TeamRunMemberStatus
{
    public const string Pending = "pending";
    public const string Running = "running";
    public const string Completed = "completed";
    public const string Failed = "failed";
    public const string Cancelled = "cancelled";
    public const string Skipped = "skipped";
}
