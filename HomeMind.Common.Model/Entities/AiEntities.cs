using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HomeMind.Common.Model.Entities;

/// <summary>用户自定义 AI 技能表。</summary>
[Table("ai_skills")]
public sealed class AiSkill
{
    /// <summary>技能主键。</summary>
    [Key, Column("id")] public long Id { get; set; }
    /// <summary>所属租户标识，从 JWT 派生，客户端不可覆盖。</summary>
    [Column("tenant_id")] public long TenantId { get; set; }
    /// <summary>技能创建者用户标识。</summary>
    [Column("user_id")] public long UserId { get; set; }
    /// <summary>技能对外展示名称。</summary>
    [Column("name")] public string Name { get; set; } = null!;
    /// <summary>技能系统提示词模板，运行期由智能体运行时使用。</summary>
    [Column("prompt")] public string Prompt { get; set; } = null!;
    /// <summary>技能被调用时所需授权范围的 JSON 序列化为字符串。</summary>
    [Column("scopes")] public string Scopes { get; set; } = "[]";
    /// <summary>是否系统内置技能，禁用由用户删除或编辑。</summary>
    [Column("is_builtin")] public bool IsBuiltin { get; set; }
    /// <summary>技能启用状态；停用后不再出现在调用候选中。</summary>
    [Column("is_active")] public bool IsActive { get; set; } = true;
    /// <summary>记录创建时间（UTC）。</summary>
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    /// <summary>记录最近一次修改时间（UTC）。</summary>
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
    /// <summary>逻辑删除标记，软删除时填写时间戳。</summary>
    [Column("deleted_at")] public DateTime? DeletedAt { get; set; }
}

/// <summary>用户 AI 调用配置表，每用户一行，主键即用户标识。</summary>
[Table("ai_configs")]
public sealed class AiConfig
{
    /// <summary>配置所属用户标识。</summary>
    [Key, Column("user_id")] public long UserId { get; set; }
    /// <summary>OpenAI 兼容的 API 端点地址，如 https://api.openai.com/v1。</summary>
    [Column("endpoint", TypeName = "varchar(512)")] public string Endpoint { get; set; } = null!;
    /// <summary>默认使用的模型名称，如 gpt-4.1-mini。</summary>
    [Column("model", TypeName = "varchar(128)")] public string Model { get; set; } = null!;
    /// <summary>生成温度参数，取值范围 0~1，精确到两位小数。</summary>
    [Column("temperature", TypeName = "decimal(3,2)")] public double Temperature { get; set; } = 0.7;
    /// <summary>是否启用 AI 生成能力；<c>false</c> 时 <c>/api/v1/ai/{generate,chat,stream}</c> 与专家运行整体不可用，调用方应返回 422。</summary>
    [Column("enabled", TypeName = "tinyint(1)")] public bool Enabled { get; set; } = true;
    /// <summary>API 密钥密文，由 SecretProtector 加密，永不回传客户端；未配置时为空数组。</summary>
    [Column("api_key_encrypted", TypeName = "blob")] public byte[] ApiKeyEncrypted { get; set; } = Array.Empty<byte>();
    /// <summary>记录最近一次修改时间（UTC）。</summary>
    [Column("updated_at", TypeName = "datetime(3)")] public DateTime UpdatedAt { get; set; }
    /// <summary>逻辑删除标记，软删除时填写时间戳。</summary>
    [Column("deleted_at", TypeName = "datetime(3)")] public DateTime? DeletedAt { get; set; }
    /// <summary>行版本号，用于同步冲突检测。</summary>
    [Column("sync_version")] public long SyncVersion { get; set; }
}

/// <summary>专家目录，保存专家模板与所属租户关系。</summary>
[Table("experts")]
public sealed class Expert
{
    /// <summary>专家主键。</summary>
    [Key, Column("id")] public long Id { get; set; }
    /// <summary>所属租户标识。</summary>
    [Column("tenant_id")] public long TenantId { get; set; }
    /// <summary>专家所有者用户标识，可为空表示平台内置。</summary>
    [Column("owner_user_id")] public long? OwnerUserId { get; set; }
    /// <summary>专家业务编码，在租户内唯一。</summary>
    [Column("code")] public string Code { get; set; } = null!;
    /// <summary>专家对外展示名称。</summary>
    [Column("name")] public string Name { get; set; } = null!;
    /// <summary>专家分类，如"生活管家""日程协调"等。</summary>
    [Column("category")] public string Category { get; set; } = null!;
    /// <summary>专家类型，取值参见 <see cref="ExpertType"/>。</summary>
    [Column("expert_type")] public string ExpertType { get; set; } = "builtin";
    /// <summary>专家状态，取值参见 <see cref="ExpertStatus"/>。</summary>
    [Column("status")] public string Status { get; set; } = "active";
    /// <summary>专家的描述信息，Swagger 展示与运行期说明使用。</summary>
    [Column("description")] public string? Description { get; set; }
    /// <summary>专家可见的隐私范围 JSON 字符串，由智能体运行时解析。</summary>
    [Column("privacy_scope_json")] public string? PrivacyScope { get; set; }
    /// <summary>创建时间（UTC）。</summary>
    [Column("created_at", TypeName = "datetime(3)")] public DateTime CreatedAt { get; set; }
    /// <summary>最近更新时间（UTC）。</summary>
    [Column("updated_at", TypeName = "datetime(3)")] public DateTime UpdatedAt { get; set; }
    /// <summary>软删除时间（UTC），非空表示已删除；已删专家从目录、运行解析与会话发送全部消失。</summary>
    [Column("deleted_at", TypeName = "datetime(3)")] public DateTime? DeletedAt { get; set; }
    /// <summary>行版本号，乐观锁比较字段，更新时递增。</summary>
    [ConcurrencyCheck, Column("row_version")] public long RowVersion { get; set; } = 1;
}

