namespace HomeMind.Common.Model.ViewModel.Data.SmartHome;

/// <summary>智能家居空间视图。</summary>
/// <param name="Id">空间主键。</param>
/// <param name="Name">空间名称。</param>
/// <param name="SpaceType">空间类型。</param>
/// <param name="Summary">空间摘要，可为空。</param>
/// <param name="DeviceCount">设备总数。</param>
/// <param name="UpdatedAt">更新时间（UTC）。</param>
public sealed record SmartHomeSpaceView(long Id, string Name, string SpaceType, string? Summary, int DeviceCount, DateTime UpdatedAt);

/// <summary>设备能力视图。</summary>
/// <param name="Capability">能力名。</param>
/// <param name="ValueSchema">能力取值 JSON Schema 字符串。</param>
/// <param name="Permission">所需权限名。</param>
/// <param name="IsWritable">是否可写。</param>
public sealed record DeviceCapabilityView(string Capability, string ValueSchema, string Permission, bool IsWritable);

/// <summary>智能家居设备归一化视图。</summary>
/// <param name="Id">设备主键。</param>
/// <param name="SpaceId">所属空间主键，可为空。</param>
/// <param name="Name">设备名。</param>
/// <param name="DeviceType">设备类型。</param>
/// <param name="OnlineStatus">在线状态。</param>
/// <param name="StateSummary">状态摘要，可为空。</param>
/// <param name="StateUpdatedAt">最近一次状态更新时间（UTC）。</param>
/// <param name="Capabilities">设备能力列表。</param>
/// <param name="ZigbeeRole">Zigbee 角色，可为空。</param>
/// <param name="BatteryLevel">电池电量百分比 0-100，可为空。</param>
/// <param name="SignalLqi">信号 LQI，可为空。</param>
/// <param name="HealthStatus">健康状态，可为空。</param>
public sealed record SmartHomeDeviceView(long Id, long? SpaceId, string Name, string DeviceType, string OnlineStatus, string? StateSummary, DateTime? StateUpdatedAt, IReadOnlyList<DeviceCapabilityView> Capabilities, string? ZigbeeRole = null, byte? BatteryLevel = null, int? SignalLqi = null, string? HealthStatus = null);

/// <summary>设备健康聚合摘要。</summary>
/// <param name="Total">设备总数。</param>
/// <param name="Healthy">健康设备数。</param>
/// <param name="Degraded">降级设备数。</param>
/// <param name="Offline">离线设备数。</param>
/// <param name="LowBattery">低电量设备数。</param>
/// <param name="DominantStatus">主要状态，可为空。</param>
public sealed record DeviceHealthSummaryView(int Total, int Healthy, int Degraded, int Offline, int LowBattery, string? DominantStatus = null);

/// <summary>单设备健康详情视图。</summary>
/// <param name="Id">设备主键。</param>
/// <param name="SpaceId">归属空间主键，可为空。</param>
/// <param name="Name">设备展示名。</param>
/// <param name="DeviceType">标准化设备类型。</param>
/// <param name="OnlineStatus">在线状态，online / offline。</param>
/// <param name="ZigbeeRole">Zigbee 角色，end_device / router / coordinator；未知为 null。</param>
/// <param name="BatteryLevel">电量百分比 0-100；未知为 null。</param>
/// <param name="SignalLqi">信号强度 0-255；未知为 null。</param>
/// <param name="HealthStatus">派生健康状态，healthy / degraded / offline / low_battery。</param>
/// <param name="StateUpdatedAt">最近状态采样时间（UTC），可为空表示未同步。</param>
public sealed record DeviceHealthDetailView(long Id, long? SpaceId, string Name, string DeviceType, string OnlineStatus, string? ZigbeeRole = null, byte? BatteryLevel = null, int? SignalLqi = null, string? HealthStatus = null, DateTime? StateUpdatedAt = null);

/// <summary>场景视图。</summary>
/// <param name="Id">场景主键。</param>
/// <param name="Key">场景业务键。</param>
/// <param name="Name">场景展示名。</param>
/// <param name="Summary">场景摘要，可为空。</param>
/// <param name="Status">场景状态。</param>
/// <param name="UpdatedAt">更新时间（UTC）。</param>
public sealed record SceneView(long Id, string Key, string Name, string? Summary, string Status, DateTime UpdatedAt);

