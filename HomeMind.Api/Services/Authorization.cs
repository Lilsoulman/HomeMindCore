using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Dapper;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace HomeMind.Api.Services;

public static class HomeMindAuthenticationDefaults
{
    public const string Scheme = "HomeMindBearer";
}

public static class PermissionNames
{
    public const string IdentityRead = "identity.read";
    public const string AiRead = "ai.read";
    public const string AiRun = "ai.run";
    public const string AiSkillsRead = "ai.skills.read";
    public const string AiSkillsWrite = "ai.skills.write";
    public const string CalendarRead = "calendar.read";
    public const string CalendarWrite = "calendar.write";
    public const string TodoRead = "todo.read";
    public const string TodoWrite = "todo.write";

    public static IReadOnlyCollection<string> All { get; } = new[]
    {
        IdentityRead,
        AiRead,
        AiRun,
        AiSkillsRead,
        AiSkillsWrite,
        CalendarRead,
        CalendarWrite,
        TodoRead,
        TodoWrite
    };
}

public sealed class PermissionRequirement : IAuthorizationRequirement
{
    public PermissionRequirement(string permission) => Permission = permission;
    public string Permission { get; }
}

public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private static readonly HashSet<string> MemberPermissions = new(StringComparer.Ordinal)
    {
        PermissionNames.IdentityRead,
        PermissionNames.AiRead,
        PermissionNames.AiRun,
        PermissionNames.AiSkillsRead,
        PermissionNames.AiSkillsWrite,
        PermissionNames.CalendarRead,
        PermissionNames.CalendarWrite,
        PermissionNames.TodoRead,
        PermissionNames.TodoWrite
    };

    private static readonly HashSet<string> ViewerPermissions = new(StringComparer.Ordinal)
    {
        PermissionNames.IdentityRead,
        PermissionNames.AiRead,
        PermissionNames.AiSkillsRead,
        PermissionNames.CalendarRead,
        PermissionNames.TodoRead
    };

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var role = context.User.FindFirstValue(ClaimTypes.Role);
        if (role is "owner" or "admin" || role == "member" && MemberPermissions.Contains(requirement.Permission) || role == "viewer" && ViewerPermissions.Contains(requirement.Permission))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

public sealed class AccessTokenValidator
{
    private readonly MySqlConnectionFactory _connections;

    public AccessTokenValidator(MySqlConnectionFactory connections) => _connections = connections;

    public async Task<UserContext?> ValidateAsync(AccessTokenPayload payload)
    {
        await using var db = _connections.Open();
        var role = await db.QuerySingleOrDefaultAsync<string>("SELECT m.role FROM tenant_members m JOIN users u ON u.id=m.user_id JOIN tenants t ON t.id=m.tenant_id WHERE m.user_id=@UserId AND m.tenant_id=@TenantId AND m.status='active' AND u.status='active' AND u.deleted_at IS NULL AND t.status='active'", new { payload.UserId, payload.TenantId });
        if (role is null) return null;

        var isRevoked = await db.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM auth_access_token_revocations WHERE token_id=@TokenId AND expires_at>UTC_TIMESTAMP(3)", new { payload.TokenId });
        return isRevoked == 0 ? new UserContext(payload.UserId, payload.TenantId, payload.DeviceId, role, payload.ExpiresAtUnixTime, payload.TokenId) : null;
    }

    public async Task RevokeAsync(UserContext user)
    {
        await using var db = _connections.Open();
        await db.OpenAsync();
        await using var transaction = await db.BeginTransactionAsync();
        await db.ExecuteAsync("INSERT INTO auth_access_token_revocations(token_id,user_id,tenant_id,expires_at,revoke_reason) VALUES (@TokenId,@UserId,@TenantId,FROM_UNIXTIME(@ExpiresAtUnixTime),'logout') ON DUPLICATE KEY UPDATE revoked_at=UTC_TIMESTAMP(3),revoke_reason='logout'", user, transaction);
        await db.ExecuteAsync("UPDATE auth_refresh_tokens SET revoked_at=UTC_TIMESTAMP(3),revoke_reason='logout' WHERE user_id=@UserId AND device_id=@DeviceId AND revoked_at IS NULL", user, transaction);
        await transaction.CommitAsync();
    }
}

public sealed class BearerAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly TokenService _tokens;
    private readonly AccessTokenValidator _validator;

    public BearerAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, TokenService tokens, AccessTokenValidator validator)
        : base(options, logger, encoder)
    {
        _tokens = tokens;
        _validator = validator;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authorization = Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(authorization)) return AuthenticateResult.NoResult();
        if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return AuthenticateResult.Fail("Authorization header must use the Bearer scheme.");
        if (!_tokens.TryRead(authorization[7..].Trim(), out var payload)) return AuthenticateResult.Fail("Access token is invalid or expired.");

        var user = await _validator.ValidateAsync(payload);
        if (user is null) return AuthenticateResult.Fail("Access token is revoked or the tenant membership is inactive.");

        Context.Items["HomeMind.User"] = user;
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new Claim("tenant_id", user.TenantId.ToString()),
            new Claim("device_id", user.DeviceId.ToString()),
            new Claim(ClaimTypes.Role, user.Role)
        };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
    }
}

public sealed class AuthorizeOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var actionAttributes = context.MethodInfo.GetCustomAttributes(true);
        var controllerAttributes = context.MethodInfo.DeclaringType?.GetCustomAttributes(true) ?? Array.Empty<object>();
        if (actionAttributes.OfType<IAllowAnonymous>().Any() || controllerAttributes.OfType<IAllowAnonymous>().Any()) return;
        if (!actionAttributes.OfType<IAuthorizeData>().Any() && !controllerAttributes.OfType<IAuthorizeData>().Any()) return;

        operation.Responses.TryAdd("401", new OpenApiResponse { Description = "Access token is missing, invalid, expired, or revoked." });
        operation.Responses.TryAdd("403", new OpenApiResponse { Description = "The current role does not have the required permission." });
        operation.Security ??= new List<OpenApiSecurityRequirement>();
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            }] = Array.Empty<string>()
        });
    }
}