/// <summary>专家版本快照，发布后不可变；运行期引用具体版本。</summary>
[Table("expert_versions")]
public sealed class ExpertVersion
{
    /// <summary>专家版本主键。</summary>
    [Key, Column("id")] public long Id { get; set; }
    /// <summary>所属租户标识。</summary>
    [Column("tenant_id")] public long TenantId { get; set; }
    /// <summary>所属专家模板主键。</summary>
    [Column("expert_id")] public long ExpertId { get; set; }
    /// <summary>从 1 起的版本号，租户内专家内单调递增。</summary>
    [Column("version")] public int Version { get; set; }
    /// <summary>版本状态，取值参见 <see cref="ExpertVersionStatus"/>。</summary>
    [Column("status")] public string Status { get; set; } = "published";
    /// <summary>角色设定，运行期提示词的人设片段。</summary>
    [Column("persona")] public string Persona { get; set; } = null!;
    /// <summary>方法论说明，影响思考链风格。</summary>
    [Column("methodology")] public string Methodology { get; set; } = null!;
    /// <summary>完整提示词模板。</summary>
    [Column("prompt_template")] public string PromptTemplate { get; set; } = null!;
    /// <summary>工具策略 JSON，决定可调用的 Skill / Connector 集合。</summary>
    [Column("tool_policy_json")] public string? ToolPolicy { get; set; }
    /// <summary>输出契约 JSON，用于结构化校验。</summary>
    [Column("output_schema_json")] public string? OutputSchema { get; set; }
    /// <summary>单次运行的预估积分消耗。</summary>
    [Column("estimated_credits")] public decimal EstimatedCredits { get; set; }
}

/// <summary>专家组目录，可作为团队运行模板的根。</summary>
[Table("expert_groups")]
public sealed class ExpertGroup
{
    /// <summary>专家组主键。</summary>
    [Key, Column("id")] public long Id { get; set; }
    /// <summary>所属租户标识。</summary>
    [Column("tenant_id")] public long TenantId { get; set; }
    /// <summary>所有者用户标识，可为空表示平台内置。</summary>
    [Column("owner_user_id")] public long? OwnerUserId { get; set; }
    /// <summary>专家组业务编码，租户内唯一。</summary>
    [Column("code")] public string Code { get; set; } = null!;
    /// <summary>专家组对外展示名称。</summary>
    [Column("name")] public string Name { get; set; } = null!;
    /// <summary>专家组分类。</summary>
    [Column("category")] public string Category { get; set; } = null!;
    /// <summary>队长专家版本主键，承担汇总/裁决角色。</summary>
    [Column("captain_expert_id")] public long CaptainExpertId { get; set; }
    /// <summary>专家组状态。</summary>
    [Column("status")] public string Status { get; set; } = "active";
    /// <summary>专家组描述。</summary>
    [Column("description")] public string? Description { get; set; }
}

/// <summary>专家组版本快照，发布后不可变。</summary>
[Table("expert_group_versions")]
public sealed class ExpertGroupVersion
{
    /// <summary>专家组版本主键。</summary>
    [Key, Column("id")] public long Id { get; set; }
    /// <summary>所属租户标识。</summary>
    [Column("tenant_id")] public long TenantId { get; set; }
    /// <summary>所属专家组主键。</summary>
    [Column("group_id")] public long GroupId { get; set; }
    /// <summary>从 1 起的版本号，租户内专家组内单调递增。</summary>
    [Column("version")] public int Version { get; set; }
    /// <summary>版本状态。</summary>
    [Column("status")] public string Status { get; set; } = "published";
    /// <summary>编排策略 JSON，包含成员顺序、并行/串行、合成方式等。</summary>
    [Column("orchestration_policy_json")] public string? OrchestrationPolicy { get; set; }
    /// <summary>输出契约 JSON。</summary>
    [Column("output_schema_json")] public string? OutputSchema { get; set; }
    /// <summary>单次运行的预估积分消耗。</summary>
    [Column("estimated_credits")] public decimal EstimatedCredits { get; set; }
}

