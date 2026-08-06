using System.Text.Json;

namespace HomeMind.Business.IServices.Connector;

/// <summary>连接器引用，包含连接器主键、租户主键与凭据引用；凭据值永不离开适配器层。</summary>
/// <param name="ConnectorId">连接器主键。</param>
/// <param name="TenantId">租户主键，由 JWT 推导，客户端不可覆盖。</param>
/// <param name="CredentialRef">Vault 凭据引用标识。</param>
public sealed record ConnectorReference(long ConnectorId, long TenantId, string CredentialRef);

/// <summary>连接测试结果；Succeeded 为 false 时携带错误码与可展示信息。</summary>
/// <param name="Succeeded">连接测试是否成功。</param>
/// <param name="ErrorCode">错误码，如 secret_vault_unavailable / timeout / unreachable。</param>
/// <param name="Message">面向用户的中文说明。</param>
public sealed record ConnectorConnectionTestResult(bool Succeeded, string? ErrorCode = null, string? Message = null);

/// <summary>适配器读取的单设备标准化状态；内部字段绝不进入 HTTP 响应。</summary>
/// <param name="DeviceId">标准化设备主键。</param>
/// <param name="State">标准化状态 JSON。</param>
/// <param name="SampledAt">采样时间（UTC）。</param>
public sealed record AdapterDeviceState(long DeviceId, JsonElement State, DateTime SampledAt);

/// <summary>设备边界核心契约：连接健康测试与单设备状态读取。业务层只依赖本接口，不感知具体厂商实现。</summary>
public interface IDeviceAdapter
{
    /// <summary>适配器对应的 Provider 编码，如 home_assistant。</summary>
    string ProviderCode { get; }

    /// <summary>测试连接器连通性与凭据可用性；失败时返回可展示错误码。</summary>
    /// <param name="connector">连接器引用。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>连接测试结果。</returns>
    Task<ConnectorConnectionTestResult> TestConnectionAsync(ConnectorReference connector, CancellationToken cancellationToken = default);

    /// <summary>读取单设备最新标准化状态；设备不可达或未同步时返回 null。</summary>
    /// <param name="connector">连接器引用。</param>
    /// <param name="deviceId">标准化设备主键。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>设备状态；不可达时为 null。</returns>
    Task<AdapterDeviceState?> ReadDeviceStateAsync(ConnectorReference connector, long deviceId, CancellationToken cancellationToken = default);
}
