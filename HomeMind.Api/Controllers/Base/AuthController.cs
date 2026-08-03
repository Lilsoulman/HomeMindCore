using System;
using System.Threading.Tasks;
using Dapper;
using HomeMind.Api.Controllers.Base;
using HomeMind.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace HomeMind.Api.Controllers.Base;

/// <summary>
/// 身份认证模块，负责账户注册、登录、令牌续期和当前用户信息查询。
/// </summary>
[Route("api/v1/auth")]
public sealed class AuthController : ApiControllerBase
{
    private readonly MySqlConnectionFactory _connections;
    private readonly TokenService _tokens;
    private readonly AccessTokenValidator _accessTokens;

    public AuthController(MySqlConnectionFactory connections, TokenService tokens, AccessTokenValidator accessTokens)
    {
        _connections = connections;
        _tokens = tokens;
        _accessTokens = accessTokens;
    }

    /// <summary>
    /// 使用手机号和密码注册个人账户，并返回访问令牌与刷新令牌。
    /// </summary>
    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<ActionResult<ApiResponse<AuthResult>>> Register(RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Phone) || string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
            return BadRequest(ApiResponse<AuthResult>.Fail(422, "Phone and an 8-character password are required."));

        await using var db = _connections.Open();
        await db.OpenAsync();
        await using var tx = await db.BeginTransactionAsync();
        try
        {
            var userId = await db.QuerySingleAsync<long>("INSERT INTO users(display_name) VALUES (@Name); SELECT LAST_INSERT_ID();", new { Name = string.IsNullOrWhiteSpace(request.DisplayName) ? "HomeMind user" : request.DisplayName.Trim() }, tx);
            await db.ExecuteAsync("INSERT INTO user_identities(user_id,provider,issuer,subject_kind,subject_hash,verified_at,is_primary) VALUES (@UserId,'phone','sms','e164',UNHEX(SHA2(@Phone,256)),UTC_TIMESTAMP(3),1)", new { UserId = userId, Phone = request.Phone.Trim() }, tx);
            await db.ExecuteAsync("INSERT INTO password_credentials(user_id,password_hash) VALUES (@UserId,@Hash)", new { UserId = userId, Hash = PasswordHasher.Hash(request.Password) }, tx);
            var tenantId = await db.QuerySingleAsync<long>("INSERT INTO tenants(tenant_type,code,name,status,owner_user_id) VALUES ('personal',CONCAT('user-',@UserId),CONCAT('Personal workspace ',@UserId),'active',@UserId); SELECT LAST_INSERT_ID();", new { UserId = userId }, tx);
            await db.ExecuteAsync("INSERT INTO tenant_members(tenant_id,user_id,role,status) VALUES (@TenantId,@UserId,'owner','active')", new { TenantId = tenantId, UserId = userId }, tx);
            var result = await IssueSession(db, tx, userId, tenantId, request.InstallationId, request.Platform);
            await tx.CommitAsync();
            return Ok(ApiResponse<AuthResult>.Ok(result));
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        {
            await tx.RollbackAsync();
            return Conflict(ApiResponse<AuthResult>.Fail(409, "This phone number is already bound to an account."));
        }
    }

    /// <summary>
    /// 使用手机号和密码登录，创建或更新当前设备会话。
    /// </summary>
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<AuthResult>>> Login(LoginRequest request)
    {
        await using var db = _connections.Open();
        var row = await db.QuerySingleOrDefaultAsync<LoginRow>("SELECT u.id UserId, p.password_hash PasswordHash, t.id TenantId FROM user_identities i JOIN users u ON u.id=i.user_id JOIN password_credentials p ON p.user_id=u.id JOIN tenants t ON t.owner_user_id=u.id AND t.tenant_type='personal' AND t.status='active' WHERE i.provider='phone' AND i.subject_hash=UNHEX(SHA2(@Phone,256)) AND i.revoked_at IS NULL AND u.status='active' LIMIT 1", new { Phone = request.Phone.Trim() });
        if (row is null || !PasswordHasher.Verify(request.Password, row.PasswordHash)) return Unauthorized(ApiResponse<AuthResult>.Fail(401, "Invalid phone or password."));
        await db.OpenAsync();
        await using var tx = await db.BeginTransactionAsync();
        var result = await IssueSession(db, tx, row.UserId, row.TenantId, request.InstallationId, request.Platform);
        await tx.CommitAsync();
        return Ok(ApiResponse<AuthResult>.Ok(result));
    }

