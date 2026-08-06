using HomeMind.Business.IServices.Connector;
using HomeMind.Business.IServices.SmartHome;

namespace HomeMind.Business.Services.SmartHome;

/// <summary>默认拒绝解析，防止在未接入 Vault 时退回到配置或数据库中的明文凭据。</summary>
public sealed class UnavailableConnectorSecretResolver : IConnectorSecretResolver
{
    public Task<ConnectorSecretResolution> ResolveAsync(ConnectorReference connector, CancellationToken cancellationToken = default) =>
        Task.FromResult(new ConnectorSecretResolution(
            false,
            ErrorCode: "secret_vault_unavailable",
            Message: "Secret Vault 未配置或暂时不可用，无法连接 Home Assistant。"));
}
