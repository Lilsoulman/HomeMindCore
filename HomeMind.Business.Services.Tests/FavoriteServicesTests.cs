using HomeMind.Business.IServices.Family;
using HomeMind.Business.Services.Life;
using HomeMind.Common.Model.Entities;
using HomeMind.Common.Model.Entities.Family;
using HomeMind.Common.Model.Entities.Life;
using HomeMind.Common.Model.ViewModel.Data.Life;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HomeMind.Business.Services.Tests;

/// <summary>个人偏好收藏服务定向测试：可见性过滤、软删除、写权限与审计动作。</summary>
public class FavoriteServicesTests
{
    /// <summary>private 收藏仅归属成员本人可见，family 收藏家庭内可读。</summary>
    [Fact]
    public async Task List_Applies_Visibility_Filter_For_Private_Favorites()
    {
        await using var db = NewDb("favorites-visibility");
        var audit = new FakeAuditLogger();
        SeedMember(db, memberId: 1, createdBy: 10);
        SeedMember(db, memberId: 2, createdBy: 20);
        SeedFavorite(db, id: 100, ownerMemberId: 1, visibility: "private");
        SeedFavorite(db, id: 101, ownerMemberId: 2, visibility: "private");
        SeedFavorite(db, id: 102, ownerMemberId: 2, visibility: "family");
        var services = new FavoriteService(db, audit);

        var result = await services.ListAsync(1, actorUserId: 10, category: null, visibility: null, default);

        Assert.True(result.Succeeded);
        using var document = System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(result.Data));
        var ids = document.RootElement.EnumerateArray().Select(x => x.GetProperty("Id").GetInt64()).ToArray();
        Assert.Equal(new[] { 100L, 102L }, ids.OrderBy(x => x));
    }

    /// <summary>软删除后列表不再返回，且写入 favorite_delete 审计。</summary>
    [Fact]
    public async Task Delete_Soft_Deletes_And_Writes_Audit()
    {
        await using var db = NewDb("favorites-delete");
        var audit = new FakeAuditLogger();
        SeedMember(db, memberId: 1, createdBy: 10);
        SeedFavorite(db, id: 100, ownerMemberId: 1, visibility: "private");
        var services = new FavoriteService(db, audit);

        var result = await services.DeleteAsync(1, actorUserId: 10, favoriteId: 100, default);

        Assert.True(result.Succeeded);
        var deleted = await db.PersonalFavorites.SingleAsync(x => x.Id == 100);
        Assert.NotNull(deleted.DeletedAt);
        Assert.Equal(FamilyAuditActions.FavoriteDelete, audit.LastAction);
        Assert.Equal(FamilyAuditTargetTypes.PersonalFavorite, audit.LastTargetType);
    }

    /// <summary>非归属成员不能修改他人的 private 收藏。</summary>
    [Fact]
    public async Task Update_Forbids_Non_Owner_Without_Admin_Role()
    {
        await using var db = NewDb("favorites-forbid");
        var audit = new FakeAuditLogger();
        SeedMember(db, memberId: 1, createdBy: 10);
        SeedMember(db, memberId: 2, createdBy: 20);
        SeedFavorite(db, id: 100, ownerMemberId: 1, visibility: "private");
        db.TenantMembers.Add(new TenantMember { TenantId = 1, UserId = 20, Role = "member", Status = "active", JoinedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var services = new FavoriteService(db, audit);

        var result = await services.UpdateAsync(1, actorUserId: 20, favoriteId: 100, new FavoriteUpdateRequest("改名"), default);

        Assert.Equal(403, result.StatusCode);
    }

    /// <summary>家庭管理员可以更新他人的收藏。</summary>
    [Fact]
    public async Task Update_Allows_Admin_To_Edit_Others_Favorite()
    {
        await using var db = NewDb("favorites-admin");
        var audit = new FakeAuditLogger();
        SeedMember(db, memberId: 1, createdBy: 10);
        SeedMember(db, memberId: 2, createdBy: 20);
        SeedFavorite(db, id: 100, ownerMemberId: 1, visibility: "private");
        db.TenantMembers.Add(new TenantMember { TenantId = 1, UserId = 20, Role = "admin", Status = "active", JoinedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var services = new FavoriteService(db, audit);

        var result = await services.UpdateAsync(1, actorUserId: 20, favoriteId: 100, new FavoriteUpdateRequest("管理员改名"), default);

        Assert.True(result.Succeeded);
        Assert.Equal(FamilyAuditActions.FavoriteUpdate, audit.LastAction);
    }

    /// <summary>对话导入写入 favorite_import 审计并保留来源原因。</summary>
    [Fact]
    public async Task Import_Writes_Import_Audit_With_Source()
    {
        await using var db = NewDb("favorites-import");
        var audit = new FakeAuditLogger();
        SeedMember(db, memberId: 1, createdBy: 10);
        var services = new FavoriteService(db, audit);

        var result = await services.ImportAsync(1, actorUserId: 10, new FavoriteImportRequest("restaurant", "老王面馆", Visibility: "private", Source: "小红书"), default);

        Assert.True(result.Succeeded);
        Assert.Equal(FamilyAuditActions.FavoriteImport, audit.LastAction);
        Assert.Contains("小红书", audit.LastReason);
    }

    private static HomeMindDbContext NewDb(string name) =>
        new(new DbContextOptionsBuilder<HomeMindDbContext>()
            .UseInMemoryDatabase($"hm-b15-favorites-{name}-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private static void SeedMember(HomeMindDbContext db, long memberId, long createdBy)
    {
        db.FamilyMembers.Add(new FamilyMember { Id = memberId, HomeId = 1, Name = $"成员{memberId}", Relation = "self", MemberStatus = "active", CreatedByUserId = createdBy, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        db.SaveChanges();
    }

    private static void SeedFavorite(HomeMindDbContext db, long id, long ownerMemberId, string visibility)
    {
        db.PersonalFavorites.Add(new PersonalFavorite
        {
            Id = id,
            HomeId = 1,
            OwnerMemberId = ownerMemberId,
            Category = "restaurant",
            Name = $"收藏{id}",
            Visibility = visibility,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        db.SaveChanges();
    }

    private sealed class FakeAuditLogger : IFamilyAuditLogger
    {
        public string? LastAction { get; private set; }
        public string? LastTargetType { get; private set; }
        public string? LastReason { get; private set; }

        public Task<bool> LogAsync(long homeId, long? actorUserId, string action, string targetType, long? targetId, object? before, object? after, string? reason, long? relatedRunId, CancellationToken cancellationToken = default)
        {
            LastAction = action;
            LastTargetType = targetType;
            LastReason = reason;
            return Task.FromResult(true);
        }
    }
}
