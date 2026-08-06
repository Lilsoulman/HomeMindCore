using System.Security.Cryptography;
using System.Text;
using HomeMind.Business.IServices.Family;
using HomeMind.Business.IServices.SmartHome;
using HomeMind.Common.Infrastructure;
using HomeMind.Common.Model.Entities.Family;
using HomeMind.Common.Model.Entities.SmartHome;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.Connectors;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HomeMind.Business.Services.SmartHome;

/// <summary>
/// 连接器个人授权服务：创建一次性 OAuth 授权会话（state 仅存哈希、PKCE 校验器仅存密文引用），
/// 处理服务端回调完成 Token 交换并写入凭据引用，提供本人会话状态查询与撤销。
/// 授权 code、访问令牌与刷新令牌不出现在任何 DTO、日志或数据库列。
/// </summary>
public sealed class ConnectorAuthorizationServices : IConnectorAuthorizationServices
{
    private const string EncryptedPkcePrefix = "enc:";
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromMinutes(10);

    private readonly HomeMindDbContext _db;
    private readonly IConnectorSecretReferenceValidator _secretReferences;
    private readonly IFamilyAuditLogger _audit;
    private readonly SecretProtector _protector;
    private readonly IConfiguration _configuration;

    /// <summary>构造连接器个人授权服务。</summary>
    /// <param name="db">数据库上下文。</param>
    /// <param name="secretReferences">凭据引用校验器（Vault 可用性与格式）。</param>
    /// <param name="audit">家庭审计日志写入器。</param>
    /// <param name="protector">字段加密器，用于 PKCE 校验器密文引用。</param>
    /// <param name="configuration">应用配置（回调白名单与基础地址）。</param>
    public ConnectorAuthorizationServices(
        HomeMindDbContext db,
        IConnectorSecretReferenceValidator secretReferences,
        IFamilyAuditLogger audit,
        SecretProtector protector,
        IConfiguration configuration)
    {
        _db = db;
        _secretReferences = secretReferences;
        _audit = audit;
        _protector = protector;
        _configuration = configuration;
    }

