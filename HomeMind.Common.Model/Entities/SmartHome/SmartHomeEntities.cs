using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HomeMind.Common.Model.Entities.SmartHome;

[Table("connector_providers")]
public sealed class ConnectorProvider
{
    [Key, Column("id")] public long Id { get; set; }
    [Column("code")] public string Code { get; set; } = null!;
    [Column("name")] public string Name { get; set; } = null!;
    [Column("provider")] public string Provider { get; set; } = null!;
    [Column("connector_type")] public string ConnectorType { get; set; } = null!;
    [Column("status")] public string Status { get; set; } = "active";
    [Column("description")] public string? Description { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
    [Column("deleted_at")] public DateTime? DeletedAt { get; set; }
    [Column("sync_version")] public long SyncVersion { get; set; }
}

[Table("workspace_connectors")]
public sealed class WorkspaceConnector
{
    [Key, Column("id")] public long Id { get; set; }
    [Column("tenant_id")] public long TenantId { get; set; }
    [Column("connector_provider_id")] public long ConnectorProviderId { get; set; }
    [Column("name")] public string Name { get; set; } = null!;
    [Column("credential_ref")] public string? CredentialRef { get; set; }
    [Column("status")] public string Status { get; set; } = "disconnected";
    [Column("last_sync_at")] public DateTime? LastSyncAt { get; set; }
    [Column("last_health_at")] public DateTime? LastHealthAt { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
    [Column("deleted_at")] public DateTime? DeletedAt { get; set; }
    [Column("sync_version")] public long SyncVersion { get; set; }
}

[Table("user_connector_authorizations")]
public sealed class UserConnectorAuthorization
{
    [Key, Column("id")] public long Id { get; set; }
    [Column("tenant_id")] public long TenantId { get; set; }
    [Column("user_id")] public long UserId { get; set; }
    [Column("workspace_connector_id")] public long WorkspaceConnectorId { get; set; }
    [Column("scope_json")] public string Scope { get; set; } = "[]";
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
    [Column("deleted_at")] public DateTime? DeletedAt { get; set; }
    [Column("sync_version")] public long SyncVersion { get; set; }
}

[Table("smart_home_spaces")]
public sealed class SmartHomeSpace
{
    [Key, Column("id")] public long Id { get; set; }
    [Column("tenant_id")] public long TenantId { get; set; }
    [Column("name")] public string Name { get; set; } = null!;
    [Column("space_type")] public string SpaceType { get; set; } = null!;
    [Column("summary")] public string? Summary { get; set; }
    [Column("sort_order")] public int SortOrder { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
    [Column("deleted_at")] public DateTime? DeletedAt { get; set; }
    [Column("sync_version")] public long SyncVersion { get; set; }
}

[Table("smart_home_devices")]
public sealed class SmartHomeDevice
{
    [Key, Column("id")] public long Id { get; set; }
    [Column("tenant_id")] public long TenantId { get; set; }
    [Column("workspace_connector_id")] public long? WorkspaceConnectorId { get; set; }
    [Column("space_id")] public long? SpaceId { get; set; }
    [Column("external_id")] public string? ExternalId { get; set; }
    [Column("name")] public string Name { get; set; } = null!;
    [Column("device_type")] public string DeviceType { get; set; } = null!;
    [Column("online_status")] public string OnlineStatus { get; set; } = "unknown";
    [Column("state_summary")] public string? StateSummary { get; set; }
    [Column("last_seen_at")] public DateTime? LastSeenAt { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
    [Column("deleted_at")] public DateTime? DeletedAt { get; set; }
    [Column("sync_version")] public long SyncVersion { get; set; }
}

[Table("device_capabilities")]
public sealed class DeviceCapability
{
    [Key, Column("id")] public long Id { get; set; }
    [Column("device_id")] public long DeviceId { get; set; }
    [Column("capability")] public string Capability { get; set; } = null!;
    [Column("value_schema_json")] public string ValueSchema { get; set; } = "{}";
    [Column("permission")] public string Permission { get; set; } = null!;
    [Column("is_writable")] public bool IsWritable { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
    [Column("deleted_at")] public DateTime? DeletedAt { get; set; }
    [Column("sync_version")] public long SyncVersion { get; set; }
}

[Table("device_states")]
public sealed class DeviceState
{
    [Key, Column("id")] public long Id { get; set; }
    [Column("device_id")] public long DeviceId { get; set; }
    [Column("state_json")] public string State { get; set; } = "{}";
    [Column("sampled_at")] public DateTime SampledAt { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
}

[Table("scenes")]
public sealed class Scene
{
    [Key, Column("id")] public long Id { get; set; }
    [Column("tenant_id")] public long TenantId { get; set; }
    [Column("scene_key")] public string SceneKey { get; set; } = null!;
    [Column("name")] public string Name { get; set; } = null!;
    [Column("summary")] public string? Summary { get; set; }
    [Column("status")] public string Status { get; set; } = "active";
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
    [Column("deleted_at")] public DateTime? DeletedAt { get; set; }
    [Column("sync_version")] public long SyncVersion { get; set; }
}

[Table("scene_actions")]
public sealed class SceneAction
{
    [Key, Column("id")] public long Id { get; set; }
    [Column("scene_id")] public long SceneId { get; set; }
    [Column("device_id")] public long DeviceId { get; set; }
    [Column("capability")] public string Capability { get; set; } = null!;
    [Column("target_value_json")] public string TargetValue { get; set; } = "{}";
    [Column("sort_order")] public int SortOrder { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
}

[Table("automation_rules")]
public sealed class AutomationRule
{
    [Key, Column("id")] public long Id { get; set; }
    [Column("tenant_id")] public long TenantId { get; set; }
    [Column("owner_user_id")] public long OwnerUserId { get; set; }
    [Column("name")] public string Name { get; set; } = null!;
    [Column("trigger_type")] public string TriggerType { get; set; } = null!;
    [Column("trigger_config_json")] public string TriggerConfig { get; set; } = "{}";
    [Column("conditions_json")] public string Conditions { get; set; } = "[]";
    [Column("actions_json")] public string Actions { get; set; } = "[]";
    [Column("approval_policy")] public string ApprovalPolicy { get; set; } = "manual_confirmation";
    [Column("enabled")] public bool Enabled { get; set; } = true;
    [Column("last_triggered_at")] public DateTime? LastTriggeredAt { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
    [Column("row_version")] public long RowVersion { get; set; }
    [Column("sync_version")] public long SyncVersion { get; set; }
}

[Table("connector_sync_jobs")]
public sealed class ConnectorSyncJob
{
    [Key, Column("id")] public long Id { get; set; }
    [Column("tenant_id")] public long TenantId { get; set; }
    [Column("workspace_connector_id")] public long WorkspaceConnectorId { get; set; }
    [Column("status")] public string Status { get; set; } = "queued";
    [Column("reason")] public string Reason { get; set; } = "manual";
    [Column("attempt_no")] public int AttemptNo { get; set; }
    [Column("available_at")] public DateTime AvailableAt { get; set; }
    [Column("started_at")] public DateTime? StartedAt { get; set; }
    [Column("completed_at")] public DateTime? CompletedAt { get; set; }
    [Column("last_error_code")] public string? LastErrorCode { get; set; }
    [Column("idempotency_key")] public string IdempotencyKey { get; set; } = null!;
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
    [Column("sync_version")] public long SyncVersion { get; set; }
}
