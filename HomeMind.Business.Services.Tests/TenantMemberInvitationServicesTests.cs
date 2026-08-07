using System.Security.Cryptography;
using System.Text;
using HomeMind.Business.IServices.Family;
using HomeMind.Business.Services.Identity;
using HomeMind.Common.Model.Entities;
using HomeMind.Common.Model.Entities.Family;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.Identity;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HomeMind.Business.Services.Tests;

/// <summary>家庭成员邀请服务定向测试：手机号哈希、7 天过期、同标识唯一、撤销、接受校验。</summary>
public class TenantMemberInvitationServicesTests
{
    /// <summary>创建邀请正确哈希手机号并设置 7 天过期。</summary>
    [Fact]
    public async Task Create_Hashes_Phone_And_Sets_7_Day_Expiry()
    {
        await using var db = NewDb("invite-create");
        var audit = new FakeAuditLogger();
        Seed(db, tenantId: 1);
        await db.SaveChangesAsync();
        var services = new TenantMemberInvitationServices(db, audit);

        var result = await services.CreateAsync(1, 1, new TenantMemberInvitationCreateRequest { Phone = "+8613800138000", ProposedRole = "member" }, default);

        Assert.True(result.Succeeded);
        var view = Assert.IsType<TenantMemberInvitationView>(result.Data);
        Assert.Equal("pending", view.Status);
        Assert.InRange(view.ExpiresAt - DateTime.UtcNow, TimeSpan.FromDays(6.99), TimeSpan.FromDays(7.01));
        Assert.Equal(Convert.ToHexString(Sha256("+8613800138000")), view.SubjectHashHex);
        Assert.Equal(FamilyAuditActions.TenantInvitationCreated, audit.LastAction);
    }

    /// <summary>同 (tenant_id, subject_hash) 已 pending 返回 409。</summary>
    [Fact]
    public async Task Create_Returns_409_When_Same_Subject_Hash_Pending()
    {
        await using var db = NewDb("invite-dup");
        var audit = new FakeAuditLogger();
        Seed(db, tenantId: 1);
        await db.SaveChangesAsync();
        var services = new TenantMemberInvitationServices(db, audit);

        var first = await services.CreateAsync(1, 1, new TenantMemberInvitationCreateRequest { Phone = "+8613800138000", ProposedRole = "member" }, default);
        Assert.True(first.Succeeded);

        var second = await services.CreateAsync(1, 1, new TenantMemberInvitationCreateRequest { Phone = "+8613800138000", ProposedRole = "admin" }, default);

        Assert.Equal(409, second.StatusCode);
        Assert.Equal(ApiErrorCodes.TenantInvitationConflict, second.Code);
    }

    /// <summary>列表按状态过滤，已过期项不写回填。</summary>
    [Fact]
    public async Task List_Filters_Expired_Without_Writing_Back()
    {
        await using var db = NewDb("invite-expired");
        var audit = new FakeAuditLogger();
        Seed(db, tenantId: 1);
        await db.SaveChangesAsync();
        var services = new TenantMemberInvitationServices(db, audit);

        await services.CreateAsync(1, 1, new TenantMemberInvitationCreateRequest { Phone = "+8613800138000", ProposedRole = "member" }, default);
        // 手工把第一条邀请置为已过期
        var invitation = await db.TenantMemberInvitations.SingleAsync();
        invitation.ExpiresAt = DateTime.UtcNow.AddDays(-1);
        await db.SaveChangesAsync();

        var result = await services.ListAsync(1, 1, "pending", default);

        var list = Assert.IsType<TenantMemberInvitationListView>(result.Data);
        Assert.Empty(list.Items);
        var untouched = await db.TenantMemberInvitations.SingleAsync();
        Assert.Equal("pending", untouched.Status);
    }

    /// <summary>撤销写审计并停止接受。</summary>
    [Fact]
    public async Task Revoke_Writes_Audit_And_Stops_Acceptance()
    {
        await using var db = NewDb("invite-revoke");
        var audit = new FakeAuditLogger();
        Seed(db, tenantId: 1);
        await db.SaveChangesAsync();
        var services = new TenantMemberInvitationServices(db, audit);

        var created = await services.CreateAsync(1, 1, new TenantMemberInvitationCreateRequest { Phone = "+8613800138000", ProposedRole = "member" }, default);
        var view = Assert.IsType<TenantMemberInvitationView>(created.Data);

        var revoke = await services.RevokeAsync(1, 1, view.Id, default);
        Assert.True(revoke.Succeeded);
        Assert.Equal(FamilyAuditActions.TenantInvitationRevoked, audit.LastAction);

        var accept = await services.AcceptAsync(1, new TenantMemberInvitationAcceptRequest { InvitationId = view.Id, Phone = "+8613800138000" }, default);
        Assert.Equal(409, accept.StatusCode);
    }