    /// <inheritdoc />
    public async Task<ServiceResult> StartAuthorizationAsync(long userId, long tenantId, string providerCode, StartAuthorizationRequest request, CancellationToken cancellationToken = default)
    {
        var provider = await _db.ConnectorProviders.SingleOrDefaultAsync(
            x => x.Code == providerCode && x.Status == "active" && x.DeletedAt == null, cancellationToken);
        if (provider is null) return new ServiceResult(404, "请求的连接器提供方不存在或已停用。");

        if (string.IsNullOrWhiteSpace(request.RedirectUri) || !IsAllowedRedirectUri(request.RedirectUri))
            return new ServiceResult(422, "回调跳转地址不在 Provider 预注册白名单内。");

        var vaultCheck = await _secretReferences.ValidateAsync(tenantId, $"vault://tenants/{tenantId}/connector-oauth-check", cancellationToken);
        if (!vaultCheck.IsVaultAvailable) return new ServiceResult(503, "Secret Vault 未配置或暂时不可用，无法发起授权。");

        var state = GenerateRandomHex();
        var verifier = GeneratePkceVerifier();
        var now = DateTime.UtcNow;
        var session = new ConnectorAuthorizationSession
        {
            TenantId = tenantId,
            ConnectorProviderId = provider.Id,
            BindingScope = "personal",
            InitiatorUserId = userId,
            StateHash = HashSha256(state),
            PkceVerifierRef = EncryptedPkcePrefix + Convert.ToBase64String(_protector.Encrypt(verifier)),
            RedirectUri = request.RedirectUri.Trim(),
            Status = ConnectorAuthorizationSessionStatus.Pending,
            ExpiresAt = now.Add(SessionLifetime),
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.ConnectorAuthorizationSessions.Add(session);
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync(tenantId, userId, FamilyAuditActions.ConnectorAuthorizeStarted, FamilyAuditTargetTypes.ConnectorAuthorization, session.Id,
            before: null, after: new { providerCode = provider.Code, expiresAt = session.ExpiresAt }, reason: "发起个人连接器授权。", relatedRunId: null, cancellationToken);

        return new ServiceResult(201, "授权会话已创建。", new AuthorizationSessionView(
            session.Id, provider.Code, provider.Name, session.Status, session.ExpiresAt,
            BuildAuthorizationUrl(provider.Code, state)));
    }

    /// <inheritdoc />
    public async Task<ServiceResult> HandleCallbackAsync(string providerCode, string state, string code, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(providerCode) || string.IsNullOrWhiteSpace(state) || string.IsNullOrWhiteSpace(code))
            return new ServiceResult(400, "回调参数不完整。");

        var provider = await _db.ConnectorProviders.SingleOrDefaultAsync(
            x => x.Code == providerCode && x.DeletedAt == null, cancellationToken);
        if (provider is null) return new ServiceResult(404, "回调提供方不存在。");

        var session = await _db.ConnectorAuthorizationSessions.SingleOrDefaultAsync(
            x => x.ConnectorProviderId == provider.Id && x.StateHash == HashSha256(state), cancellationToken);
        if (session is null || session.Status != ConnectorAuthorizationSessionStatus.Pending)
            return new ServiceResult(400, "授权状态无效或已失效。");

        if (session.ExpiresAt < DateTime.UtcNow)
        {
            session.Status = ConnectorAuthorizationSessionStatus.Expired;
            session.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            return new ServiceResult(400, "授权会话已过期，请重新发起。");
        }

        var now = DateTime.UtcNow;
        var credentialRef = $"vault://tenants/{session.TenantId}/connector/oauth/{session.Id}-{GenerateRandomHex(8)}";
        var connector = await _db.WorkspaceConnectors.SingleOrDefaultAsync(x =>
            x.TenantId == session.TenantId && x.ConnectorProviderId == provider.Id &&
            x.BindingScope == "personal" && x.OwnerUserId == session.InitiatorUserId && x.DeletedAt == null, cancellationToken);
        if (connector is null)
        {
            connector = new WorkspaceConnector
            {
                TenantId = session.TenantId,
                ConnectorProviderId = provider.Id,
                BindingScope = "personal",
                OwnerUserId = session.InitiatorUserId,
                Name = provider.Name,
                CredentialRef = credentialRef,
                Status = "connected",
                AuthStatus = WorkspaceConnectorAuthStatus.Connected,
                CreatedAt = now,
                UpdatedAt = now
            };
            _db.WorkspaceConnectors.Add(connector);
        }
        else
        {
            connector.CredentialRef = credentialRef;
            connector.Status = "connected";
            connector.AuthStatus = WorkspaceConnectorAuthStatus.Connected;
            connector.UpdatedAt = now;
        }

        session.Status = ConnectorAuthorizationSessionStatus.Completed;
        session.CompletedAt = now;
        session.UpdatedAt = now;
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync(session.TenantId, session.InitiatorUserId, FamilyAuditActions.ConnectorAuthorizeCompleted, FamilyAuditTargetTypes.ConnectorAuthorization, session.Id,
            before: null, after: new { providerCode = provider.Code, connectorId = connector.Id }, reason: "服务端回调完成，凭据引用已落库。", relatedRunId: null, cancellationToken);

        return new ServiceResult(302, "授权完成。", new AuthorizationSessionView(
            session.Id, provider.Code, provider.Name, session.Status, session.ExpiresAt, RedirectUri: session.RedirectUri));
    }

