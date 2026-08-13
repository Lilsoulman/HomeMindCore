using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HomeMind.Common.Model.Entities.SmartHome;

/// <summary>连接器提供方目录，登记可被租户接入的厂商类型。</summary>
[Table("connector_providers")]
public sealed class ConnectorProvider
{
    /// <summary>提供方主键。</summary>
    [Key, Column("id")] public long Id { get; set; }
    /// <summary>提供方业务编码，全局唯一。</summary>
    [Column("code")] public string Code { get; set; } = null!;
    /// <summary>提供方对外展示名。</summary>
    [Column("name")] public string Name { get; set; } = null!;
    /// <summary>底层实现供应商（如"home_assistant""mqtt"等）。</summary>
    [Column("provider")] public string Provider { get; set; } = null!;
    /// <summary>连接器类型，如"smart_home""calendar"等。</summary>
    [Column("connector_type")] public string ConnectorType { get; set; } = null!;
    /// <summary>提供方状态，参见 <see cref="ConnectorProviderStatus"/>。</summary>
    [Column("status")] public string Status { get; set; } = "active";
    /// <summary>提供方描述。</summary>
    [Column("description")] public string? Description { get; set; }
    /// <summary>创建时间（UTC）。</summary>
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    /// <summary>更新时间（UTC）。</summary>
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
    /// <summary>软删除时间戳。</summary>
    [Column("deleted_at")] public DateTime? DeletedAt { get; set; }
    /// <summary>同步版本号。</summary>
    [Column("sync_version")] public long SyncVersion { get; set; }
}

/// <summary>工作区连接器，租户实际接入的连接器实例。</summary>
[Table("workspace_connectors")]
public sealed class WorkspaceConnector
{
    /// <summary>连接器实例主键。</summary>
    [Key, Column("id")] public long Id { get; set; }
    /// <summary>所属租户标识。</summary>
    [Column("tenant_id")] public long TenantId { get; set; }
    /// <summary>所用提供方主键。</summary>
    [Column("connector_provider_id")] public long ConnectorProviderId { get; set; }
    /// <summary>绑定范围：household 为家庭共享实例，personal 为成员个人实例。</summary>
    [Column("binding_scope")] public string BindingScope { get; set; } = "household";
    /// <summary>个人实例所有者用户主键，家庭实例必须为空。</summary>
    [Column("owner_user_id")] public long? OwnerUserId { get; set; }
    /// <summary>租户侧自定义的连接器名称。</summary>
    [Column("name")] public string Name { get; set; } = null!;
    /// <summary>凭据引用，格式为 vault://tenants/{tenantId}/...，API 不返回明文。</summary>
    [Column("credential_ref")] public string? CredentialRef { get; set; }
    /// <summary>连接器状态，参见 <see cref="WorkspaceConnectorStatus"/>。</summary>
    [Column("status")] public string Status { get; set; } = "disconnected";
    /// <summary>授权生命周期状态，参见 <see cref="WorkspaceConnectorAuthStatus"/>。</summary>
    [Column("auth_status")] public string AuthStatus { get; set; } = "none";
    /// <summary>非敏感配置 JSON；凭据只存 <see cref="CredentialRef"/> 指向密钥服务。</summary>
    [Column("config")] public string? Config { get; set; }
    /// <summary>最近一次同步时间（UTC）。</summary>
    [Column("last_sync_at")] public DateTime? LastSyncAt { get; set; }
    /// <summary>最近一次健康探测时间（UTC）。</summary>
    [Column("last_health_at")] public DateTime? LastHealthAt { get; set; }
    /// <summary>创建时间（UTC）。</summary>
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    /// <summary>更新时间（UTC）。</summary>
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
    /// <summary>软删除时间戳。</summary>
    [Column("deleted_at")] public DateTime? DeletedAt { get; set; }
    /// <summary>同步版本号。</summary>
    [Column("sync_version")] public long SyncVersion { get; set; }
}


