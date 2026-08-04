using HomeMind.Business.IServices.Base;
using HomeMind.Common.Helpers;
using HomeMind.Common.Infrastructure;
using HomeMind.Common.Model.Entities;
using HomeMind.Common.Model.ViewModel.Data.Base;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;

namespace HomeMind.Business.Services.Base;

/// <summary>账户认证业务实现。控制器只负责 HTTP 协议与返回码。</summary>
public sealed class BaseUserServices : IBaseUserServices
{
    private readonly HomeMindDbContext _db;
    private readonly TokenService _tokens;

    public BaseUserServices(HomeMindDbContext db, TokenService tokens)
    {
        _db = db;
        _tokens = tokens;
    }

    public async Task<AuthenticationResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Phone) || string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
            return new AuthenticationResult(422, "请输入手机号和至少 8 位的密码。", null);

        var phoneHash = DbValue.Sha256(request.Phone.Trim());
        var existing = await FindLoginAsync(phoneHash, cancellationToken);
        if (existing is not null)
        {
            return PasswordHasher.Verify(request.Password, existing.PasswordHash)
                ? await CreateSucceededResultAsync(existing.UserId, existing.TenantId, request.InstallationId, request.Platform, cancellationToken: cancellationToken)
                : new AuthenticationResult(409, "该手机号已绑定其他账号。", null);
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var user = new User { DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? "HomeMind 用户" : request.DisplayName.Trim() };
        _db.Users.Add(user);
        await _db.SaveChangesAsync(cancellationToken);

        var tenant = new Tenant
        {
            TenantType = "personal",
            Code = $"user-{user.Id}",
            Name = $"个人空间 {user.Id}",
            Status = "active",
            OwnerUserId = user.Id
        };
        _db.UserIdentities.Add(new UserIdentity { UserId = user.Id, Provider = "phone", Issuer = "sms", SubjectKind = "e164", SubjectHash = phoneHash, VerifiedAt = DateTime.UtcNow, IsPrimary = true });
        _db.PasswordCredentials.Add(new PasswordCredential { UserId = user.Id, PasswordHash = PasswordHasher.Hash(request.Password) });
        _db.Tenants.Add(tenant);
        await _db.SaveChangesAsync(cancellationToken);
        _db.TenantMembers.Add(new TenantMember { TenantId = tenant.Id, UserId = user.Id, Role = "owner", Status = "active" });
        var result = await CreateSucceededResultAsync(user.Id, tenant.Id, request.InstallationId, request.Platform, cancellationToken: cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<AuthenticationResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Phone) || string.IsNullOrWhiteSpace(request.Password))
            return new AuthenticationResult(401, "手机号或密码错误。", null);
        var row = await FindLoginAsync(DbValue.Sha256(request.Phone.Trim()), cancellationToken);
        return row is null || !PasswordHasher.Verify(request.Password, row.PasswordHash)
            ? new AuthenticationResult(401, "手机号或密码错误。", null)
            : await CreateSucceededResultAsync(row.UserId, row.TenantId, request.InstallationId, request.Platform, cancellationToken: cancellationToken);
    }

    public async Task<AuthenticationResult> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken)) return new AuthenticationResult(401, "刷新令牌无效或已过期。", null);
        var now = DateTime.UtcNow;
        var tokenHash = DbValue.Sha256(request.RefreshToken);
        var row = await (from refresh in _db.AuthRefreshTokens
                         join tenant in _db.Tenants on refresh.UserId equals tenant.OwnerUserId
                         where refresh.TokenHash == tokenHash && refresh.RevokedAt == null && refresh.ExpiresAt > now
                               && tenant.TenantType == "personal" && tenant.Status == "active"
                         select new RefreshRow(refresh, tenant.Id)).SingleOrDefaultAsync(cancellationToken);
        if (row is null) return new AuthenticationResult(401, "刷新令牌无效或已过期。", null);

        row.Token.RevokedAt = now;
        row.Token.RevokeReason = "rotated";
        row.Token.LastUsedAt = now;
        return await CreateSucceededResultAsync(row.Token.UserId, row.TenantId, null, null, row.Token.DeviceId, row.Token.FamilyId, cancellationToken);
    }

    public async Task<BaseUserViewModel?> GetCurrentUserAsync(long userId, CancellationToken cancellationToken = default) =>
        await _db.Users.Where(x => x.Id == userId && x.DeletedAt == null)
            .Select(x => new BaseUserViewModel(x.Id, x.DisplayName, x.AvatarUrl, x.Status, x.Timezone, x.Locale, x.CreatedAt))
            .SingleOrDefaultAsync(cancellationToken);

    private Task<LoginRow?> FindLoginAsync(byte[] phoneHash, CancellationToken cancellationToken) =>
        (from identity in _db.UserIdentities
         join credential in _db.PasswordCredentials on identity.UserId equals credential.UserId
         join tenant in _db.Tenants on identity.UserId equals tenant.OwnerUserId
         join account in _db.Users on identity.UserId equals account.Id
         where identity.Provider == "phone" && identity.SubjectHash == phoneHash && identity.RevokedAt == null
               && account.Status == "active" && tenant.TenantType == "personal" && tenant.Status == "active"
         select new LoginRow(identity.UserId, credential.PasswordHash, tenant.Id)).SingleOrDefaultAsync(cancellationToken);

    private async Task<AuthenticationResult> CreateSucceededResultAsync(long userId, long tenantId, string? installationId, string? platform, long? existingDeviceId = null, string? familyId = null, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var deviceId = existingDeviceId;
        if (deviceId is null)
        {
            var installation = Guid.TryParse(installationId, out var parsed) ? parsed.ToString() : Guid.NewGuid().ToString();
            var device = await _db.AuthDevices.SingleOrDefaultAsync(x => x.UserId == userId && x.InstallationId == installation, cancellationToken);
            if (device is null)
            {
                device = new AuthDevice { UserId = userId, InstallationId = installation, Platform = string.IsNullOrWhiteSpace(platform) ? "h5" : platform, LastSeenAt = now };
                _db.AuthDevices.Add(device);
            }
            else
            {
                device.Platform = string.IsNullOrWhiteSpace(platform) ? device.Platform : platform;
                device.LastSeenAt = now;
            }
            await _db.SaveChangesAsync(cancellationToken);
            deviceId = device.Id;
        }

        var refresh = DbValue.RandomToken();
        _db.AuthRefreshTokens.Add(new AuthRefreshToken { UserId = userId, DeviceId = deviceId.Value, FamilyId = familyId ?? Guid.NewGuid().ToString(), TokenHash = DbValue.Sha256(refresh), ExpiresAt = now.AddDays(_tokens.RefreshTokenDays) });
        await _db.SaveChangesAsync(cancellationToken);
        return new AuthenticationResult(200, "登录成功。", new AuthSessionViewModel(_tokens.CreateAccessToken(userId, tenantId, deviceId.Value), refresh, userId, tenantId));
    }

    private sealed record LoginRow(long UserId, string PasswordHash, long TenantId);
    private sealed record RefreshRow(AuthRefreshToken Token, long TenantId);
}