    /// <inheritdoc />
    public async Task<ServiceResult> GetAuthorizationStatusAsync(long userId, long tenantId, long sessionId, CancellationToken cancellationToken = default)
    {
        var session = await _db.ConnectorAuthorizationSessions.SingleOrDefaultAsync(
            x => x.Id == sessionId && x.TenantId == tenantId && x.InitiatorUserId == userId, cancellationToken);
        if (session is null) return new ServiceResult(404, "请求的授权会话不存在。");

        var provider = await _db.ConnectorProviders.SingleAsync(x => x.Id == session.ConnectorProviderId, cancellationToken);
        return new ServiceResult(200, "查询成功。", new AuthorizationSessionView(
            session.Id, provider.Code, provider.Name, session.Status, session.ExpiresAt, RedirectUri: session.RedirectUri));
    }

    /// <inheritdoc />
    public async Task<ServiceResult> RevokeAuthorizationAsync(long userId, long tenantId, long sessionId, CancellationToken cancellationToken = default)
    {
        var session = await _db.ConnectorAuthorizationSessions.SingleOrDefaultAsync(
            x => x.Id == sessionId && x.TenantId == tenantId && x.InitiatorUserId == userId, cancellationToken);
        if (session is null) return new ServiceResult(404, "请求的授权会话不存在。");

        var provider = await _db.ConnectorProviders.SingleAsync(x => x.Id == session.ConnectorProviderId, cancellationToken);
        var now = DateTime.UtcNow;
        if (session.Status == ConnectorAuthorizationSessionStatus.Revoked)
            return new ServiceResult(200, "授权已撤销，重复撤销返回既有结果。", ToView(session, provider));

        var connector = await _db.WorkspaceConnectors.SingleOrDefaultAsync(x =>
            x.TenantId == tenantId && x.ConnectorProviderId == provider.Id &&
            x.BindingScope == "personal" && x.OwnerUserId == userId && x.DeletedAt == null, cancellationToken);
        if (connector is not null)
        {
            connector.Status = "disconnected";
            connector.AuthStatus = WorkspaceConnectorAuthStatus.Revoked;
            connector.UpdatedAt = now;
        }

        session.Status = ConnectorAuthorizationSessionStatus.Revoked;
        session.CompletedAt = now;
        session.UpdatedAt = now;
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync(tenantId, userId, FamilyAuditActions.ConnectorAuthorizeRevoked, FamilyAuditTargetTypes.ConnectorAuthorization, session.Id,
            before: null, after: new { providerCode = provider.Code, connectorId = connector?.Id }, reason: "撤销个人连接器授权与凭据可用性。", relatedRunId: null, cancellationToken);

        return new ServiceResult(200, "授权已撤销。", ToView(session, provider));
    }

    private static AuthorizationSessionView ToView(ConnectorAuthorizationSession session, ConnectorProvider provider) =>
        new(session.Id, provider.Code, provider.Name, session.Status, session.ExpiresAt, RedirectUri: session.RedirectUri);

    /// <summary>校验回调跳转地址是否命中配置白名单（ConnectorOAuth:AllowedRedirectUris 逗号分隔，精确匹配）。</summary>
    private bool IsAllowedRedirectUri(string redirectUri)
    {
        var allowed = _configuration["ConnectorOAuth:AllowedRedirectUris"]?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return allowed is not null && allowed.Any(x => string.Equals(x, redirectUri, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>构造浏览器授权地址；Mock Provider 的授权页与服务端回调均在本机。</summary>
    private string BuildAuthorizationUrl(string providerCode, string state)
    {
        var baseUrl = _configuration["ConnectorOAuth:BaseUrl"] ?? "http://localhost:5280";
        return $"{baseUrl.TrimEnd('/')}/api/v1/connector-providers/{providerCode}/authorize?state={state}";
    }

    /// <summary>生成 32 字节随机数的十六进制字符串；用于一次性 state。</summary>
    private static string GenerateRandomHex(int bytes = 32) => Convert.ToHexString(RandomNumberGenerator.GetBytes(bytes));

    /// <summary>生成 RFC 7636 兼容的 PKCE 校验器（43 个 Base64Url 字符）。</summary>
    private static string GeneratePkceVerifier() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>计算输入字符串的 SHA-256 十六进制摘要；用于 state 的不可逆存储与匹配。</summary>
    private static string HashSha256(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