/// <summary>用户对连接器的范围授权。</summary>
[Table("user_connector_authorizations")]
public sealed class UserConnectorAuthorization
{
    /// <summary>授权主键。</summary>
    [Key, Column("id")] public long Id { get; set; }
    /// <summary>所属租户标识。</summary>
    [Column("tenant_id")] public long TenantId { get; set; }
    /// <summary>被授权用户主键。</summary>
    [Column("user_id")] public long UserId { get; set; }
    /// <summary>工作区连接器主键。</summary>
    [Column("workspace_connector_id")] public long WorkspaceConnectorId { get; set; }
    /// <summary>授权范围 JSON 字符串。</summary>
    [Column("scope_json")] public string Scope { get; set; } = "[]";
    /// <summary>创建时间（UTC）。</summary>
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    /// <summary>更新时间（UTC）。</summary>
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
    /// <summary>软删除时间戳。</summary>
    [Column("deleted_at")] public DateTime? DeletedAt { get; set; }
    /// <summary>同步版本号。</summary>
    [Column("sync_version")] public long SyncVersion { get; set; }
}

/// <summary>连接器授权生命周期状态集合。</summary>
public static class WorkspaceConnectorAuthStatus
{
    /// <summary>未发起授权。</summary>
    public const string None = "none";
    /// <summary>授权会话进行中。</summary>
    public const string Authorizing = "authorizing";
    /// <summary>授权完成，凭据可用。</summary>
    public const string Connected = "connected";
    /// <summary>授权已撤销，凭据不可用。</summary>
    public const string Revoked = "revoked";
    /// <summary>授权流程失败。</summary>
    public const string Failed = "failed";
}

/// <summary>连接器授权会话，承载个人 OAuth 或受控家庭授权的短期服务端会话；单次使用且过期。</summary>
/// <remarks>仅保存 <c>state</c> 的哈希与 PKCE 校验器引用，不保存授权 code、访问令牌或刷新令牌。</remarks>
[Table("connector_authorization_sessions")]
public sealed class ConnectorAuthorizationSession
{
    /// <summary>会话主键。</summary>
    [Key, Column("id")] public long Id { get; set; }
    /// <summary>所属租户标识。</summary>
    [Column("tenant_id")] public long TenantId { get; set; }
    /// <summary>所授权连接器提供方主键。</summary>
    [Column("connector_provider_id")] public long ConnectorProviderId { get; set; }
    /// <summary>绑定范围，当前实现固定为 personal。</summary>
    [Column("binding_scope")] public string BindingScope { get; set; } = "personal";
    /// <summary>发起授权的用户主键。</summary>
    [Column("initiator_user_id")] public long InitiatorUserId { get; set; }
    /// <summary>一次性 state 的 SHA-256 十六进制哈希，回调时校验，使用后失效。</summary>
    [Column("state_hash")] public string StateHash { get; set; } = null!;
    /// <summary>PKCE 校验器引用，格式为 vault://tenants/{tenantId}/...，API 不返回明文。</summary>
    [Column("pkce_verifier_ref")] public string? PkceVerifierRef { get; set; }
    /// <summary>回调跳转地址，必须命中 Provider 预注册白名单。</summary>
    [Column("redirect_uri")] public string RedirectUri { get; set; } = null!;
    /// <summary>会话状态，参见 <see cref="ConnectorAuthorizationSessionStatus"/>。</summary>
    [Column("status")] public string Status { get; set; } = "pending";
    /// <summary>会话过期时间（UTC），过期后回调拒绝。</summary>
    [Column("expires_at")] public DateTime ExpiresAt { get; set; }
    /// <summary>会话完成时间（UTC），成功回调或撤销时写入。</summary>
    [Column("completed_at")] public DateTime? CompletedAt { get; set; }
    /// <summary>创建时间（UTC）。</summary>
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    /// <summary>更新时间（UTC）。</summary>
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
}

