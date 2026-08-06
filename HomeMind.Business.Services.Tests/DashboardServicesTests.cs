using HomeMind.Business.Services.Dashboard;
using HomeMind.Common.Model.Entities.Steward;
using HomeMind.Common.Model.ViewModel.Data.Dashboard;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HomeMind.Business.Services.Tests;

/// <summary>仪表板 V2.2 聚合定向测试：待确认事项模块、管家动态模块与模块降级。</summary>
public class DashboardServicesTests
{
    /// <summary>待确认事项模块只返回未过期且仍 pending 的确认项，排除过期与已确认项。</summary>
    [Fact]
    public async Task PendingConfirmations_Module_Returns_Only_Pending_Unexpired()
    {
        await using var db = NewDb("dash-confirmations");
        var services = new DashboardServices(db);
        SeedConfirmation(db, 1, ConfirmationRiskLevel.L1, ConfirmationItemStatus.Pending, "未过期待确认", null);
        SeedConfirmation(db, 1, ConfirmationRiskLevel.L2, ConfirmationItemStatus.Pending, "已过期待确认", DateTime.UtcNow.AddMinutes(-1));
        SeedConfirmation(db, 1, ConfirmationRiskLevel.L3, ConfirmationItemStatus.Confirmed, "已确认项", null);
        await db.SaveChangesAsync();

        var result = await services.GetAsync(1, 1);
        Assert.True(result.Succeeded);
        var view = Assert.IsType<DashboardView>(result.Data);
        Assert.Equal("available", view.PendingConfirmations.Status);
        var item = Assert.Single(view.PendingConfirmations.Data);
        Assert.Equal("未过期待确认", item.Title);
    }

    /// <summary>管家动态模块返回最近动态，且跨家庭活动被隔离。</summary>
    [Fact]
    public async Task StewardActivities_Module_Returns_Recent_Activities()
    {
        await using var db = NewDb("dash-activities");
        var services = new DashboardServices(db);
        SeedActivity(db, 1, "本家庭动态");
        SeedActivity(db, 2, "跨家庭动态");
        await db.SaveChangesAsync();

        var result = await services.GetAsync(1, 1);
        Assert.True(result.Succeeded);
        var view = Assert.IsType<DashboardView>(result.Data);
        Assert.Equal("available", view.StewardActivities.Status);
        var item = Assert.Single(view.StewardActivities.Data);
        Assert.Equal("本家庭动态", item.Title);
    }

    /// <summary>单模块读取失败时降级为 unavailable 并置位 PartialFailure，整体响应仍成功。</summary>
    [Fact]
    public async Task Module_Failure_Degrades_To_Unavailable()
    {
        var db = NewDb("dash-degrade");
        await db.DisposeAsync();
        var services = new DashboardServices(db);

        var result = await services.GetAsync(1, 1);
        Assert.True(result.Succeeded);
        var view = Assert.IsType<DashboardView>(result.Data);
        Assert.True(view.PartialFailure);
        Assert.Equal("unavailable", view.Home.Status);
    }

    private static HomeMindDbContext NewDb(string name) =>
        new(new DbContextOptionsBuilder<HomeMindDbContext>()
            .UseInMemoryDatabase($"hm-b12-dashboard-{name}-{Guid.NewGuid()}")
            .Options);

    /// <summary>构造一条管家动态。</summary>
    private static void SeedActivity(HomeMindDbContext db, long homeId, string title)
    {
        var now = DateTime.UtcNow;
        db.StewardActivities.Add(new StewardActivity
        {
            HomeId = homeId,
            Category = StewardActivityCategory.Reporting,
            Title = title,
            RiskLevel = ConfirmationRiskLevel.L1,
            Status = StewardActivityStatus.Completed,
            CreatedAt = now,
            UpdatedAt = now
        });
    }

    /// <summary>构造一条确认项。</summary>
    private static void SeedConfirmation(HomeMindDbContext db, long homeId, string riskLevel, string status, string title, DateTime? expiresAt)
    {
        var now = DateTime.UtcNow;
        db.ConfirmationItems.Add(new ConfirmationItem
        {
            HomeId = homeId,
            RiskLevel = riskLevel,
            Title = title,
            Status = status,
            ExpiresAt = expiresAt,
            CreatedAt = now,
            UpdatedAt = now
        });
    }
}
