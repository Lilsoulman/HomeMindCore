namespace HomeMind.Business.IServices.Connector;

/// <summary>Adapter 内部的规范化发现结果；ExternalId 绝不进入 HTTP 响应。</summary>
/// <param name="ExternalId">供应商侧实体标识，仅停留在适配器层。</param>
/// <param name="Name">标准化展示名称。</param>
/// <param name="DeviceType">标准化设备类型，如 light / switch / sensor。</param>
/// <param name="OnlineStatus">在线状态，online 或 offline。</param>
/// <param name="StateJson">标准化状态 JSON 字符串。</param>
/// <param name="SampledAt">采样时间（UTC）。</param>
/// <param name="SpaceName">归属空间名称，可为空。</param>
/// <param name="Capabilities">标准化能力集合。</param>
/// <param name="ZigbeeRole">Zigbee 角色，end_device / router / coordinator；未知时为 null。</param>
/// <param name="BatteryLevel">电量百分比 0-100；未知时为 null。</param>
/// <param name="SignalLqi">信号强度 0-255；未知时为 null。</param>
/// <param name="HealthStatus">派生健康状态，healthy / degraded / offline / low_battery；未知时为 null。</param>
public sealed record DiscoveredDevice(
    string ExternalId,
    string Name,
    string DeviceType,
    string OnlineStatus,
    string StateJson,
    DateTime SampledAt,
    string? SpaceName,
    IReadOnlyList<DiscoveredDeviceCapability> Capabilities,
    string? ZigbeeRole = null,
    byte? BatteryLevel = null,
    int? SignalLqi = null,
    string? HealthStatus = null);

/// <summary>标准化能力描述，ValueSchema 为 JSON Schema 字符串。</summary>
/// <param name="Capability">能力编码，如 power / brightness。</param>
/// <param name="ValueSchema">目标值 JSON Schema。</param>
/// <param name="IsWritable">是否可写；只读能力不能下发命令。</param>
public sealed record DiscoveredDeviceCapability(string Capability, string ValueSchema, bool IsWritable);

/// <summary>设备发现契约：把供应商设备发现为标准化设备。业务层只依赖本接口，不感知具体厂商实现。</summary>
public interface IDeviceDiscovery
{
    /// <summary>发现适配器对应的 Provider 编码，如 home_assistant。</summary>
    string ProviderCode { get; }

    /// <summary>发现连接器下的全部设备并返回标准化结果。</summary>
    /// <param name="connector">连接器引用。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>标准化设备列表。</returns>
    /// <exception cref="ConnectorAdapterException">连接失败、超时或响应不可识别时抛出。</exception>
    Task<IReadOnlyList<DiscoveredDevice>> DiscoverDevicesAsync(ConnectorReference connector, CancellationToken cancellationToken = default);
}