/// <summary>连接器授权会话状态集合。</summary>
public static class ConnectorAuthorizationSessionStatus
{
    /// <summary>已创建，等待 Provider 回调。</summary>
    public const string Pending = "pending";
    /// <summary>state 已被消费，防止重放。</summary>
    public const string Used = "used";
    /// <summary>会话已过期。</summary>
    public const string Expired = "expired";
    /// <summary>会话被撤销。</summary>
    public const string Revoked = "revoked";
    /// <summary>回调处理完成，凭据已落库。</summary>
    public const string Completed = "completed";
    /// <summary>回调处理失败。</summary>
    public const string Failed = "failed";
}

/// <summary>智能家居空间（如客厅、卧室）的归一化视图。</summary>
[Table("smart_home_spaces")]
public sealed class SmartHomeSpace
{
    /// <summary>空间主键。</summary>
    [Key, Column("id")] public long Id { get; set; }
    /// <summary>所属租户标识。</summary>
    [Column("tenant_id")] public long TenantId { get; set; }
    /// <summary>空间名称。</summary>
    [Column("name")] public string Name { get; set; } = null!;
    /// <summary>空间类型，例如"living_room""bedroom"等。</summary>
    [Column("space_type")] public string SpaceType { get; set; } = null!;
    /// <summary>空间摘要，便于前端列表展示。</summary>
    [Column("summary")] public string? Summary { get; set; }
    /// <summary>前端排序值，越小越靠前。</summary>
    [Column("sort_order")] public int SortOrder { get; set; }
    /// <summary>创建时间（UTC）。</summary>
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    /// <summary>更新时间（UTC）。</summary>
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
    /// <summary>软删除时间戳。</summary>
    [Column("deleted_at")] public DateTime? DeletedAt { get; set; }
    /// <summary>同步版本号。</summary>
    [Column("sync_version")] public long SyncVersion { get; set; }
}

/// <summary>智能家居设备归一化实体，兼容多种底层协议。</summary>
[Table("smart_home_devices")]
public sealed class SmartHomeDevice
{
    /// <summary>设备主键。</summary>
    [Key, Column("id")] public long Id { get; set; }
    /// <summary>所属租户标识。</summary>
    [Column("tenant_id")] public long TenantId { get; set; }
    /// <summary>所属工作区连接器主键，平台设备可为空。</summary>
    [Column("workspace_connector_id")] public long? WorkspaceConnectorId { get; set; }
    /// <summary>所属空间主键，可为空表示未分配空间。</summary>
    [Column("space_id")] public long? SpaceId { get; set; }
    /// <summary>底层厂商实体 ID，不对外返回。</summary>
    [Column("external_id")] public string? ExternalId { get; set; }
    /// <summary>设备对外展示名。</summary>
    [Column("name")] public string Name { get; set; } = null!;
    /// <summary>设备类型，例如"light""switch""sensor"等。</summary>
    [Column("device_type")] public string DeviceType { get; set; } = null!;
    /// <summary>在线状态，参见 <see cref="DeviceOnlineStatus"/>。</summary>
    [Column("online_status")] public string OnlineStatus { get; set; } = "unknown";
    /// <summary>归一化 Zigbee 角色，例如"router""end_device"等。</summary>
    [Column("zigbee_role")] public string? ZigbeeRole { get; set; }
    /// <summary>电池电量百分比，0-100，无电池设备为空。</summary>
    [Column("battery_level")] public byte? BatteryLevel { get; set; }
    /// <summary>信号 LQI 值，数值越大信号越好。</summary>
    [Column("signal_lqi")] public int? SignalLqi { get; set; }
    /// <summary>健康状态，参见 <see cref="DeviceHealthStatus"/>。</summary>
    [Column("health_status")] public string HealthStatus { get; set; } = "healthy";
    /// <summary>状态摘要，便于列表展示。</summary>
    [Column("state_summary")] public string? StateSummary { get; set; }
    /// <summary>最近一次上报时间（UTC）。</summary>
    [Column("last_seen_at")] public DateTime? LastSeenAt { get; set; }
    /// <summary>创建时间（UTC）。</summary>
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    /// <summary>更新时间（UTC）。</summary>
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
    /// <summary>软删除时间戳。</summary>
    [Column("deleted_at")] public DateTime? DeletedAt { get; set; }
    /// <summary>同步版本号。</summary>
    [Column("sync_version")] public long SyncVersion { get; set; }
}

