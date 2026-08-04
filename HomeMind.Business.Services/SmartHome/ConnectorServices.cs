using System.Text.Json;
using System.Text.RegularExpressions;
using HomeMind.Business.IServices.SmartHome;
using HomeMind.Common.Model.Entities.SmartHome;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.SmartHome;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HomeMind.Business.Services.SmartHome;

public sealed class ConnectorServices : IConnectorServices
{
    private static readonly Regex ScopePattern = new("^[a-z][a-z0-9_]*(\\.[a-z][a-z0-9_*]*)+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private readonly HomeMindDbContext _db;
    private readonly IConnectorSecretReferenceValidator _secretReferences;

    public ConnectorServices(HomeMindDbContext db, IConnectorSecretReferenceValidator secretReferences)
    {
        _db = db;
        _secretReferences = secretReferences;
    }

    public async Task<ServiceResult> ListProvidersAsync(CancellationToken cancellationToken = default)
    {
        var providers = await _db.ConnectorProviders
            .Where(x => x.Status == "active" && x.DeletedAt == null)
            .OrderBy(x => x.Name)
            .Select(x => new ConnectorProviderView(x.Id, x.Code, x.Name, x.ConnectorType, x.Description))
            .ToListAsync(cancellationToken);
        return new ServiceResult(200, "查询成功。", providers);
    }

    public async Task<ServiceResult> ListConnectorsAsync(long userId, long tenantId, bool canManage, CancellationToken cancellationToken = default)
    {
        var connectors = _db.WorkspaceConnectors
            .Where(x => x.TenantId == tenantId && x.DeletedAt == null);
        if (!canManage)
        {
            var grantedConnectorIds = _db.UserConnectorAuthorizations
                .Where(x => x.TenantId == tenantId && x.UserId == userId && x.DeletedAt == null)
                .Select(x => x.WorkspaceConnectorId);
            connectors = connectors.Where(x => grantedConnectorIds.Contains(x.Id));
        }

        var items = await (from connector in connectors
                           join provider in _db.ConnectorProviders on connector.ConnectorProviderId equals provider.Id
                           where provider.DeletedAt == null
                           orderby connector.Name
                           select new WorkspaceConnectorView(
                               connector.Id,
                               provider.Id,
                               provider.Code,
                               provider.Name,
                               connector.Name,
                               connector.Status,
                               connector.LastSyncAt,
                               connector.LastHealthAt,
                               connector.CreatedAt,
                               connector.UpdatedAt))
            .ToListAsync(cancellationToken);
        return new ServiceResult(200, "查询成功。", items);
    }

    public async Task<ServiceResult> CreateConnectorAsync(long userId, long tenantId, CreateConnectorRequest request, CancellationToken cancellationToken = default)
    {
        if (request.UnsupportedProperties?.Count > 0)
            return new ServiceResult(422, "连接器请求只允许 providerId、name 和 credentialRef 字段。");
        if (request.ProviderId is null || string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.CredentialRef))
            return new ServiceResult(422, "请填写连接器类型、名称和凭据引用。");

        var secretReference = await _secretReferences.ValidateAsync(tenantId, request.CredentialRef.Trim(), cancellationToken);
        if (!secretReference.IsValid) return new ServiceResult(422, secretReference.Message);
        if (!secretReference.IsVaultAvailable) return new ServiceResult(503, secretReference.Message);

        var provider = await _db.ConnectorProviders.SingleOrDefaultAsync(
            x => x.Id == request.ProviderId.Value && x.Status == "active" && x.DeletedAt == null,
            cancellationToken);
        if (provider is null) return new ServiceResult(404, "请求的连接器类型不存在或已停用。");

        var now = DateTime.UtcNow;
        var connector = new WorkspaceConnector
        {
            TenantId = tenantId,
            ConnectorProviderId = provider.Id,
            Name = request.Name.Trim(),
            CredentialRef = request.CredentialRef.Trim(),
            Status = "disconnected",
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.WorkspaceConnectors.Add(connector);
        await _db.SaveChangesAsync(cancellationToken);
        return new ServiceResult(201, "创建成功，连接将在后续配置完成后测试。", ToView(connector, provider));
    }

    public async Task<ServiceResult> GetMyAuthorizationAsync(long userId, long tenantId, long connectorId, CancellationToken cancellationToken = default)
    {
        var authorization = await _db.UserConnectorAuthorizations
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.UserId == userId && x.WorkspaceConnectorId == connectorId && x.DeletedAt == null, cancellationToken);
        if (authorization is null)
        {
            var connectorExists = await _db.WorkspaceConnectors.AnyAsync(x => x.Id == connectorId && x.TenantId == tenantId && x.DeletedAt == null, cancellationToken);
            if (!connectorExists) return new ServiceResult(404, "请求的连接器不存在。");
            return new ServiceResult(403, "当前成员未被授予该连接器的使用范围。");
        }

        return new ServiceResult(200, "查询成功。", ToAuthorizationView(authorization));
    }

