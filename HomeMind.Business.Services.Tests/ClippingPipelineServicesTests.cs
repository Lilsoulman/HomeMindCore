using HomeMind.Business.IServices.Media;
using HomeMind.Business.Services.Media;
using HomeMind.Common.Model.Entities;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace HomeMind.Business.Services.Tests;

/// <summary>B36 剪辑流水线定向测试：未配置不得伪造成功，Seedance 必须满足四重门禁。</summary>
public sealed class ClippingPipelineServicesTests
{
    /// <summary>未配置的首个引擎将任务置失败，并仅写失败阶段事件。</summary>
    [Fact]
    public async Task ProcessNext_UnconfiguredEngine_FailsWithoutSucceededEvent()
    {
        await using var db = CreateDb();
        db.ClippingTasks.Add(new ClippingTask { Id = 1, TenantId = 1, RunId = 10, CreatedByUserId = 7, Status = ClippingTaskStatus.Generating, EngineStage = "video_use", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var services = new ClippingPipelineServices(db, [], Configuration());

        Assert.Equal(1, await services.ProcessNextAsync());
        var task = await db.ClippingTasks.SingleAsync();
        Assert.Equal(ClippingTaskStatus.Failed, task.Status);
        var events = await db.RunEvents.ToListAsync();
        Assert.Single(events);
        Assert.Contains("\"status\":\"failed\"", events[0].Payload);
        Assert.DoesNotContain("succeeded", events[0].Payload);
    }

    /// <summary>Seedance 未满足逐任务授权时只写跳过事件，不调用引擎。</summary>
    [Fact]
    public async Task ProcessNext_SeedanceWithoutFourGates_SkipsEngine()
    {
        await using var db = CreateDb();
        db.ClippingTasks.Add(new ClippingTask { Id = 1, TenantId = 1, RunId = 10, CreatedByUserId = 7, Status = ClippingTaskStatus.Generating, EngineStage = "seedance", CurrentPlan = "{}", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var services = new ClippingPipelineServices(db, [new SuccessfulEngine("hyperframes"), new SuccessfulEngine("remotion")], Configuration());

        await services.ProcessNextAsync();
        var events = await db.RunEvents.ToListAsync();
        Assert.Contains(events, item => item.Payload.Contains("\"stage\":\"seedance\"") && item.Payload.Contains("\"status\":\"skipped\""));
    }

    /// <summary>创建独立内存数据库。</summary>
    private static HomeMindDbContext CreateDb() => new(new DbContextOptionsBuilder<HomeMindDbContext>().UseInMemoryDatabase($"hm-b36-{Guid.NewGuid()}").Options);

    /// <summary>创建默认关闭的引擎配置。</summary>
    private static IConfiguration Configuration() => new ConfigurationBuilder().AddInMemoryCollection().Build();

    /// <summary>用于验证后续阶段流转的成功引擎。</summary>
    private sealed class SuccessfulEngine : IClippingEngine
    {
        /// <summary>阶段标识。</summary>
        public string Stage { get; }
        /// <summary>构造成功引擎。</summary>
        public SuccessfulEngine(string stage) => Stage = stage;
        /// <summary>返回健康成功。</summary>
        public Task<ClippingEngineResult> CheckHealthAsync(CancellationToken cancellationToken = default) => Task.FromResult(new ClippingEngineResult(true, "健康检查通过。"));
        /// <summary>返回执行成功。</summary>
        public Task<ClippingEngineResult> ExecuteAsync(CancellationToken cancellationToken = default) => Task.FromResult(new ClippingEngineResult(true, "处理完成。"));
    }
}