/// <summary>专家会话表：用户围绕某领域创建的对话框，绑定专家与连接器（连接器在本阶段仅作元数据落库）。</summary>
[Table("conversations")]
public sealed class Conversation
{
    /// <summary>会话主键。</summary>
    [Key, Column("id")] public long Id { get; set; }
    /// <summary>所属租户标识，会话为个人资源，与 owner 共同隔离。</summary>
    [Column("tenant_id")] public long TenantId { get; set; }
    /// <summary>会话所有者用户标识；仅本人可读写，跨用户/跨租户一律 404。</summary>
    [Column("owner_user_id")] public long OwnerUserId { get; set; }
    /// <summary>会话标题。</summary>
    [Column("title", TypeName = "varchar(64)")] public string Title { get; set; } = null!;
    /// <summary>绑定的专家主键，可空表示尚未选择专家。</summary>
    [Column("expert_id")] public long? ExpertId { get; set; }
    /// <summary>绑定的专家版本主键，与专家同空或同非空。</summary>
    [Column("expert_version_id")] public long? ExpertVersionId { get; set; }
    /// <summary>绑定的连接器实例主键（单值），本阶段仅元数据，多连接器后续演进。</summary>
    [Column("workspace_connector_id")] public long? WorkspaceConnectorId { get; set; }
    /// <summary>创建时间（UTC）。</summary>
    [Column("created_at", TypeName = "datetime(3)")] public DateTime CreatedAt { get; set; }
    /// <summary>最近更新时间（UTC）。</summary>
    [Column("updated_at", TypeName = "datetime(3)")] public DateTime UpdatedAt { get; set; }
    /// <summary>软删除时间（UTC），非空表示已删除。</summary>
    [Column("deleted_at", TypeName = "datetime(3)")] public DateTime? DeletedAt { get; set; }
    /// <summary>行版本号，乐观锁比较字段，更新时递增。</summary>
    [ConcurrencyCheck, Column("row_version")] public long RowVersion { get; set; } = 1;
}

/// <summary>会话内对话消息；user 消息在发送时落库，assistant 消息在 Run 终态后落库，均保留 run_id 供追溯。</summary>
[Table("conversation_messages")]
public sealed class ConversationMessage
{
    /// <summary>消息主键。</summary>
    [Key, Column("id")] public long Id { get; set; }
    /// <summary>所属会话主键。</summary>
    [Column("conversation_id")] public long ConversationId { get; set; }
    /// <summary>消息角色，取值 user/assistant。</summary>
    [Column("role", TypeName = "varchar(16)")] public string Role { get; set; } = null!;
    /// <summary>消息内容；不包含 Prompt 或模型思考链。</summary>
    [Column("content", TypeName = "text")] public string Content { get; set; } = null!;
    /// <summary>关联的 Agent 运行主键，可空表示尚未追溯（如历史导入）；同会话内唯一。</summary>
    [Column("run_id")] public long? RunId { get; set; }
    /// <summary>消息创建时间（UTC）。</summary>
    [Column("created_at", TypeName = "datetime(3)")] public DateTime CreatedAt { get; set; }
}

/// <summary>
/// AI Agent 的一次可审计运行。物理表保留为 expert_runs，以兼容既有 Todo、Calendar 和 Action 外键。
/// </summary>
[Table("expert_runs")]
public sealed class AgentRun
{
    /// <summary>运行主键。</summary>
    [Key, Column("id")] public long Id { get; set; }
    /// <summary>所属租户标识。</summary>
    [Column("tenant_id")] public long TenantId { get; set; }
    /// <summary>发起运行的用户标识。</summary>
    [Column("user_id")] public long UserId { get; set; }
    /// <summary>运行来源，如"steward""expert_run"等。</summary>
    [Column("source_type")] public string SourceType { get; set; } = null!;
    /// <summary>所引用的专家版本主键，单专家运行时使用。</summary>
    [Column("expert_version_id")] public long? ExpertVersionId { get; set; }
    /// <summary>所引用的专家组版本主键，团队运行时使用。</summary>
    [Column("group_version_id")] public long? GroupVersionId { get; set; }
    /// <summary>请求级幂等键，重复请求复用结果。</summary>
    [Column("request_idempotency_key")] public string RequestIdempotencyKey { get; set; } = null!;
    /// <summary>输入负载 JSON，由智能体运行时解析。</summary>
    [Column("input_json")] public string Input { get; set; } = null!;
    /// <summary>运行状态，取值参见 <see cref="AgentRunStatus"/>。</summary>
    [Column("status")] public string Status { get; set; } = "queued";
    /// <summary>运行模式，如"single""steward"等。</summary>
    [Column("mode")] public string Mode { get; set; } = "single";
    /// <summary>自动确认策略，如"never""L3_only"等。</summary>
    [Column("auto_confirm_policy")] public string AutoConfirmPolicy { get; set; } = "never";
    /// <summary>运行创建时的权限快照 JSON（scope/owner 与连接器授权摘要），Action 确认与执行前实时复验。</summary>
    [Column("permission_snapshot_json")] public string? PermissionSnapshot { get; set; }
    /// <summary>结果负载 JSON。</summary>
    [Column("result_json")] public string? Result { get; set; }
    /// <summary>面向用户的结果摘要。</summary>
    [Column("result_summary")] public string? ResultSummary { get; set; }
    /// <summary>预估积分。</summary>
    [Column("estimated_credits")] public decimal EstimatedCredits { get; set; }
    /// <summary>实际扣减积分。</summary>
    [Column("actual_credits")] public decimal ActualCredits { get; set; }
    /// <summary>取消请求时间戳，存在时表示客户端已请求取消。</summary>
    [Column("cancel_requested_at")] public DateTime? CancelRequestedAt { get; set; }
    /// <summary>创建时间（UTC）。</summary>
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    /// <summary>实际开始时间（UTC）。</summary>
    [Column("started_at")] public DateTime? StartedAt { get; set; }
    /// <summary>结束时间（UTC），失败与取消也会写入。</summary>
    [Column("finished_at")] public DateTime? FinishedAt { get; set; }
    /// <summary>所属会话主键，可空表示非会话运行；会话运行终态后据此追加 assistant 消息。</summary>
    [Column("conversation_id")] public long? ConversationId { get; set; }
}

