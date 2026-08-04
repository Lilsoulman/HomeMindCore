using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using HomeMind.Business.IServices.SmartHome;
using Microsoft.Extensions.Configuration;

namespace HomeMind.Business.Services.SmartHome;

/// <summary>通过 HashiCorp Vault HTTP API 解析 Connector 凭据；Vault 令牌仅来自进程环境变量。</summary>
public sealed class HashiCorpVaultConnectorSecretResolver : IConnectorSecretResolver
{
    private readonly IConfiguration _configuration;

    public HashiCorpVaultConnectorSecretResolver(IConfiguration configuration) => _configuration = configuration;

    public async Task<ConnectorSecretResolution> ResolveAsync(ConnectorReference connector, CancellationToken cancellationToken = default)
    {
        if (!IsEnabled()) return Unavailable();
        if (!TryGetVaultRequest(connector, out var requestUri, out var vaultToken)) return Unavailable();

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Add("X-Vault-Token", vaultToken);
        var vaultNamespace = _configuration["SecretVault:Namespace"];
        if (!string.IsNullOrWhiteSpace(vaultNamespace)) request.Headers.Add("X-Vault-Namespace", vaultNamespace);
        try
        {
            using var response = await client.SendAsync(request, cancellationToken);
            if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
                return new ConnectorSecretResolution(false, ErrorCode: "secret_vault_denied", Message: "Secret Vault 拒绝读取该家庭的连接器凭据。 ");
            if (!response.IsSuccessStatusCode)
                return new ConnectorSecretResolution(false, ErrorCode: "secret_vault_error", Message: "Secret Vault 暂时无法读取连接器凭据。 ");

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var secret = document.RootElement;
            if (secret.TryGetProperty("data", out var data)) secret = data;
            if (secret.ValueKind == JsonValueKind.Object && secret.TryGetProperty("data", out var nestedData)) secret = nestedData;
            if (!secret.TryGetProperty("baseUrl", out var baseUrl) || !secret.TryGetProperty("accessToken", out var accessToken)
                || baseUrl.ValueKind != JsonValueKind.String || accessToken.ValueKind != JsonValueKind.String)
                return new ConnectorSecretResolution(false, ErrorCode: "invalid_secret", Message: "Secret Vault 中的 Home Assistant 凭据格式无效。 ");

            // 只将 Adapter 所需字段保留在本次调用的内存中，避免响应包含其它 Vault 字段。
            var secretJson = JsonSerializer.Serialize(new { baseUrl = baseUrl.GetString(), accessToken = accessToken.GetString() });
            return new ConnectorSecretResolution(true, secretJson);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new ConnectorSecretResolution(false, ErrorCode: "secret_vault_timeout", Message: "读取 Secret Vault 超时。 ");
        }
        catch (HttpRequestException)
        {
            return new ConnectorSecretResolution(false, ErrorCode: "secret_vault_unavailable", Message: "Secret Vault 未配置或暂时不可用，无法连接 Home Assistant。 ");
        }
    }

    private bool IsEnabled() => bool.TryParse(_configuration["SecretVault:Enabled"], out var enabled) && enabled;

    private bool TryGetVaultRequest(ConnectorReference connector, out Uri requestUri, out string vaultToken)
    {
        requestUri = default!;
        vaultToken = string.Empty;
        var endpoint = _configuration["SecretVault:Endpoint"];
        var tokenVariable = _configuration["SecretVault:TokenEnvironmentVariable"] ?? "NEXUSMIND_SECRET_VAULT_TOKEN";
        vaultToken = Environment.GetEnvironmentVariable(tokenVariable) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(vaultToken)) return false;
        if (!Uri.TryCreate(connector.CredentialRef, UriKind.Absolute, out var credentialUri) || credentialUri.Scheme != "vault" || credentialUri.Host != "tenants") return false;
        var path = credentialUri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.EscapeDataString);
        var vaultPath = "tenants/" + string.Join('/', path);
        if (!Uri.TryCreate(endpoint.TrimEnd('/') + "/v1/" + vaultPath, UriKind.Absolute, out var uri)) return false;
        requestUri = uri;
        return true;
    }

    private static ConnectorSecretResolution Unavailable() =>
        new(false, ErrorCode: "secret_vault_unavailable", Message: "Secret Vault 未配置或暂时不可用，无法连接 Home Assistant。 ");
}
