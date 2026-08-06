using HomeMind.Business.IServices.Connector;

namespace HomeMind.Business.IServices.SmartHome;

/// <summary>
/// 仅供 Connector Adapter 使用的密钥解析边界。实现不得把密钥写入数据库、日志或 HTTP 响应。
/// </summary>
public interface IConnectorSecretResolver
{
    Task<ConnectorSecretResolution> ResolveAsync(ConnectorReference connector, CancellationToken cancellationToken = default);
}

public sealed record ConnectorSecretResolution(bool Succeeded, string? SecretJson = null, string? ErrorCode = null, string? Message = null);