/// <summary>智能体运行期作业，承载规划、确认、重试等任务。</summary>
[Table("expert_jobs")]
public sealed class ExpertJob
{
    /// <summary>作业主键。</summary>
    [Key, Column("id")] public long Id { get; set; }
    /// <summary>所属租户标识。</summary>
    [Column("tenant_id")] public long TenantId { get; set; }
    /// <summary>所属运行主键。</summary>
    [Column("run_id")] public long RunId { get; set; }
    /// <summary>作业类型，如"plan""tool_call"等。</summary>
    [Column("job_type")] public string JobType { get; set; } = "plan";
    /// <summary>作业状态。</summary>
    [Column("status")] public string Status { get; set; } = "queued";
    /// <summary>作业幂等键，用于去重。</summary>
    [Column("idempotency_key")] public string IdempotencyKey { get; set; } = null!;
}

/// <summary>运行事件，审计派生；不含提示与模型原始输出。</summary>
[Table("run_events")]
public sealed class RunEvent
{
    /// <summary>事件主键。</summary>
    [Key, Column("id")] public long Id { get; set; }
    /// <summary>所属租户标识。</summary>
    [Column("tenant_id")] public long TenantId { get; set; }
    /// <summary>所属运行主键。</summary>
    [Column("run_id")] public long RunId { get; set; }
    /// <summary>事件序号，租户内运行内单调递增。</summary>
    [Column("sequence")] public int Sequence { get; set; }
    /// <summary>事件类型，定义参见 RunEventTypes。</summary>
    [Column("event_type")] public string EventType { get; set; } = null!;
    /// <summary>展示安全的负载 JSON。</summary>
    [Column("display_payload_json")] public string Payload { get; set; } = null!;
    /// <summary>事件创建时间（UTC）。</summary>
    [Column("created_at")] public DateTime CreatedAt { get; set; }
}

/// <summary>专家运行的待执行动作，提交后由用户确认或自动执行。</summary>
[Table("expert_run_actions")]
public sealed class ExpertRunAction
{
    /// <summary>动作主键。</summary>
    [Key, Column("id")] public long Id { get; set; }
    /// <summary>所属运行主键。</summary>
    [Column("run_id")] public long RunId { get; set; }
    /// <summary>所属租户标识。</summary>
    [Column("tenant_id")] public long TenantId { get; set; }
    /// <summary>触发动作的用户标识。</summary>
    [Column("user_id")] public long UserId { get; set; }
    /// <summary>动作类型，如"smart_home_device"等。</summary>
    [Column("action_type")] public string ActionType { get; set; } = null!;
    /// <summary>请求级幂等键，避免重复触发同一动作。</summary>
    [Column("request_idempotency_key")] public string RequestIdempotencyKey { get; set; } = null!;
    /// <summary>动作请求负载 JSON。</summary>
    [Column("request_json")] public string RequestJson { get; set; } = "{}";
    /// <summary>动作状态，取值参见 <see cref="ExpertRunActionStatus"/>。</summary>
    [Column("status")] public string Status { get; set; } = "pending";
    /// <summary>动作结果 JSON。</summary>
    [Column("result_json")] public string? Result { get; set; }
    /// <summary>动作创建时间（UTC）。</summary>
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    /// <summary>动作最近修改时间（UTC）。</summary>
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
}

/// <summary>动作执行审计，记录每次确认/调度尝试。</summary>
[Table("action_execution_audits")]
public sealed class ActionExecutionAudit
{
    /// <summary>审计主键。</summary>
    [Key, Column("id")] public long Id { get; set; }
    /// <summary>所属租户标识。</summary>
    [Column("tenant_id")] public long TenantId { get; set; }
    /// <summary>关联的动作主键。</summary>
    [Column("run_action_id")] public long RunActionId { get; set; }
    /// <summary>实际执行动作的用户标识（含自动策略归属）。</summary>
    [Column("operator_user_id")] public long OperatorUserId { get; set; }
    /// <summary>调度所用工作区连接器主键；非设备类动作（如日历同步）为空。</summary>
    [Column("workspace_connector_id")] public long? WorkspaceConnectorId { get; set; }
    /// <summary>目标设备主键；非设备类动作（如日历同步）为空。</summary>
    [Column("device_id")] public long? DeviceId { get; set; }
    /// <summary>幂等键，确认/取消/重试均会复用。</summary>
    [Column("idempotency_key")] public string IdempotencyKey { get; set; } = null!;
    /// <summary>审计状态，取值参见 <see cref="ActionExecutionAuditStatus"/>。</summary>
    [Column("status")] public string Status { get; set; } = "executing";
    /// <summary>调度命令 JSON，展示安全版本。</summary>
    [Column("command_json")] public string Command { get; set; } = "{}";
    /// <summary>执行结果 JSON。</summary>
    [Column("result_json")] public string? Result { get; set; }
    /// <summary>创建时间（UTC）。</summary>
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    /// <summary>更新时间（UTC）。</summary>
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
}

