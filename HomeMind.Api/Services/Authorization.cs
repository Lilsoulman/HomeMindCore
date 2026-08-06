using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using HomeMind.Common.Model.Entities;
using HomeMind.Common.Repository;
using HomeMind.Common.Infrastructure;
using HomeMind.Common.Model.ViewModel.Common;
using Microsoft.EntityFrameworkCore;
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
    public const string AiConfigRead = "ai.config.read";
    public const string AiConfigWrite = "ai.config.write";
    public const string CalendarRead = "calendar.read";
    public const string CalendarWrite = "calendar.write";
    public const string TodoRead = "todo.read";
    public const string TodoWrite = "todo.write";
    public const string SmartHomeRead = "smart_home.read";
    public const string ConnectorRead = "connector.read";
    public const string ConnectorWrite = "connector.write";
    public const string ConnectorAuthorize = "connector.authorize";
    public const string AutomationRead = "automation.read";
    public const string AutomationWrite = "automation.write";
    public const string ExpertFileRead = "expert_file.read";
    public const string ExpertFileWrite = "expert_file.write";
    public const string TeamRunRead = "team_run.read";
    public const string TeamRunWrite = "team_run.write";
    public const string TeamManage = "team.manage";
    public const string FamilyRead = "family.read";
    public const string FamilyWrite = "family.write";
    public const string StewardActivityRead = "steward.activity.read";
    public const string ConfirmationRead = "confirmation.read";
    public const string ConfirmationWrite = "confirmation.write";
    public const string LifeFavoriteRead = "life.favorite.read";
    public const string LifeFavoriteWrite = "life.favorite.write";

    public static IReadOnlyCollection<string> All { get; } = new[]
    {
        IdentityRead,
        AiRead,
        AiRun,
        AiSkillsRead,
        AiSkillsWrite,
        AiConfigRead,
        AiConfigWrite,
        CalendarRead,
        CalendarWrite,
        TodoRead,
        TodoWrite,
        SmartHomeRead,
        ConnectorRead,
        ConnectorWrite,
        ConnectorAuthorize
        ,AutomationRead
        ,AutomationWrite
        ,ExpertFileRead
        ,ExpertFileWrite
        ,TeamRunRead
        ,TeamRunWrite
        ,TeamManage
        ,FamilyRead
        ,FamilyWrite
        ,StewardActivityRead
        ,ConfirmationRead
        ,ConfirmationWrite
        ,LifeFavoriteRead
        ,LifeFavoriteWrite
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
        PermissionNames.AiConfigRead,
        PermissionNames.AiConfigWrite,
        PermissionNames.CalendarRead,
        PermissionNames.CalendarWrite,
        PermissionNames.TodoRead,
        PermissionNames.TodoWrite,
        PermissionNames.SmartHomeRead,
        PermissionNames.ConnectorRead,
        PermissionNames.ConnectorAuthorize
        ,PermissionNames.AutomationRead
        ,PermissionNames.ExpertFileRead
        ,PermissionNames.TeamRunRead
        ,PermissionNames.FamilyRead
        ,PermissionNames.FamilyWrite
        ,PermissionNames.StewardActivityRead
        ,PermissionNames.ConfirmationRead
        ,PermissionNames.ConfirmationWrite
        ,PermissionNames.LifeFavoriteRead
        ,PermissionNames.LifeFavoriteWrite
    };

    private static readonly HashSet<string> ViewerPermissions = new(StringComparer.Ordinal)
    {
        PermissionNames.IdentityRead,
        PermissionNames.AiRead,
        PermissionNames.AiSkillsRead,
        PermissionNames.AiConfigRead,
        PermissionNames.CalendarRead,
        PermissionNames.TodoRead,
        PermissionNames.SmartHomeRead
        ,PermissionNames.AutomationRead
        ,PermissionNames.ExpertFileRead
        ,PermissionNames.FamilyRead
        ,PermissionNames.StewardActivityRead
        ,PermissionNames.ConfirmationRead
        ,PermissionNames.LifeFavoriteRead
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
    private readonly HomeMindDbContext _db;

    public AccessTokenValidator(HomeMindDbContext db) => _db = db;

    public async Task<UserContext?> ValidateAsync(AccessTokenPayload payload)
    {
        var role = await (from member in _db.TenantMembers
                          join account in _db.Users on member.UserId equals account.Id
                          join tenant in _db.Tenants on member.TenantId equals tenant.Id
                          where member.UserId == payload.UserId && member.TenantId == payload.TenantId
                                && member.Status == "active" && account.Status == "active"
                                && account.DeletedAt == null && tenant.Status == "active"
                          select member.Role).SingleOrDefaultAsync();
        if (role is null) return null;

        var isRevoked = await _db.AccessTokenRevocations.AnyAsync(x => x.TokenId == payload.TokenId && x.ExpiresAt > DateTime.UtcNow);
        return !isRevoked ? new UserContext(payload.UserId, payload.TenantId, payload.DeviceId, role, payload.ExpiresAtUnixTime, payload.TokenId) : null;
    }

    public async Task RevokeAsync(UserContext user)
    {
        var now = DateTime.UtcNow;
        var revocation = await _db.AccessTokenRevocations.FindAsync(user.TokenId);
        if (revocation is null)
        {
            _db.AccessTokenRevocations.Add(new AccessTokenRevocation
            {
                TokenId = user.TokenId,
                UserId = user.UserId,
                TenantId = user.TenantId,
                ExpiresAt = DateTimeOffset.FromUnixTimeSeconds(user.ExpiresAtUnixTime).UtcDateTime,
                RevokedAt = now,
                RevokeReason = "logout"
            });
        }
        else
        {
            revocation.RevokedAt = now;
            revocation.RevokeReason = "logout";
        }

        var refreshTokens = await _db.AuthRefreshTokens
            .Where(x => x.UserId == user.UserId && x.DeviceId == user.DeviceId && x.RevokedAt == null)
            .ToListAsync();
        foreach (var refreshToken in refreshTokens)
        {
            refreshToken.RevokedAt = now;
            refreshToken.RevokeReason = "logout";
        }
        await _db.SaveChangesAsync();
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
        if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return AuthenticateResult.Fail("认证请求头格式错误，请使用访问令牌认证。");
        if (!_tokens.TryRead(authorization[7..].Trim(), out var payload)) return AuthenticateResult.Fail("访问令牌无效或已过期。");

        var user = await _validator.ValidateAsync(payload);
        if (user is null) return AuthenticateResult.Fail("访问令牌已失效，或当前租户成员资格未启用。");

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

    protected override Task HandleChallengeAsync(AuthenticationProperties properties) =>
        WriteErrorAsync(401, "未提供访问令牌，或访问令牌无效、过期或已失效。");

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties) =>
        WriteErrorAsync(403, "当前账号没有执行此操作的权限。");

    private Task WriteErrorAsync(int statusCode, string message)
    {
        Response.StatusCode = statusCode;
        Response.ContentType = "application/json; charset=utf-8";
        return JsonSerializer.SerializeAsync(Response.Body, ApiResponse<object>.Fail(ApiErrorCodes.FromHttpStatus(statusCode), message), new JsonSerializerOptions { PropertyNamingPolicy = null });
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

        operation.Responses.TryAdd("401", new OpenApiResponse { Description = "未提供访问令牌，或访问令牌无效、过期或已失效。" });
        operation.Responses.TryAdd("403", new OpenApiResponse { Description = "当前角色没有执行此操作所需的权限。" });
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