/// <summary>执行场景的请求参数。</summary>
/// <param name="IdempotencyKey">幂等键，可为空。</param>
public sealed record SceneRunRequest(string? IdempotencyKey);

/// <summary>内置场景目录。其执行通过管家 Run 完成，仍受确认、授权、幂等与审计约束。</summary>
public static class SmartHomeSceneDefinitions
{
    /// <summary>构造内置场景的展示列表。</summary>
    /// <param name="updatedAt">统一更新时间戳。</param>
    /// <returns>内置场景视图列表。</returns>
    public static IReadOnlyList<SceneView> CreateViews(DateTime updatedAt) =>
    [
        new(-301, "arrive_home", "回家", "恢复舒适照明与环境。", "active", updatedAt),
        new(-302, "leave_home", "离家", "关闭非必要设备并检查安全状态。", "active", updatedAt),
        new(-303, "sleep", "睡眠", "调整卧室灯光和空调。", "active", updatedAt)
    ];

    /// <summary>尝试将场景键解析为管家意图。</summary>
    /// <param name="key">场景键或快捷方式名。</param>
    /// <param name="intent">输出参数，解析成功时为"arrive""away"或"sleep"，失败时为空串。</param>
    /// <returns>解析是否成功。</returns>
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

/// <summary>连接器提供方目录视图。</summary>
/// <param name="Id">提供方主键。</param>
/// <param name="Code">业务编码。</param>
/// <param name="Name">展示名。</param>
/// <param name="ConnectorType">连接器类型。</param>
/// <param name="Description">描述，可为空。</param>
public sealed record ConnectorProviderView(long Id, string Code, string Name, string ConnectorType, string? Description);

/// <summary>工作区连接器视图。</summary>
/// <param name="Id">连接器主键。</param>
/// <param name="ProviderId">提供方主键。</param>
/// <param name="ProviderCode">提供方编码。</param>
/// <param name="ProviderName">提供方展示名。</param>
/// <param name="Name">租户侧名称。</param>
/// <param name="Status">连接器状态。</param>
/// <param name="LastSyncAt">最近一次同步时间（UTC）。</param>
/// <param name="LastHealthAt">最近一次健康探测时间（UTC）。</param>
/// <param name="CreatedAt">创建时间（UTC）。</param>
/// <param name="UpdatedAt">更新时间（UTC）。</param>
public sealed record WorkspaceConnectorView(long Id, long ProviderId, string ProviderCode, string ProviderName, string Name, string Status, DateTime? LastSyncAt, DateTime? LastHealthAt, DateTime CreatedAt, DateTime UpdatedAt);

/// <summary>用户对连接器的授权视图。</summary>
/// <param name="ConnectorId">连接器主键。</param>
/// <param name="UserId">被授权用户主键。</param>
/// <param name="Scopes">已授予的作用域列表。</param>
/// <param name="UpdatedAt">更新时间（UTC）。</param>
public sealed record ConnectorAuthorizationView(long ConnectorId, long UserId, IReadOnlyList<string> Scopes, DateTime UpdatedAt);

/// <summary>连接器操作结果视图。</summary>
/// <param name="ConnectorId">连接器主键。</param>
/// <param name="Status">操作后的连接器状态。</param>
/// <param name="DeviceCount">当前已知设备数。</param>
/// <param name="LastHealthAt">最近一次健康探测时间（UTC）。</param>
/// <param name="LastSyncAt">最近一次同步时间（UTC）。</param>
public sealed record ConnectorOperationView(long ConnectorId, string Status, int DeviceCount, DateTime? LastHealthAt, DateTime? LastSyncAt);

/// <summary>创建或替换自动化规则的请求体。</summary>
public sealed class AutomationRuleRequest
{
    /// <summary>规则名称，长度 1-128。</summary>
    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(128), System.ComponentModel.Description("规则名称，长度 1-128。")]
    public string? Name { get; init; }
    /// <summary>触发类型，取值见 AutomationTriggerType。</summary>
    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.Description("触发类型，可选 time_schedule、device_state_change、scene_completed、sync_completed。")]
    public string? TriggerType { get; init; }
    /// <summary>触发配置 JSON 对象。</summary>
    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.Description("触发配置 JSON，结构随 triggerType 变化。")]
    public System.Text.Json.JsonElement? Trigger { get; init; }
    /// <summary>额外条件 JSON 数组，可选。</summary>
    [System.ComponentModel.Description("额外条件 JSON 数组，可选。")]
    public System.Text.Json.JsonElement? Conditions { get; init; }
    /// <summary>动作列表 JSON 数组，元素必须为内置场景键。</summary>
    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.Description("动作列表 JSON 数组，元素必须为内置场景键。")]
    public System.Text.Json.JsonElement? Actions { get; init; }
    /// <summary>审批策略，可选"manual_confirmation"或"auto_execute"。</summary>
    [System.ComponentModel.Description("审批策略，可选 manual_confirmation 或 auto_execute。")]
    public string? ApprovalPolicy { get; init; }
    /// <summary>是否启用规则。</summary>
    [System.ComponentModel.Description("是否启用规则。")]
    public bool? Enabled { get; init; }
}