    public async Task<ServiceResult> UpdateAuthorizationAsync(long tenantId, long connectorId, long memberUserId, ConnectorAuthorizationRequest request, CancellationToken cancellationToken = default)
    {
        if (!TryValidateScopes(request.Scopes, out var scopes)) return new ServiceResult(422, "授权范围必须是非空、格式正确的权限列表。");
        var connectorExists = await _db.WorkspaceConnectors.AnyAsync(x => x.Id == connectorId && x.TenantId == tenantId && x.DeletedAt == null, cancellationToken);
        if (!connectorExists) return new ServiceResult(404, "请求的连接器不存在。");
        var isActiveMember = await _db.TenantMembers.AnyAsync(x => x.TenantId == tenantId && x.UserId == memberUserId && x.Status == "active", cancellationToken);
        if (!isActiveMember) return new ServiceResult(404, "目标成员不属于当前家庭。");

        var now = DateTime.UtcNow;
        var authorization = await _db.UserConnectorAuthorizations.SingleOrDefaultAsync(
            x => x.TenantId == tenantId && x.UserId == memberUserId && x.WorkspaceConnectorId == connectorId,
            cancellationToken);
        if (authorization is null)
        {
            authorization = new UserConnectorAuthorization
            {
                TenantId = tenantId,
                UserId = memberUserId,
                WorkspaceConnectorId = connectorId,
                Scope = JsonSerializer.Serialize(scopes),
                CreatedAt = now,
                UpdatedAt = now
            };
            _db.UserConnectorAuthorizations.Add(authorization);
        }
        else
        {
            authorization.Scope = JsonSerializer.Serialize(scopes);
            authorization.DeletedAt = null;
            authorization.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return new ServiceResult(200, "授权范围已更新。", ToAuthorizationView(authorization));
    }

    private static bool TryValidateScopes(IReadOnlyList<string>? requestedScopes, out string[] scopes)
    {
        scopes = requestedScopes?
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray() ?? [];
        return scopes.Length is > 0 and <= 32 && scopes.All(x => x.Length <= 64 && ScopePattern.IsMatch(x));
    }

    private static ConnectorAuthorizationView ToAuthorizationView(UserConnectorAuthorization authorization) =>
        new(authorization.WorkspaceConnectorId, authorization.UserId, ParseScopes(authorization.Scope), authorization.UpdatedAt);

    private static IReadOnlyList<string> ParseScopes(string value)
    {
        try { return JsonSerializer.Deserialize<string[]>(value) ?? []; }
        catch (JsonException) { return []; }
    }

    private static WorkspaceConnectorView ToView(WorkspaceConnector connector, ConnectorProvider provider) =>
        new(connector.Id, provider.Id, provider.Code, provider.Name, connector.Name, connector.Status, connector.LastSyncAt, connector.LastHealthAt, connector.CreatedAt, connector.UpdatedAt);
}

public sealed class ConfigurationConnectorSecretReferenceValidator : IConnectorSecretReferenceValidator
{
    private readonly IConfiguration _configuration;

    public ConfigurationConnectorSecretReferenceValidator(IConfiguration configuration) => _configuration = configuration;

    public Task<ConnectorSecretReferenceValidation> ValidateAsync(long tenantId, string credentialRef, CancellationToken cancellationToken = default)
    {
        if (!IsTenantVaultReference(tenantId, credentialRef))
            return Task.FromResult(new ConnectorSecretReferenceValidation(false, false, "凭据引用必须使用当前家庭的 vault://tenants/{tenantId}/... 格式。"));
        var enabled = bool.TryParse(_configuration["SecretVault:Enabled"], out var configured) && configured;
        return Task.FromResult(enabled
            ? new ConnectorSecretReferenceValidation(true, true, "凭据引用有效。")
            : new ConnectorSecretReferenceValidation(true, false, "Secret Vault 未配置或暂时不可用，无法创建连接器。"));
    }

    private static bool IsTenantVaultReference(long tenantId, string credentialRef)
    {
        if (!Uri.TryCreate(credentialRef, UriKind.Absolute, out var uri) || uri.Scheme != "vault" || uri.Host != "tenants" || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment)) return false;
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length >= 3 && segments[0] == tenantId.ToString() && segments.All(segment => !string.IsNullOrWhiteSpace(segment));
    }
}
