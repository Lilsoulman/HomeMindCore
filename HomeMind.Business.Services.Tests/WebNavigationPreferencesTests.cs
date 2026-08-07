using HomeMind.Business.IServices.Family;
using HomeMind.Business.Services.Identity;
using HomeMind.Common.Infrastructure.Constants;
using HomeMind.Common.Model.Entities;
using HomeMind.Common.Model.Entities.Family;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.Identity;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HomeMind.Business.Services.Tests;

/// <summary>Web 导航偏好服务定向测试：白名单合并、未发布 route_key 拒绝、偏好覆盖与审计。</summary>
public class WebNavigationPreferencesTests
{
    /// <summary>无偏好时返回全部白名单 route_key，默认 enabled=true 且按默认 sort_order。</summary>
    [Fact]
    public async Task List_Merges_Defaults_When_No_Preferences()
    {
        await using var db = NewDb("nav-default");
        var audit = new FakeAuditLogger();
        Seed(db, tenantId: 1);
        await db.SaveChangesAsync();
        var services = new WebNavigationPreferencesServices(db, audit);

        var result = await services.GetForCurrentAsync(1, "member", default);

        var view = Assert.IsType<WebNavigationPreferencesView>(result.Data);
        Assert.Equal(NexusWebNavigationKeys.All.Count, view.Routes.Count);
        Assert.All(view.Routes, r => Assert.True(r.Enabled));
        Assert.All(view.Routes, r => Assert.False(r.IsCustomized));
    }

    /// <summary>持久化偏好覆盖默认值。</summary>
    [Fact]
    public async Task List_Merges_Persisted_Overrides()
    {
        await using var db = NewDb("nav-override");
        var audit = new FakeAuditLogger();
        Seed(db, tenantId: 1);
        db.WebNavigationPreferences.Add(new WebNavigationPreference
        {
            TenantId = 1, Role = "member", RouteKey = NexusWebNavigationKeys.TenantLife, Enabled = false, SortOrder = 5,
            UpdatedByUserId = 1, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var services = new WebNavigationPreferencesServices(db, audit);

        var result = await services.GetForCurrentAsync(1, "member", default);

        var view = Assert.IsType<WebNavigationPreferencesView>(result.Data);
        var life = view.Routes.Single(x => x.RouteKey == NexusWebNavigationKeys.TenantLife);
        Assert.False(life.Enabled);
        Assert.Equal(5, life.SortOrder);
        Assert.True(life.IsCustomized);
    }

    /// <summary>未发布 route_key 返回 422。</summary>
    [Fact]
    public async Task Update_Rejects_Unknown_Route_Key()
    {
        await using var db = NewDb("nav-unknown");
        var audit = new FakeAuditLogger();
        Seed(db, tenantId: 1);
        await db.SaveChangesAsync();
        var services = new WebNavigationPreferencesServices(db, audit);

        var result = await services.UpdateForRoleAsync(1, 1, new WebNavigationPreferencesUpdateRequest
        {
            TargetRole = "member",
            Items = new[] { new WebNavigationPreferenceUpdateItem { RouteKey = "tenant.not_published", Enabled = true, SortOrder = 1 } }
        }, default);

        Assert.Equal(422, result.StatusCode);
        Assert.Equal(ApiErrorCodes.WebNavigationRouteKeyNotPublished, result.Code);
    }

    /// <summary>owner/admin 写入成功并写 web_navigation_preference_updated 审计。</summary>
    [Fact]
    public async Task Update_Writes_Preference_Audit()
    {
        await using var db = NewDb("nav-write");
        var audit = new FakeAuditLogger();
        Seed(db, tenantId: 1);
        await db.SaveChangesAsync();
        var services = new WebNavigationPreferencesServices(db, audit);

        var result = await services.UpdateForRoleAsync(1, 1, new WebNavigationPreferencesUpdateRequest
        {
            TargetRole = "member",
            Items = new[] { new WebNavigationPreferenceUpdateItem { RouteKey = NexusWebNavigationKeys.TenantLife, Enabled = false, SortOrder = 3 } }
        }, default);

        Assert.True(result.Succeeded);
        Assert.Equal(FamilyAuditActions.WebNavigationPreferenceUpdated, audit.LastAction);
        Assert.Equal(FamilyAuditTargetTypes.WebNavigationPreference, audit.LastTargetType);
    }

    private static HomeMindDbContext NewDb(string name) =>
        new(new DbContextOptionsBuilder<HomeMindDbContext>()
            .UseInMemoryDatabase($"hm-b19-nav-{name}-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private static void Seed(HomeMindDbContext db, long tenantId)
    {
        db.Tenants.Add(new Tenant { Id = tenantId, TenantType = "personal", Code = $"t{tenantId}", Name = $"家庭{tenantId}", Status = "active", OwnerUserId = 1, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        db.Users.Add(new User { Id = 1, DisplayName = "管理员", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
    }

    private sealed class FakeAuditLogger : IFamilyAuditLogger
    {
        public string? LastAction { get; private set; }
        public string? LastTargetType { get; private set; }

        public Task<bool> LogAsync(long homeId, long? actorUserId, string action, string targetType, long? targetId, object? before, object? after, string? reason, long? relatedRunId, CancellationToken cancellationToken = default)
        {
            LastAction = action;
            LastTargetType = targetType;
            return Task.FromResult(true);
        }
    }
}