/// <summary>专家文件主表，记录元数据与生命周期状态。</summary>
[Table("expert_files")]
public sealed class ExpertFile
{
    /// <summary>文件主键。</summary>
    [Key, Column("id")] public long Id { get; set; }
    /// <summary>所属租户标识。</summary>
    [Column("tenant_id")] public long TenantId { get; set; }
    /// <summary>文件所有者用户标识。</summary>
    [Column("owner_user_id")] public long OwnerUserId { get; set; }
    /// <summary>文件对外展示名称。</summary>
    [Column("name")] public string Name { get; set; } = null!;
    /// <summary>MIME 类型，扫描与展示使用。</summary>
    [Column("mime_type")] public string MimeType { get; set; } = null!;
    /// <summary>文件字节数。</summary>
    [Column("size_bytes")] public long SizeBytes { get; set; }
    /// <summary>文件 SHA-256 摘要的十六进制字符串。</summary>
    [Column("sha256")] public string Sha256 { get; set; } = null!;
    /// <summary>文件状态，取值参见 <see cref="ExpertFileStatus"/>。</summary>
    [Column("status")] public string Status { get; set; } = "pending_upload";
    /// <summary>扫描提供方名称，由扫描器在完成后回填。</summary>
    [Column("scan_provider")] public string? ScanProvider { get; set; }
    /// <summary>扫描完成时间（UTC）。</summary>
    [Column("scan_completed_at")] public DateTime? ScanCompletedAt { get; set; }
    /// <summary>拒绝原因，文件被扫描器拒绝时填写。</summary>
    [Column("rejection_reason")] public string? RejectionReason { get; set; }
    /// <summary>文件额度上限（字节）。</summary>
    [Column("quota_bytes")] public long QuotaBytes { get; set; }
    /// <summary>文件过期时间（UTC），到期后不再可附件。</summary>
    [Column("expires_at")] public DateTime? ExpiresAt { get; set; }
    /// <summary>软删除时间戳。</summary>
    [Column("soft_deleted_at")] public DateTime? SoftDeletedAt { get; set; }
    /// <summary>创建时间（UTC）。</summary>
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    /// <summary>更新时间（UTC）。</summary>
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
    /// <summary>乐观锁版本号，更新时必须带回。</summary>
    [Column("row_version")] public long RowVersion { get; set; } = 1;
    /// <summary>同步版本号，离线同步使用。</summary>
    [Column("sync_version")] public long SyncVersion { get; set; } = 1;
}

/// <summary>专家文件分片对象表，记录已上传的对象块。</summary>
[Table("expert_file_objects")]
public sealed class ExpertFileObject
{
    /// <summary>对象主键。</summary>
    [Key, Column("id")] public long Id { get; set; }
    /// <summary>所属文件主键。</summary>
    [Column("expert_file_id")] public long ExpertFileId { get; set; }
    /// <summary>对象存储内部键，API 不返回。</summary>
    [Column("object_key")] public string ObjectKey { get; set; } = null!;
    /// <summary>对象块字节大小。</summary>
    [Column("size_bytes")] public long SizeBytes { get; set; }
    /// <summary>对象块相对文件起始偏移。</summary>
    [Column("offset_bytes")] public long OffsetBytes { get; set; }
    /// <summary>对象块上传完成时间（UTC）。</summary>
    [Column("uploaded_at")] public DateTime UploadedAt { get; set; }
}

/// <summary>专家文件附件关系，关联专家或运行。</summary>
[Table("expert_file_attachments")]
public sealed class ExpertFileAttachment
{
    /// <summary>附件主键。</summary>
    [Key, Column("id")] public long Id { get; set; }
    /// <summary>所属租户标识。</summary>
    [Column("tenant_id")] public long TenantId { get; set; }
    /// <summary>关联文件主键。</summary>
    [Column("expert_file_id")] public long ExpertFileId { get; set; }
    /// <summary>关联专家主键，可为空表示运行附件。</summary>
    [Column("expert_id")] public long? ExpertId { get; set; }
    /// <summary>关联智能体运行主键。</summary>
    [Column("agent_run_id")] public long? AgentRunId { get; set; }
    /// <summary>附件操作者用户标识。</summary>
    [Column("attached_by_user_id")] public long AttachedByUserId { get; set; }
    /// <summary>创建时间（UTC）。</summary>
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    /// <summary>更新时间（UTC）。</summary>
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
}

/// <summary>团队运行模板主表，保存可复用的编排蓝图。</summary>
[Table("team_run_templates")]
public sealed class TeamRunTemplate
{
    /// <summary>模板主键。</summary>
    [Key, Column("id")] public long Id { get; set; }
    /// <summary>所属租户标识。</summary>
    [Column("tenant_id")] public long TenantId { get; set; }
    /// <summary>模板所有者用户标识。</summary>
    [Column("owner_user_id")] public long OwnerUserId { get; set; }
    /// <summary>模板名称。</summary>
    [Column("name")] public string Name { get; set; } = null!;
    /// <summary>团队协议版本号，V1 时固定为 1。</summary>
    [Column("team_version")] public int TeamVersion { get; set; }
    /// <summary>运行模式，取值参见 <see cref="TeamRunMode"/>。</summary>
    [Column("mode")] public string Mode { get; set; } = "sequential";
    /// <summary>有向图 JSON，描述成员依赖与并行关系。</summary>
    [Column("graph_json")] public string GraphJson { get; set; } = "{}";
    /// <summary>审批策略，如"manual_confirmation""auto_execute"等。</summary>
    [Column("approval_policy")] public string ApprovalPolicy { get; set; } = "manual_confirmation";
    /// <summary>创建时间（UTC）。</summary>
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    /// <summary>更新时间（UTC）。</summary>
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
    /// <summary>乐观锁版本号。</summary>
    [Column("row_version")] public long RowVersion { get; set; } = 1;
    /// <summary>同步版本号。</summary>
    [Column("sync_version")] public long SyncVersion { get; set; } = 1;
}