/// <summary>设备能力声明，决定可调用与可读取的字段。</summary>
[Table("device_capabilities")]
public sealed class DeviceCapability
{
    /// <summary>能力主键。</summary>
    [Key, Column("id")] public long Id { get; set; }
    /// <summary>所属设备主键。</summary>
    [Column("device_id")] public long DeviceId { get; set; }
    /// <summary>能力名，例如"on_off""brightness"等。</summary>
    [Column("capability")] public string Capability { get; set; } = null!;
    /// <summary>能力取值 JSON Schema 字符串。</summary>
    [Column("value_schema_json")] public string ValueSchema { get; set; } = "{}";
    /// <summary>所需权限名，需与用户授权范围匹配。</summary>
    [Column("permission")] public string Permission { get; set; } = null!;
    /// <summary>是否可写。</summary>
    [Column("is_writable")] public bool IsWritable { get; set; }
    /// <summary>创建时间（UTC）。</summary>
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    /// <summary>更新时间（UTC）。</summary>
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
    /// <summary>软删除时间戳。</summary>
    [Column("deleted_at")] public DateTime? DeletedAt { get; set; }
    /// <summary>同步版本号。</summary>
    [Column("sync_version")] public long SyncVersion { get; set; }
}

/// <summary>设备状态采样，按时间倒序保留最近若干条。</summary>
[Table("device_states")]
public sealed class DeviceState
{
    /// <summary>状态主键。</summary>
    [Key, Column("id")] public long Id { get; set; }
    /// <summary>所属设备主键。</summary>
    [Column("device_id")] public long DeviceId { get; set; }
    /// <summary>设备状态 JSON 字符串。</summary>
    [Column("state_json")] public string State { get; set; } = "{}";
    /// <summary>采样时间（UTC）。</summary>
    [Column("sampled_at")] public DateTime SampledAt { get; set; }
    /// <summary>入库时间（UTC）。</summary>
    [Column("created_at")] public DateTime CreatedAt { get; set; }
}

/// <summary>场景定义，由若干设备动作组合。</summary>
[Table("scenes")]
public sealed class Scene
{
    /// <summary>场景主键。</summary>
    [Key, Column("id")] public long Id { get; set; }
    /// <summary>所属租户标识。</summary>
    [Column("tenant_id")] public long TenantId { get; set; }
    /// <summary>场景业务键（用于路由与快捷方式）。</summary>
    [Column("scene_key")] public string SceneKey { get; set; } = null!;
    /// <summary>场景对外展示名。</summary>
    [Column("name")] public string Name { get; set; } = null!;
    /// <summary>场景摘要。</summary>
    [Column("summary")] public string? Summary { get; set; }
    /// <summary>场景状态，参见 <see cref="SceneStatus"/>。</summary>
    [Column("status")] public string Status { get; set; } = "active";
    /// <summary>创建时间（UTC）。</summary>
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    /// <summary>更新时间（UTC）。</summary>
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
    /// <summary>软删除时间戳。</summary>
    [Column("deleted_at")] public DateTime? DeletedAt { get; set; }
    /// <summary>同步版本号。</summary>
    [Column("sync_version")] public long SyncVersion { get; set; }
}

/// <summary>场景动作，描述对设备能力的赋值。</summary>
[Table("scene_actions")]
public sealed class SceneAction
{
    /// <summary>动作主键。</summary>
    [Key, Column("id")] public long Id { get; set; }
    /// <summary>所属场景主键。</summary>
    [Column("scene_id")] public long SceneId { get; set; }
    /// <summary>目标设备主键。</summary>
    [Column("device_id")] public long DeviceId { get; set; }
    /// <summary>目标能力名。</summary>
    [Column("capability")] public string Capability { get; set; } = null!;
    /// <summary>目标值 JSON 字符串。</summary>
    [Column("target_value_json")] public string TargetValue { get; set; } = "{}";
    /// <summary>执行顺序，数值小的先执行。</summary>
    [Column("sort_order")] public int SortOrder { get; set; }
    /// <summary>创建时间（UTC）。</summary>
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    /// <summary>更新时间（UTC）。</summary>
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
}

