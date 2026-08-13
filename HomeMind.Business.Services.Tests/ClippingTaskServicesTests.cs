using HomeMind.Business.Services.Media;
using HomeMind.Common.Model.Entities;
using HomeMind.Common.Model.ViewModel.Data.Media;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HomeMind.Business.Services.Tests;

/// <summary>V2.8 B35 任务查询定向测试：恢复版本历史且严格用户/租户隔离。</summary>
public sealed class ClippingTaskServicesTests
{
    [Fact]
    public async Task Get_Returns_Safe_History_And_Hides_Other_Owners()
    {
        await using var db = new HomeMindDbContext(new DbContextOptionsBuilder<HomeMindDbContext>()
            .UseInMemoryDatabase($"hm-b35-{Guid.NewGuid()}").Options);
        db.ClippingTasks.Add(new ClippingTask
        {
            Id = 10, TenantId = 1, CreatedByUserId = 7, Status = "reviewing", EngineStage = "planning",
            Materials = "[\"/safe/a.mp4\"]", Goal = "竖屏 30 秒", CurrentPlan = "{\"total_duration\":30}",
            VersionHistory = "[{\"version\":1,\"plan\":{\"total_duration\":30},\"change\":\"初始方案\",\"modifiedAt\":\"2026-08-13T00:00:00Z\"}]",
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var services = new ClippingTaskServices(db);

        var own = await services.GetAsync(7, 1, 10);
        var otherUser = await services.GetAsync(8, 1, 10);
        var otherTenant = await services.GetAsync(7, 2, 10);

        var view = Assert.IsType<ClippingTaskView>(own.Data);
        Assert.Equal("planning", view.EngineStage);
        Assert.Single(view.VersionHistory);
        Assert.Equal(404, otherUser.StatusCode);
        Assert.Equal(404, otherTenant.StatusCode);
    }
}