/// <summary>团队运行模板的发布版本，发布后不可变。</summary>
[Table("team_run_template_versions")]
public sealed class TeamRunTemplateVersion
{
    /// <summary>版本主键。</summary>
    [Key, Column("id")] public long Id { get; set; }
    /// <summary>所属模板主键。</summary>
    [Column("team_run_template_id")] public long TeamRunTemplateId { get; set; }
    /// <summary>所属租户标识。</summary>
    [Column("tenant_id")] public long TenantId { get; set; }
    /// <summary>从 1 起的版本号。</summary>
    [Column("version")] public int Version { get; set; }
    /// <summary>成员快照 JSON，包含 expertVersionId、displayName、stageOrder 等。</summary>
    [Column("members_json")] public string MembersJson { get; set; } = "[]";
    /// <summary>文件引用快照 JSON。</summary>
    [Column("file_refs_json")] public string FileRefsJson { get; set; } = "[]";
    /// <summary>成员权限交集 JSON。</summary>
    [Column("permission_intersections_json")] public string PermissionIntersectionsJson { get; set; } = "{}";
    /// <summary>有向图快照 JSON。</summary>
    [Column("graph_json")] public string GraphJson { get; set; } = "{}";
    /// <summary>创建时间（UTC）。</summary>
    [Column("created_at")] public DateTime CreatedAt { get; set; }
}

/// <summary>团队运行实例主表。</summary>
[Table("team_runs")]
public sealed class TeamRun
{
    /// <summary>团队运行主键。</summary>
    [Key, Column("id")] public long Id { get; set; }
    /// <summary>所属租户标识。</summary>
    [Column("tenant_id")] public long TenantId { get; set; }
    /// <summary>触发团队运行的父 AgentRun 主键。</summary>
    [Column("parent_agent_run_id")] public long ParentAgentRunId { get; set; }
    /// <summary>团队运行模板主键。</summary>
    [Column("team_run_template_id")] public long TeamRunTemplateId { get; set; }
    /// <summary>冻结的模板版本主键。</summary>
    [Column("team_run_template_version_id")] public long TeamRunTemplateVersionId { get; set; }
    /// <summary>团队协议版本号，V1 时为 1。</summary>
    [Column("team_version")] public int TeamVersion { get; set; }
    /// <summary>运行模式。</summary>
    [Column("mode")] public string Mode { get; set; } = "sequential";
    /// <summary>运行状态，取值参见 <see cref="TeamRunStatus"/>。</summary>
    [Column("status")] public string Status { get; set; } = "pending";
    /// <summary>合成结果 JSON。</summary>
    [Column("synthesis_result_json")] public string? SynthesisResultJson { get; set; }
    /// <summary>最近一次失败的错误码。</summary>
    [Column("last_error_code")] public string? LastErrorCode { get; set; }
    /// <summary>创建时间（UTC）。</summary>
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    /// <summary>更新时间（UTC）。</summary>
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
    /// <summary>乐观锁版本号。</summary>
    [Column("row_version")] public long RowVersion { get; set; } = 1;
    /// <summary>同步版本号。</summary>
    [Column("sync_version")] public long SyncVersion { get; set; } = 1;
}

/// <summary>团队运行成员快照，保存冻结后的成员配置与子运行。</summary>
[Table("team_run_members")]
public sealed class TeamRunMember
{
    /// <summary>成员主键。</summary>
    [Key, Column("id")] public long Id { get; set; }
    /// <summary>所属租户标识。</summary>
    [Column("tenant_id")] public long TenantId { get; set; }
    /// <summary>所属团队运行主键。</summary>
    [Column("team_run_id")] public long TeamRunId { get; set; }
    /// <summary>冻结的专家版本主键。</summary>
    [Column("expert_version_id")] public long ExpertVersionId { get; set; }
    /// <summary>成员子 AgentRun 主键，调度时写入。</summary>
    [Column("child_agent_run_id")] public long? ChildAgentRunId { get; set; }
    /// <summary>成员显示名，用于前端展示。</summary>
    [Column("display_name")] public string DisplayName { get; set; } = null!;
    /// <summary>阶段序号，决定执行顺序。</summary>
    [Column("stage_order")] public int StageOrder { get; set; }
    /// <summary>权限交集 JSON。</summary>
    [Column("permission_intersection_json")] public string PermissionIntersectionJson { get; set; } = "{}";
    /// <summary>成员状态，取值参见 <see cref="TeamRunMemberStatus"/>。</summary>
    [Column("status")] public string Status { get; set; } = "pending";
    /// <summary>成员最近一次失败的错误码。</summary>
    [Column("last_error_code")] public string? LastErrorCode { get; set; }
    /// <summary>创建时间（UTC）。</summary>
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    /// <summary>更新时间（UTC）。</summary>
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
}

