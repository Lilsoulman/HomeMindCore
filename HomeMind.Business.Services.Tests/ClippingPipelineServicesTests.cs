using HomeMind.Business.IServices.Media;
using HomeMind.Business.Services.Media;
using HomeMind.Business.IServices.Expert;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.AI;
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
        var services = new ClippingPipelineServices(db, [], Configuration(), new FailedRender());

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
        var services = new ClippingPipelineServices(db, [new SuccessfulEngine("hyperframes"), new SuccessfulEngine("remotion")], Configuration(), new FailedRender());

        await services.ProcessNextAsync();
        var events = await db.RunEvents.ToListAsync();
        Assert.Contains(events, item => item.Payload.Contains("\"stage\":\"seedance\"") && item.Payload.Contains("\"status\":\"skipped\""));
    }

    /// <summary>B37：渲染成功后登记 mp4，并将任务、动作和运行一并推进到完成。</summary>
    [Fact]
    public async Task ProcessNext_Rendering_RegistersMp4AndCompletes()
    {
        await using var db = CreateDb();
        db.AgentRuns.Add(new AgentRun { Id = 10, TenantId = 1, UserId = 7, SourceType = "skill", RequestIdempotencyKey = Guid.NewGuid().ToString(), Input = "{}", Status = "running", Mode = "steward", AutoConfirmPolicy = "L3_only", PermissionSnapshot = "{}", StartedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow });
        var now = DateTime.UtcNow;
        db.ClippingTasks.AddRange(
            new ClippingTask { Id = 1, TenantId = 1, CreatedByUserId = 7, Status = ClippingTaskStatus.Generating, EngineStage = "planning", CreatedAt = now.AddMinutes(-1), UpdatedAt = now.AddMinutes(-1) },
            new ClippingTask { Id = 2, TenantId = 1, RunId = 10, CreatedByUserId = 7, Status = ClippingTaskStatus.Rendering, CurrentPlan = "{}", CreatedAt = now, UpdatedAt = now });
        db.ExpertRunActions.Add(new ExpertRunAction { Id = 20, RunId = 10, TenantId = 1, UserId = 7, ActionType = "draft_generate", RequestIdempotencyKey = Guid.NewGuid().ToString(), RequestJson = "{}", Status = "executing", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var files = new RenderFileServices();
        var services = new ClippingPipelineServices(db, [], Configuration(), new SuccessfulRender(), files);

        Assert.Equal(1, await services.ProcessNextAsync());
        Assert.Equal(1, files.RegisterCalls);
        Assert.Equal("video/mp4", files.MimeType);
        Assert.Equal(ClippingTaskStatus.Generating, (await db.ClippingTasks.FindAsync(1L))!.Status);
        Assert.Equal(ClippingTaskStatus.Done, (await db.ClippingTasks.FindAsync(2L))!.Status);
        Assert.Equal("completed", (await db.AgentRuns.SingleAsync()).Status);
        Assert.Equal("executed", (await db.ExpertRunActions.SingleAsync()).Status);
        Assert.Contains(await db.RunEvents.ToListAsync(), item => item.Payload.Contains("\"stage\":\"render\"") && item.Payload.Contains("\"status\":\"succeeded\""));
    }

    /// <summary>B37：渲染关闭或失败只写失败状态，绝不登记占位视频。</summary>
    [Fact]
    public async Task ProcessNext_RenderingFailure_DoesNotRegisterOrPretendSuccess()
    {
        await using var db = CreateDb();
        db.AgentRuns.Add(new AgentRun { Id = 10, TenantId = 1, UserId = 7, SourceType = "skill", RequestIdempotencyKey = Guid.NewGuid().ToString(), Input = "{}", Status = "running", Mode = "steward", AutoConfirmPolicy = "L3_only", PermissionSnapshot = "{}", StartedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow });
        db.ClippingTasks.Add(new ClippingTask { Id = 1, TenantId = 1, RunId = 10, CreatedByUserId = 7, Status = ClippingTaskStatus.Rendering, CurrentPlan = "{}", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var files = new RenderFileServices();
        var services = new ClippingPipelineServices(db, [], Configuration(), new FailedRender(), files);

        Assert.Equal(1, await services.ProcessNextAsync());
        Assert.Equal(0, files.RegisterCalls);
        Assert.Equal(ClippingTaskStatus.Failed, (await db.ClippingTasks.SingleAsync()).Status);
        Assert.Equal("failed", (await db.AgentRuns.SingleAsync()).Status);
        Assert.Contains(await db.RunEvents.ToListAsync(), item => item.Payload.Contains("failed"));
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

    /// <summary>返回固定 mp4 内容的渲染替身。</summary>
    private sealed class SuccessfulRender : IClippingRenderService
    {
        /// <inheritdoc />
        public bool IsEnabled => true;
        /// <inheritdoc />
        public Task<ClippingRenderResult> RenderAsync(string planJson, CancellationToken cancellationToken = default) => Task.FromResult(new ClippingRenderResult(true, "粗剪视频已生成。", "quick_edit_test.mp4", [1, 2, 3]));
    }

    /// <summary>模拟关闭或失败的渲染服务。</summary>
    private sealed class FailedRender : IClippingRenderService
    {
        /// <inheritdoc />
        public bool IsEnabled => false;
        /// <inheritdoc />
        public Task<ClippingRenderResult> RenderAsync(string planJson, CancellationToken cancellationToken = default) => Task.FromResult(new ClippingRenderResult(false, "粗剪渲染尚未启用。"));
    }

    /// <summary>记录生成文件登记参数的测试替身。</summary>
    private sealed class RenderFileServices : IExpertFileServices
    {
        /// <summary>登记调用次数。</summary>
        public int RegisterCalls { get; private set; }
        /// <summary>最近一次登记 MIME 类型。</summary>
        public string? MimeType { get; private set; }
        /// <inheritdoc />
        public Task<ServiceResult> RegisterGeneratedFileAsync(long userId, long tenantId, string name, string mimeType, byte[] content, long? attachRunId, CancellationToken cancellationToken = default) { RegisterCalls++; MimeType = mimeType; return Task.FromResult(new ServiceResult(201, "生成文件已就绪。", new { fileId = 101L, sizeBytes = content.Length })); }
        public Task<ServiceResult> CreateUploadAsync(long userId, long tenantId, ExpertFileUploadRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ServiceResult> CommitObjectAsync(long userId, long tenantId, long fileId, ExpertFileObjectRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ServiceResult> ListAsync(long userId, long tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ServiceResult> DeleteAsync(long userId, long tenantId, long fileId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ServiceResult> AttachToExpertAsync(long userId, long tenantId, long expertId, ExpertFileAttachmentRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ServiceResult> AttachToRunAsync(long userId, long tenantId, long runId, ExpertFileAttachmentRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ServiceResult> GenerateReadTokenAsync(long userId, long tenantId, long fileId, string purpose, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ServiceResult> GetContentAsync(long userId, long tenantId, long fileId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
