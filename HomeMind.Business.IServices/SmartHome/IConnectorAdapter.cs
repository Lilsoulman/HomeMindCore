using System.Text.Json;

namespace HomeMind.Business.IServices.SmartHome;

public interface IConnectorAdapter
{
    string ProviderCode { get; }
    Task<ConnectorConnectionTestResult> TestConnectionAsync(ConnectorReference connector, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DiscoveredDevice>> DiscoverDevicesAsync(ConnectorReference connector, CancellationToken cancellationToken = default);
    Task<AdapterDeviceState?> ReadDeviceStateAsync(ConnectorReference connector, long deviceId, CancellationToken cancellationToken = default);
    Task<DeviceCommandResult> ExecuteCommandAsync(ConnectorReference connector, DeviceCommand command, CancellationToken cancellationToken = default);
}

public sealed record ConnectorReference(long ConnectorId, long TenantId, string CredentialRef);
public sealed record ConnectorConnectionTestResult(bool Succeeded, string? ErrorCode = null, string? Message = null);
/// <summary>Adapter 内部的规范化发现结果；ExternalId 绝不进入 HTTP 响应。</summary>
public sealed record DiscoveredDevice(
    string ExternalId,
    string Name,
    string DeviceType,
    string OnlineStatus,
    string StateJson,
    DateTime SampledAt,
    string? SpaceName,
    IReadOnlyList<DiscoveredDeviceCapability> Capabilities);
public sealed record DiscoveredDeviceCapability(string Capability, string ValueSchema, bool IsWritable);
public sealed record AdapterDeviceState(long DeviceId, JsonElement State, DateTime SampledAt);
public sealed record DeviceCommand(long ConnectorId, long DeviceId, string Capability, JsonElement TargetValue, long OperatorUserId, long RunActionId, string IdempotencyKey);
public sealed record DeviceCommandResult(bool Succeeded, string Status, string? ErrorCode = null, string? Message = null);