/// <summary>团队运行相关审计记录，写入文件、附件、取消、重试等动作。</summary>
[Table("team_run_audits")]
public sealed class TeamRunAudit
{
    /// <summary>审计主键。</summary>
    [Key, Column("id")] public long Id { get; set; }
    /// <summary>所属租户标识。</summary>
    [Column("tenant_id")] public long TenantId { get; set; }
    /// <summary>操作者用户标识，系统动作可为空。</summary>
    [Column("actor_user_id")] public long? ActorUserId { get; set; }
    /// <summary>关联的团队运行主键。</summary>
    [Column("team_run_id")] public long? TeamRunId { get; set; }
    /// <summary>关联的文件主键。</summary>
    [Column("expert_file_id")] public long? ExpertFileId { get; set; }
    /// <summary>关联的成员主键。</summary>
    [Column("team_run_member_id")] public long? TeamRunMemberId { get; set; }
    /// <summary>动作类型，如"file_attach""cancel""retry"等。</summary>
    [Column("action")] public string Action { get; set; } = null!;
    /// <summary>动作结果，如"success""failed"等。</summary>
    [Column("result")] public string Result { get; set; } = "success";
    /// <summary>错误码，可为空。</summary>
    [Column("error_code")] public string? ErrorCode { get; set; }
    /// <summary>附加负载 JSON，展示安全。</summary>
    [Column("payload_json")] public string? PayloadJson { get; set; }
    /// <summary>创建时间（UTC）。</summary>
    [Column("created_at")] public DateTime CreatedAt { get; set; }
}

/// <summary>每日知识条目表，供知识管家取用：本地预置（如芸和中医）+ 用户主动传入。</summary>
[Table("knowledge_items")]
public sealed class KnowledgeItem
{
    /// <summary>条目主键。</summary>
    [Key, Column("id")] public long Id { get; set; }
    /// <summary>所属租户标识。</summary>
    [Column("tenant_id")] public long TenantId { get; set; }
    /// <summary>知识分类，如 yunhe_tcm / management / general。</summary>
    [Column("category", TypeName = "varchar(32)")] public string Category { get; set; } = null!;
    /// <summary>知识标题。</summary>
    [Column("title", TypeName = "varchar(128)")] public string Title { get; set; } = null!;
    /// <summary>知识正文。</summary>
    [Column("content", TypeName = "text")] public string Content { get; set; } = null!;
    /// <summary>来源说明，可为空。</summary>
    [Column("source", TypeName = "varchar(128)")] public string? Source { get; set; }
    /// <summary>是否启用；停用后不再被知识管家取用。</summary>
    [Column("is_active")] public bool IsActive { get; set; } = true;
    /// <summary>创建者用户标识，系统预置为空。</summary>
    [Column("created_by_user_id")] public long? CreatedByUserId { get; set; }
    /// <summary>创建时间（UTC）。</summary>
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    /// <summary>最近修改时间（UTC）。</summary>
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
}

/// <summary>专家文件状态常量集合。</summary>
public static class ExpertFileStatus
{
    /// <summary>等待上传，尚未提交任何对象。</summary>
    public const string PendingUpload = "pending_upload";
    /// <summary>扫描中。</summary>
    public const string Scanning = "scanning";
    /// <summary>已就绪，可被附件或读取。</summary>
    public const string Ready = "ready";
    /// <summary>扫描或校验拒绝。</summary>
    public const string Rejected = "rejected";
    /// <summary>已软删除。</summary>
    public const string Deleted = "deleted";
}

/// <summary>团队运行模式常量集合。</summary>
public static class TeamRunMode
{
    /// <summary>顺序执行，前置成员完成后再启动后续成员。</summary>
    public const string Sequential = "sequential";
    /// <summary>并行执行，成员间无依赖关系。</summary>
    public const string Parallel = "parallel";
    /// <summary>由队长进行综合合成。</summary>
    public const string Synthesis = "synthesis";
}

/// <summary>团队运行状态常量集合。</summary>
public static class TeamRunStatus
{
    /// <summary>已创建尚未开始。</summary>
    public const string Pending = "pending";
    /// <summary>正在执行。</summary>
    public const string Running = "running";
    /// <summary>已完成。</summary>
    public const string Completed = "completed";
    /// <summary>失败终止。</summary>
    public const string Failed = "failed";
    /// <summary>已取消。</summary>
    public const string Cancelled = "cancelled";
}

/// <summary>团队成员状态常量集合。</summary>
public static class TeamRunMemberStatus
{
    /// <summary>待执行。</summary>
    public const string Pending = "pending";
    /// <summary>执行中。</summary>
    public const string Running = "running";
    /// <summary>执行成功。</summary>
    public const string Completed = "completed";
    /// <summary>执行失败。</summary>
    public const string Failed = "failed";
    /// <summary>已取消。</summary>
    public const string Cancelled = "cancelled";
    /// <summary>被跳过（如合成模式下不参与执行）。</summary>
    public const string Skipped = "skipped";
}