/// <summary>部分更新自动化规则的请求体。</summary>
public sealed class UpdateAutomationRuleRequest
{
    /// <summary>规则名称，长度 1-128。</summary>
    [System.ComponentModel.DataAnnotations.StringLength(128), System.ComponentModel.Description("规则名称，长度 1-128。")]
    public string? Name { get; init; }
    /// <summary>触发配置 JSON 对象。</summary>
    [System.ComponentModel.Description("触发配置 JSON，结构随 triggerType 变化。")]
    public System.Text.Json.JsonElement? Trigger { get; init; }
    /// <summary>额外条件 JSON 数组。</summary>
    [System.ComponentModel.Description("额外条件 JSON 数组。")]
    public System.Text.Json.JsonElement? Conditions { get; init; }
    /// <summary>动作列表 JSON 数组。</summary>
    [System.ComponentModel.Description("动作列表 JSON 数组，元素必须为内置场景键。")]
    public System.Text.Json.JsonElement? Actions { get; init; }
    /// <summary>审批策略。</summary>
    [System.ComponentModel.Description("审批策略，可选 manual_confirmation 或 auto_execute。")]
    public string? ApprovalPolicy { get; init; }
    /// <summary>是否启用规则。</summary>
    [System.ComponentModel.Description("是否启用规则。")]
    public bool? Enabled { get; init; }
    /// <summary>乐观锁版本号，必填且必须与数据库当前值一致。</summary>
    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.Description("乐观锁版本号，更新时必填且必须与数据库当前值一致。")]
    public long? RowVersion { get; init; }
}

/// <summary>自动化规则视图。</summary>
/// <param name="Id">规则主键。</param>
/// <param name="Name">规则名称。</param>
/// <param name="TriggerType">触发类型。</param>
/// <param name="Trigger">触发配置 JSON。</param>
/// <param name="Conditions">条件 JSON。</param>
/// <param name="Actions">动作 JSON。</param>
/// <param name="ApprovalPolicy">审批策略。</param>
/// <param name="Enabled">是否启用。</param>
/// <param name="LastTriggeredAt">最近一次触发时间（UTC）。</param>
/// <param name="UpdatedAt">更新时间（UTC）。</param>
/// <param name="RowVersion">乐观锁版本号。</param>
public sealed record AutomationRuleView(long Id, string Name, string TriggerType, System.Text.Json.JsonElement Trigger, System.Text.Json.JsonElement Conditions, System.Text.Json.JsonElement Actions, string ApprovalPolicy, bool Enabled, DateTime? LastTriggeredAt, DateTime UpdatedAt, long RowVersion);

/// <summary>连接器同步任务视图。</summary>
/// <param name="Id">任务主键。</param>
/// <param name="ConnectorId">所属连接器主键。</param>
/// <param name="Status">任务状态。</param>
/// <param name="Reason">任务触发原因。</param>
/// <param name="AttemptNo">当前重试次数。</param>
/// <param name="AvailableAt">任务可被拉取的最早时间（UTC）。</param>
/// <param name="CompletedAt">完成时间（UTC）。</param>
/// <param name="UpdatedAt">更新时间（UTC）。</param>
public sealed record ConnectorSyncJobView(long Id, long ConnectorId, string Status, string Reason, int AttemptNo, DateTime AvailableAt, DateTime? CompletedAt, DateTime UpdatedAt);