    /// <summary>
    /// 使用有效的刷新令牌换取新的访问令牌和刷新令牌。
    /// </summary>
    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<ActionResult<ApiResponse<AuthResult>>> Refresh(RefreshRequest request)
    {
        await using var db = _connections.Open();
        await db.OpenAsync();
        await using var tx = await db.BeginTransactionAsync();
        var row = await db.QuerySingleOrDefaultAsync<RefreshRow>("SELECT r.id TokenId,r.user_id UserId,r.device_id DeviceId,r.family_id FamilyId,t.id TenantId FROM auth_refresh_tokens r JOIN tenants t ON t.owner_user_id=r.user_id AND t.tenant_type='personal' AND t.status='active' WHERE r.token_hash=UNHEX(SHA2(@Token,256)) AND r.revoked_at IS NULL AND r.expires_at>UTC_TIMESTAMP(3) LIMIT 1", new { Token = request.RefreshToken }, tx);
        if (row is null) { await tx.RollbackAsync(); return Unauthorized(ApiResponse<AuthResult>.Fail(401, "Refresh token is invalid or expired.")); }
        await db.ExecuteAsync("UPDATE auth_refresh_tokens SET revoked_at=UTC_TIMESTAMP(3),revoke_reason='rotated',last_used_at=UTC_TIMESTAMP(3) WHERE id=@TokenId", row, tx);
        var result = await IssueSession(db, tx, row.UserId, row.TenantId, null, null, row.DeviceId, row.FamilyId);
        await tx.CommitAsync();
        return Ok(ApiResponse<AuthResult>.Ok(result));
    }

    /// <summary>
    /// 获取当前访问令牌对应用户的基础资料。
    /// </summary>
    [Authorize(Policy = PermissionNames.IdentityRead)]
    [HttpGet("me")]
    public async Task<ActionResult<ApiResponse<object>>> Me()
    {
        if (!TryGetUser(out var user)) return UnauthorizedResult<object>();
        await using var db = _connections.Open();
        var result = await db.QuerySingleOrDefaultAsync("SELECT id,display_name displayName,avatar_url avatarUrl,status,timezone,locale,created_at createdAt FROM users WHERE id=@UserId AND deleted_at IS NULL", new { user.UserId });
        return result is null ? NotFoundResult<object>() : Ok(ApiResponse<object>.Ok(result));
    }

    /// <summary>
    /// 注销当前设备会话，并立即撤销当前访问令牌和该设备的刷新令牌。
    /// </summary>
    [Authorize(Policy = PermissionNames.IdentityRead)]
    [HttpPost("logout")]
    public async Task<ActionResult<ApiResponse<object>>> Logout()
    {
        if (!TryGetUser(out var user)) return UnauthorizedResult<object>();
        await _accessTokens.RevokeAsync(user);
        return Ok(ApiResponse<object>.Ok(new { loggedOut = true }));
    }

    /// <summary>
    /// 交换微信授权码；在微信应用配置完成前该接口返回未实现状态。
    /// </summary>
    [AllowAnonymous]
    [HttpPost("wechat/exchange")]
    public ActionResult<ApiResponse<object>> WeChatExchange() => StatusCode(501, ApiResponse<object>.Fail(501, "WeChat AppId, secret and callback configuration are required before code exchange can be enabled."));

    private async Task<AuthResult> IssueSession(MySqlConnection db, System.Data.Common.DbTransaction tx, long userId, long tenantId, string? installationId, string? platform, long? existingDeviceId = null, string? familyId = null)
    {
        var deviceId = existingDeviceId;
        if (deviceId is null)
        {
            var installation = Guid.TryParse(installationId, out var parsed) ? parsed.ToString() : Guid.NewGuid().ToString();
            deviceId = await db.QuerySingleAsync<long>("INSERT INTO auth_devices(user_id,installation_id,platform,last_seen_at) VALUES (@UserId,@Installation,@Platform,UTC_TIMESTAMP(3)) ON DUPLICATE KEY UPDATE id=LAST_INSERT_ID(id),last_seen_at=UTC_TIMESTAMP(3),platform=VALUES(platform); SELECT LAST_INSERT_ID();", new { UserId = userId, Installation = installation, Platform = string.IsNullOrWhiteSpace(platform) ? "h5" : platform }, tx);
        }
        var refresh = DbValue.RandomToken();
        await db.ExecuteAsync("INSERT INTO auth_refresh_tokens(user_id,device_id,family_id,token_hash,expires_at) VALUES (@UserId,@DeviceId,@FamilyId,UNHEX(SHA2(@Refresh,256)),DATE_ADD(UTC_TIMESTAMP(3),INTERVAL @Days DAY))", new { UserId = userId, DeviceId = deviceId, FamilyId = familyId ?? Guid.NewGuid().ToString(), Refresh = refresh, Days = _tokens.RefreshTokenDays }, tx);
        return new AuthResult(_tokens.CreateAccessToken(userId, tenantId, deviceId.Value), refresh, userId, tenantId);
    }

    public sealed record RegisterRequest(string Phone, string Password, string? DisplayName, string? InstallationId, string? Platform);
    public sealed record LoginRequest(string Phone, string Password, string? InstallationId, string? Platform);
    public sealed record RefreshRequest(string RefreshToken);
    public sealed record AuthResult(string AccessToken, string RefreshToken, long UserId, long TenantId);
    private sealed record LoginRow(long UserId, string PasswordHash, long TenantId);
    private sealed record RefreshRow(long TokenId, long UserId, long DeviceId, string FamilyId, long TenantId);
}