    /// <summary>接受匹配已验证手机号并创建 tenant_member。</summary>
    [Fact]
    public async Task Accept_Matches_Verified_Phone_Identity_And_Creates_Tenant_Member()
    {
        await using var db = NewDb("invite-accept-ok");
        var audit = new FakeAuditLogger();
        Seed(db, tenantId: 1);
        var hash = Sha256("+8613800138000");
        db.Users.Add(new User { Id = 2, DisplayName = "受邀人", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        db.UserIdentities.Add(new UserIdentity { UserId = 2, Provider = "phone", Issuer = "phone", SubjectKind = "phone_number", SubjectHash = hash, VerifiedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var services = new TenantMemberInvitationServices(db, audit);

        var created = await services.CreateAsync(1, 1, new TenantMemberInvitationCreateRequest { Phone = "+8613800138000", ProposedRole = "member" }, default);
        var view = Assert.IsType<TenantMemberInvitationView>(created.Data);

        var accept = await services.AcceptAsync(2, new TenantMemberInvitationAcceptRequest { InvitationId = view.Id, Phone = "+8613800138000" }, default);

        Assert.True(accept.Succeeded);
        var member = await db.TenantMembers.SingleAsync(x => x.UserId == 2);
        Assert.Equal(1, member.TenantId);
        Assert.Equal("member", member.Role);
        Assert.Equal(FamilyAuditActions.TenantInvitationAccepted, audit.LastAction);
    }

    /// <summary>未验证的手机号账户不能接受邀请。</summary>
    [Fact]
    public async Task Accept_Returns_404_When_Identity_Not_Verified()
    {
        await using var db = NewDb("invite-accept-unverified");
        var audit = new FakeAuditLogger();
        Seed(db, tenantId: 1);
        var hash = Sha256("+8613800138000");
        db.Users.Add(new User { Id = 2, DisplayName = "未验证", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        db.UserIdentities.Add(new UserIdentity { UserId = 2, Provider = "phone", Issuer = "phone", SubjectKind = "phone_number", SubjectHash = hash, VerifiedAt = DateTime.UtcNow, RevokedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var services = new TenantMemberInvitationServices(db, audit);

        var created = await services.CreateAsync(1, 1, new TenantMemberInvitationCreateRequest { Phone = "+8613800138000", ProposedRole = "member" }, default);
        var view = Assert.IsType<TenantMemberInvitationView>(created.Data);

        var accept = await services.AcceptAsync(2, new TenantMemberInvitationAcceptRequest { InvitationId = view.Id, Phone = "+8613800138000" }, default);

        Assert.Equal(404, accept.StatusCode);
        Assert.Equal(ApiErrorCodes.TenantInvitationIdentityNotMatched, accept.Code);
    }

    /// <summary>手机号哈希不匹配当前账户的邀请返回 404。</summary>
    [Fact]
    public async Task Accept_Rejects_Phone_Mismatch()
    {
        await using var db = NewDb("invite-accept-mismatch");
        var audit = new FakeAuditLogger();
        Seed(db, tenantId: 1);
        await db.SaveChangesAsync();
        var services = new TenantMemberInvitationServices(db, audit);

        var created = await services.CreateAsync(1, 1, new TenantMemberInvitationCreateRequest { Phone = "+8613800138000", ProposedRole = "member" }, default);
        var view = Assert.IsType<TenantMemberInvitationView>(created.Data);

        var accept = await services.AcceptAsync(1, new TenantMemberInvitationAcceptRequest { InvitationId = view.Id, Phone = "+8613900139000" }, default);

        Assert.Equal(404, accept.StatusCode);
        Assert.Equal(ApiErrorCodes.TenantInvitationIdentityNotMatched, accept.Code);
    }

    private static HomeMindDbContext NewDb(string name) =>
        new(new DbContextOptionsBuilder<HomeMindDbContext>()
            .UseInMemoryDatabase($"hm-b19-invitation-{name}-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private static void Seed(HomeMindDbContext db, long tenantId)
    {
        db.Tenants.Add(new Tenant { Id = tenantId, TenantType = "personal", Code = $"t{tenantId}", Name = $"家庭{tenantId}", Status = "active", OwnerUserId = 1, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        db.Users.Add(new User { Id = 1, DisplayName = "发起人", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
    }

    private static byte[] Sha256(string value) => SHA256.HashData(Encoding.UTF8.GetBytes(value));

    private sealed class FakeAuditLogger : IFamilyAuditLogger
    {
        public string? LastAction { get; private set; }

        public Task<bool> LogAsync(long homeId, long? actorUserId, string action, string targetType, long? targetId, object? before, object? after, string? reason, long? relatedRunId, CancellationToken cancellationToken = default)
        {
            LastAction = action;
            return Task.FromResult(true);
        }
    }
}
