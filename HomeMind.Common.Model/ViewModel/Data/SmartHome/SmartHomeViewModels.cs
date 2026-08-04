namespace HomeMind.Common.Model.ViewModel.Data.SmartHome;

public sealed record SmartHomeSpaceView(long Id, string Name, string SpaceType, string? Summary, int DeviceCount, DateTime UpdatedAt);
public sealed record DeviceCapabilityView(string Capability, string ValueSchema, string Permission, bool IsWritable);
public sealed record SmartHomeDeviceView(long Id, long? SpaceId, string Name, string DeviceType, string OnlineStatus, string? StateSummary, DateTime? StateUpdatedAt, IReadOnlyList<DeviceCapabilityView> Capabilities);
public sealed record SceneView(long Id, string Key, string Name, string? Summary, string Status, DateTime UpdatedAt);

public sealed record SceneRunRequest(string? IdempotencyKey);

/// <summary>Built-in, user-facing scene definitions. Their execution is planned as a housekeeper Run.</summary>
public static class SmartHomeSceneDefinitions
{
    public static IReadOnlyList<SceneView> CreateViews(DateTime updatedAt) =>
    [
        new(-301, "arrive_home", "回家", "恢复舒适照明与环境。", "active", updatedAt),
        new(-302, "leave_home", "离家", "关闭非必要设备并检查安全状态。", "active", updatedAt),
        new(-303, "sleep", "睡眠", "调整卧室灯光和空调。", "active", updatedAt)
    ];

    public static bool TryGetIntent(string? key, out string intent)
    {
        intent = key?.Trim().ToLowerInvariant() switch
        {
            "arrive_home" or "arrive" => "arrive",
            "leave_home" or "away" => "away",
            "sleep" => "sleep",
            _ => string.Empty
        };
        return intent.Length > 0;
    }
}

public sealed record ConnectorProviderView(long Id, string Code, string Name, string ConnectorType, string? Description);
public sealed record WorkspaceConnectorView(long Id, long ProviderId, string ProviderCode, string ProviderName, string Name, string Status, DateTime? LastSyncAt, DateTime? LastHealthAt, DateTime CreatedAt, DateTime UpdatedAt);
public sealed record ConnectorAuthorizationView(long ConnectorId, long UserId, IReadOnlyList<string> Scopes, DateTime UpdatedAt);
public sealed record ConnectorOperationView(long ConnectorId, string Status, int DeviceCount, DateTime? LastHealthAt, DateTime? LastSyncAt);

public sealed class AutomationRuleRequest
{
    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(128)]
    public string? Name { get; init; }
    [System.ComponentModel.DataAnnotations.Required]
    public string? TriggerType { get; init; }
    [System.ComponentModel.DataAnnotations.Required]
    public System.Text.Json.JsonElement? Trigger { get; init; }
    public System.Text.Json.JsonElement? Conditions { get; init; }
    [System.ComponentModel.DataAnnotations.Required]
    public System.Text.Json.JsonElement? Actions { get; init; }
    public string? ApprovalPolicy { get; init; }
    public bool? Enabled { get; init; }
}

public sealed class UpdateAutomationRuleRequest
{
    [System.ComponentModel.DataAnnotations.StringLength(128)] public string? Name { get; init; }
    public System.Text.Json.JsonElement? Trigger { get; init; }
    public System.Text.Json.JsonElement? Conditions { get; init; }
    public System.Text.Json.JsonElement? Actions { get; init; }
    public string? ApprovalPolicy { get; init; }
    public bool? Enabled { get; init; }
    [System.ComponentModel.DataAnnotations.Required] public long? RowVersion { get; init; }
}

public sealed record AutomationRuleView(long Id, string Name, string TriggerType, System.Text.Json.JsonElement Trigger, System.Text.Json.JsonElement Conditions, System.Text.Json.JsonElement Actions, string ApprovalPolicy, bool Enabled, DateTime? LastTriggeredAt, DateTime UpdatedAt, long RowVersion);
public sealed record ConnectorSyncJobView(long Id, long ConnectorId, string Status, string Reason, int AttemptNo, DateTime AvailableAt, DateTime? CompletedAt, DateTime UpdatedAt);