/// <summary>平台级 Skill 目录表，声明 Skill 的输入/输出契约、所需权限与风险等级。</summary>
/// <remarks>tenant_id 固定为 1（平台级，同 <c>scenario_templates</c> 惯例）；运行经
/// <c>POST /api/v1/skills/&#123;skillCode&#125;/runs</c> 创建 SourceType=skill 的 AgentRun。
/// 与 <see cref="AiSkill"/>（ai_skills 用户自定义技能）语义分离。</remarks>
[Table("skills")]
public sealed class SkillCatalog
{
    /// <summary>Skill 主键。</summary>
    [Key, Column("id")] public long Id { get; set; }
    /// <summary>所属租户标识，平台级固定 1。</summary>
    [Column("tenant_id")] public long TenantId { get; set; }
    /// <summary>Skill 业务键，全局唯一，路由 skillCode 即此字段。</summary>
    [Column("key")] public string Key { get; set; } = null!;
    /// <summary>Skill 对外展示名称。</summary>
    [Column("name")] public string Name { get; set; } = null!;
    /// <summary>Skill 分类，如 media。</summary>
    [Column("category")] public string Category { get; set; } = null!;
    /// <summary>Skill 描述，Swagger 展示与运行期说明使用。</summary>
    [Column("description")] public string? Description { get; set; }
    /// <summary>输入契约 JSON Schema，运行创建时校验输入参数。</summary>
    [Column("input_schema_json")] public string InputSchema { get; set; } = null!;
    /// <summary>输出契约 JSON Schema，可空。</summary>
    [Column("output_schema_json")] public string? OutputSchema { get; set; }
    /// <summary>调用该 Skill 所需的最小权限，如 media.read。</summary>
    [Column("required_permission")] public string RequiredPermission { get; set; } = null!;
    /// <summary>风险等级，取值参见 <see cref="ConfirmationRiskLevel"/>；快速剪辑为 L1。</summary>
    [Column("risk_level")] public string RiskLevel { get; set; } = "L1";
    /// <summary>Skill 状态，取值参见 <see cref="SkillCatalogStatus"/>。</summary>
    [Column("status")] public string Status { get; set; } = "active";
    /// <summary>记录创建时间（UTC）。</summary>
    [Column("created_at", TypeName = "datetime(3)")] public DateTime CreatedAt { get; set; }
    /// <summary>记录最近一次修改时间（UTC）。</summary>
    [Column("updated_at", TypeName = "datetime(3)")] public DateTime UpdatedAt { get; set; }
    /// <summary>软删除时间（UTC），非空表示已删除；已删除 Skill 不再可发起运行。</summary>
    [Column("deleted_at", TypeName = "datetime(3)")] public DateTime? DeletedAt { get; set; }
    /// <summary>行版本号，乐观锁比较字段。</summary>
    [ConcurrencyCheck, Column("row_version")] public long RowVersion { get; set; } = 1;
    /// <summary>行版本号，用于同步冲突检测。</summary>
    [Column("sync_version")] public long SyncVersion { get; set; }
}

/// <summary>平台级 Skill 目录状态常量集合。</summary>
public static class SkillCatalogStatus
{
    /// <summary>启用，可发起运行。</summary>
    public const string Active = "active";
    /// <summary>停用，运行发起返回 422。</summary>
    public const string Inactive = "inactive";
}

/// <summary>快速剪辑素材登记表：浏览器上传落盘或路径模式登记的输入文件，ffprobe 提取时长/分辨率/帧率元数据。</summary>
/// <remarks>素材仅登记服务端可访问路径（上传落盘或路径模式校验目录内），供剪辑 MCP 访问；
/// 上传返回 <see cref="StoragePath"/> 由前端回填 Skill 输入 <c>media_location</c>（B24 契约零改动）。
/// 与 <see cref="ExpertFile"/>（生成文件）语义分离：素材是输入文件，归上传者本人可见可删。</remarks>
[Table("clipping_materials")]
public sealed class ClippingMaterial
{
    /// <summary>素材主键。</summary>
    [Key, Column("id")] public long Id { get; set; }
    /// <summary>所属租户标识。</summary>
    [Column("tenant_id")] public long TenantId { get; set; }
    /// <summary>上传者用户标识，素材仅本人可见可删。</summary>
    [Column("owner_user_id")] public long OwnerUserId { get; set; }
    /// <summary>素材文件名（含扩展名）。</summary>
    [Column("file_name", TypeName = "varchar(255)")] public string FileName { get; set; } = null!;
    /// <summary>服务端可访问的素材路径（上传落盘路径或路径模式登记路径）。</summary>
    [Column("storage_path", TypeName = "varchar(1024)")] public string StoragePath { get; set; } = null!;
    /// <summary>素材 MIME 类型，路径模式登记可能为空。</summary>
    [Column("content_type", TypeName = "varchar(128)")] public string? ContentType { get; set; }
    /// <summary>文件大小（字节）。</summary>
    [Column("file_size")] public long FileSize { get; set; }
    /// <summary>时长（秒），ffprobe 提取；解析失败为空。</summary>
    [Column("duration_seconds")] public int? DurationSeconds { get; set; }
    /// <summary>画面宽度（像素），ffprobe 提取；解析失败为空。</summary>
    [Column("width")] public int? Width { get; set; }
    /// <summary>画面高度（像素），ffprobe 提取；解析失败为空。</summary>
    [Column("height")] public int? Height { get; set; }
    /// <summary>帧率（fps），ffprobe 提取；解析失败为空。</summary>
    [Column("fps")] public double? Fps { get; set; }
    /// <summary>素材状态，取值参见 <see cref="ClippingMaterialStatus"/>。</summary>
    [Column("status", TypeName = "varchar(16)")] public string Status { get; set; } = "active";
    /// <summary>软删除标记，true 表示已删除。</summary>
    [Column("is_deleted")] public bool IsDeleted { get; set; }
    /// <summary>记录创建时间（UTC）。</summary>
    [Column("created_at", TypeName = "datetime(3)")] public DateTime CreatedAt { get; set; }
    /// <summary>记录最近一次修改时间（UTC）。</summary>
    [Column("updated_at", TypeName = "datetime(3)")] public DateTime UpdatedAt { get; set; }
}

/// <summary>快速剪辑素材状态常量集合。</summary>
public static class ClippingMaterialStatus
{
    /// <summary>启用，可被 Skill 输入引用。</summary>
    public const string Active = "active";
    /// <summary>已删除（软删除）。</summary>
    public const string Deleted = "deleted";
}
