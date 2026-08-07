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

/// <summary>家庭成员受控管理服务定向测试：角色变更、状态停启、owner 转让与最后一名 active owner 守恒。</summary>
public class TenantMemberServicesTests
{
    /// <summary>角色变更直接置 owner 必须被拒绝。</summary>
    [Fact]
    public async Task ChangeRole_Rejects_Direct_Owner_Assignment()
    {
        await using var db = NewDb("role-owner");
        var audit = new FakeAuditLogger();
        Seed(db, tenantId: 1, ownerUserId: 1);
        SeedMember(db, tenantId: 1, userId: 2, role: "member");
        await db.SaveChangesAsync();
        var services = new TenantMemberServices(db, audit);

        var result = await services.ChangeRoleAsync(1, 1, 2, new TenantMemberRoleUpdateRequest { NewRole = "owner", RowVersion = 1 }, default);

        Assert.Equal(422, result.StatusCode);
        Assert.Equal(ApiErrorCodes.TenantRoleOwnerDirectForbidden, result.Code);
    }

    /// <summary>角色变更乐观锁版本不匹配返回 409。</summary>
    [Fact]
    public async Task ChangeRole_Returns_409_When_RowVersion_Mismatch()
    {
        await using var db = NewDb("role-version");
        var audit = new FakeAuditLogger();
        Seed(db, tenantId: 1, ownerUserId: 1);
        SeedMember(db, tenantId: 1, userId: 2, role: "member");
        await db.SaveChangesAsync();
        var services = new TenantMemberServices(db, audit);

        var result = await services.ChangeRoleAsync(1, 1, 2, new TenantMemberRoleUpdateRequest { NewRole = "admin", RowVersion = 99 }, default);

        Assert.Equal(409, result.StatusCode);
        Assert.Equal(ApiErrorCodes.TenantOptimisticLockConflict, result.Code);
    }

    /// <summary>不能停用最后一名 active owner。</summary>
    [Fact]
    public async Task ChangeStatus_Rejects_Disabling_Last_Active_Owner()
    {
        await using var db = NewDb("status-last-owner");
        var audit = new FakeAuditLogger();
        Seed(db, tenantId: 1, ownerUserId: 1);
        SeedMember(db, tenantId: 1, userId: 1, role: "owner");
        await db.SaveChangesAsync();
        var services = new TenantMemberServices(db, audit);

        var result = await services.ChangeStatusAsync(1, 1, 1, new TenantMemberStatusUpdateRequest { NewStatus = "suspended", Reason = "测试", RowVersion = 1 }, default);

        Assert.Equal(422, result.StatusCode);
    }

    /// <summary>owner 转让同事务更新 tenants.owner_user_id 与双方角色。</summary>
    [Fact]
    public async Task TransferOwner_Updates_Tenant_And_Both_Members_Atomically()
    {
        await using var db = NewDb("transfer-ok");
        var audit = new FakeAuditLogger();
        Seed(db, tenantId: 1, ownerUserId: 1);
        SeedMember(db, tenantId: 1, userId: 1, role: "owner");
        SeedMember(db, tenantId: 1, userId: 2, role: "member");
        await db.SaveChangesAsync();
        var services = new TenantMemberServices(db, audit);

        var result = await services.TransferOwnerAsync(1, 1, new TenantOwnerTransferRequest { NewOwnerUserId = 2, RowVersion = 1 }, default);

        Assert.True(result.Succeeded);
        var tenant = await db.Tenants.SingleAsync(x => x.Id == 1);
        Assert.Equal(2, tenant.OwnerUserId);
        var oldOwner = await db.TenantMembers.SingleAsync(x => x.TenantId == 1 && x.UserId == 1);
        var newOwner = await db.TenantMembers.SingleAsync(x => x.TenantId == 1 && x.UserId == 2);
        Assert.Equal("admin", oldOwner.Role);
        Assert.Equal("owner", newOwner.Role);
        Assert.Equal(FamilyAuditActions.TenantOwnerTransferred, audit.LastAction);
    }

    /// <summary>非 owner 不能发起 owner 转让。</summary>
    [Fact]
    public async Task TransferOwner_Rejects_For_Non_Owner_Actor()
    {
        await using var db = NewDb("transfer-actor");
        var audit = new FakeAuditLogger();
        Seed(db, tenantId: 1, ownerUserId: 1);
        SeedMember(db, tenantId: 1, userId: 1, role: "owner");
        SeedMember(db, tenantId: 1, userId: 2, role: "member");
        await db.SaveChangesAsync();
        var services = new TenantMemberServices(db, audit);

        var result = await services.TransferOwnerAsync(1, 2, new TenantOwnerTransferRequest { NewOwnerUserId = 1, RowVersion = 1 }, default);

        Assert.Equal(403, result.StatusCode);
    }

    /// <summary>新 owner 必须为 active 状态。</summary>
    [Fact]
    public async Task TransferOwner_Rejects_New_Owner_Already_Suspended()
    {
        await using var db = NewDb("transfer-suspended");
        var audit = new FakeAuditLogger();
        Seed(db, tenantId: 1, ownerUserId: 1);
        SeedMember(db, tenantId: 1, userId: 1, role: "owner");
        SeedMember(db, tenantId: 1, userId: 2, role: "member", status: "suspended");
        await db.SaveChangesAsync();
        var services = new TenantMemberServices(db, audit);

        var result = await services.TransferOwnerAsync(1, 1, new TenantOwnerTransferRequest { NewOwnerUserId = 2, RowVersion = 1 }, default);

        Assert.Equal(422, result.StatusCode);
        Assert.Equal(ApiErrorCodes.OwnerTransferInvalidReceiver, result.Code);
    }

    private static HomeMindDbContext NewDb(string name) =>
        new(new DbContextOptionsBuilder<HomeMindDbContext>()
            .UseInMemoryDatabase($"hm-b19-member-{name}-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private static void Seed(HomeMindDbContext db, long tenantId, long ownerUserId)
    {
        db.Tenants.Add(new Tenant { Id = tenantId, TenantType = "personal", Code = $"t{tenantId}", Name = $"家庭{tenantId}", Status = "active", OwnerUserId = ownerUserId, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
    }

    private static void SeedMember(HomeMindDbContext db, long tenantId, long userId, string role, string status = "active")
    {
        db.TenantMembers.Add(new TenantMember { TenantId = tenantId, UserId = userId, Role = role, Status = status, JoinedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        db.Users.Add(new User { Id = userId, DisplayName = $"用户{userId}", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
    }

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