/// <summary>自动化规则实体，租户隔离的长期运行触发器。</summary>
[Table("automation_rules")]
public sealed class AutomationRule
{
    /// <summary>规则主键。</summary>
    [Key, Column("id")] public long Id { get; set; }
    /// <summary>所属租户标识。</summary>
    [Column("tenant_id")] public long TenantId { get; set; }
    /// <summary>规则所有者用户标识。</summary>
    [Column("owner_user_id")] public long OwnerUserId { get; set; }
    /// <summary>规则名称。</summary>
    [Column("name")] public string Name { get; set; } = null!;
    /// <summary>触发类型，参见 <see cref="AutomationTriggerType"/>。</summary>
    [Column("trigger_type")] public string TriggerType { get; set; } = null!;
    /// <summary>触发配置 JSON 字符串。</summary>
    [Column("trigger_config_json")] public string TriggerConfig { get; set; } = "{}";
    /// <summary>额外条件 JSON 数组。</summary>
    [Column("conditions_json")] public string Conditions { get; set; } = "[]";
    /// <summary>动作列表 JSON 数组，限制为内置场景键。</summary>
    [Column("actions_json")] public string Actions { get; set; } = "[]";
    /// <summary>审批策略，参见 <see cref="AutomationApprovalPolicy"/>。</summary>
    [Column("approval_policy")] public string ApprovalPolicy { get; set; } = "manual_confirmation";
    /// <summary>是否启用规则。</summary>
    [Column("enabled")] public bool Enabled { get; set; } = true;
    /// <summary>最近一次触发时间（UTC）。</summary>
    [Column("last_triggered_at")] public DateTime? LastTriggeredAt { get; set; }
    /// <summary>创建时间（UTC）。</summary>
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    /// <summary>更新时间（UTC）。</summary>
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
    /// <summary>乐观锁版本号。</summary>
    [Column("row_version")] public long RowVersion { get; set; }
    /// <summary>同步版本号。</summary>
    [Column("sync_version")] public long SyncVersion { get; set; }
}

/// <summary>连接器同步任务，承载后台重试与重排队。</summary>
[Table("connector_sync_jobs")]
public sealed class ConnectorSyncJob
{
    /// <summary>同步任务主键。</summary>
    [Key, Column("id")] public long Id { get; set; }
    /// <summary>所属租户标识。</summary>
    [Column("tenant_id")] public long TenantId { get; set; }
    /// <summary>所属工作区连接器主键。</summary>
    [Column("workspace_connector_id")] public long WorkspaceConnectorId { get; set; }
    /// <summary>任务状态，参见 <see cref="ConnectorSyncJobStatus"/>。</summary>
    [Column("status")] public string Status { get; set; } = "queued";
    /// <summary>任务触发原因，如"manual""scheduled"等。</summary>
    [Column("reason")] public string Reason { get; set; } = "manual";
    /// <summary>当前重试次数（首次为 1）。</summary>
    [Column("attempt_no")] public int AttemptNo { get; set; }
    /// <summary>任务可被拉取的最早时间（UTC）。</summary>
    [Column("available_at")] public DateTime AvailableAt { get; set; }
    /// <summary>实际开始时间（UTC）。</summary>
    [Column("started_at")] public DateTime? StartedAt { get; set; }
    /// <summary>完成时间（UTC），包括失败完成。</summary>
    [Column("completed_at")] public DateTime? CompletedAt { get; set; }
    /// <summary>最近一次失败的错误码。</summary>
    [Column("last_error_code")] public string? LastErrorCode { get; set; }
    /// <summary>幂等键，避免重复入队。</summary>
    [Column("idempotency_key")] public string IdempotencyKey { get; set; } = null!;
    /// <summary>创建时间（UTC）。</summary>
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    /// <summary>更新时间（UTC）。</summary>
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
    /// <summary>同步版本号。</summary>
    [Column("sync_version")] public long SyncVersion { get; set; }
}
