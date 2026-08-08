using System.Text.Json;
using HomeMind.Business.Services.AI;
using HomeMind.Business.Services.Family;
using HomeMind.Common.Model.Entities;
using HomeMind.Common.Model.Entities.Family;
using HomeMind.Common.Model.ViewModel.Data.AI;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HomeMind.Business.Services.Tests;

/// <summary>
/// Skill 独立执行定向测试：SkillRun 创建（SourceType=skill、不绑定专家）、确定性方案生成
/// （时长提取/单片段）、幂等重放与跨类型幂等冲突、未知 Skill 与非法输入 422、
/// 跨租户/跨用户 404 与 skill_run_created 审计。
/// </summary>
public class SkillRunServicesTests
{
    /// <summary>创建成功：SourceType=skill、ExpertVersionId 为空、单个 draft_generate 动作（L1）、方案承载于 RequestJson 并写审计。</summary>
    [Fact]
    public async Task Create_Succeeds_And_Generates_Draft_Plan()
    {
        await using var db = NewDb("create");
        SeedQuickEdit(db);
        var services = NewServices(db);

        var result = await services.CreateAsync(10, 1, "quick-edit", new SkillRunCreateRequest(null, """{"media_location":"/nas/videos/探店.mp4","instruction":"竖屏 30 秒，加字幕"}"""), default);

        Assert.Equal(201, result.StatusCode);
        var run = await db.AgentRuns.SingleAsync();
        Assert.Equal("skill", run.SourceType);
        Assert.Null(run.ExpertVersionId);
        Assert.Equal("pending_actions", run.Status);

        var action = await db.ExpertRunActions.SingleAsync();
        Assert.Equal("draft_generate", action.ActionType);
        Assert.Equal("pending", action.Status);
        using var plan = JsonDocument.Parse(action.RequestJson);
        Assert.Equal(30, plan.RootElement.GetProperty("total_duration").GetInt32());
        Assert.Equal(1, plan.RootElement.GetProperty("segments").GetArrayLength());
        Assert.Equal("探店.mp4", plan.RootElement.GetProperty("segments")[0].GetProperty("source").GetString());
        Assert.Contains("探店.mp4", run.ResultSummary);

        var audit = await db.FamilyAuditLogs.SingleAsync();
        Assert.Equal(FamilyAuditActions.SkillRunCreated, audit.Action);
        Assert.Equal(FamilyAuditTargetTypes.SkillRun, audit.TargetType);
        Assert.Equal(run.Id, audit.TargetId);
        Assert.Equal(run.Id, audit.RelatedRunId);
    }

    /// <summary>同一幂等键重复创建返回既有运行，不重复创建。</summary>
    [Fact]
    public async Task Create_Replays_Same_Idempotency_Key()
    {
        await using var db = NewDb("replay");
        SeedQuickEdit(db);
        var services = NewServices(db);
        var key = Guid.NewGuid().ToString();

        var first = await services.CreateAsync(10, 1, "quick-edit", new SkillRunCreateRequest(key, """{"media_location":"/nas/videos/a.mp4"}"""), default);
        var second = await services.CreateAsync(10, 1, "quick-edit", new SkillRunCreateRequest(key, """{"media_location":"/nas/videos/a.mp4"}"""), default);

        Assert.Equal(201, first.StatusCode);
        Assert.Equal(200, second.StatusCode);
        Assert.Equal(1, await db.AgentRuns.CountAsync());
        var firstView = Assert.IsType<SkillRunView>(first.Data);
        Assert.Equal(firstView.Id, Assert.IsType<SkillRunView>(second.Data).Id);
    }

    /// <summary>未知或未启用的 Skill 返回 422。</summary>
    [Fact]
    public async Task Create_Rejects_Unknown_Skill_With_422()
    {
        await using var db = NewDb("unknown-skill");
        SeedQuickEdit(db);
        var services = NewServices(db);

        var result = await services.CreateAsync(10, 1, "unknown", new SkillRunCreateRequest(null, """{"media_location":"/nas/videos/a.mp4"}"""), default);

        Assert.Equal(422, result.StatusCode);
        Assert.Equal(0, await db.AgentRuns.CountAsync());
    }

    /// <summary>缺少 media_location 或非法 JSON 返回 422。</summary>
    [Fact]
    public async Task Create_Rejects_Missing_MediaLocation_And_Invalid_Json()
    {
        await using var db = NewDb("invalid-input");
        SeedQuickEdit(db);
        var services = NewServices(db);

        var missing = await services.CreateAsync(10, 1, "quick-edit", new SkillRunCreateRequest(null, """{"instruction":"30秒"}"""), default);
        var invalid = await services.CreateAsync(10, 1, "quick-edit", new SkillRunCreateRequest(null, "not-json"), default);

        Assert.Equal(422, missing.StatusCode);
        Assert.Equal(422, invalid.StatusCode);
        Assert.Equal(0, await db.AgentRuns.CountAsync());
    }

    /// <summary>从创作指令提取目标时长：N分钟乘以 60；无指令默认 15 秒。</summary>
    [Fact]
    public async Task Create_Parses_Duration_From_Instruction()
    {
        await using var db = NewDb("duration");
        SeedQuickEdit(db);
        var services = NewServices(db);

        var minutes = await services.CreateAsync(10, 1, "quick-edit", new SkillRunCreateRequest(null, """{"media_location":"/nas/videos/a.mp4","instruction":"时长 2 分钟"}"""), default);
        Assert.True(minutes.Succeeded);
        var minutesRun = await db.AgentRuns.SingleAsync();
        using var minutesPlan = JsonDocument.Parse(await db.ExpertRunActions.Where(x => x.RunId == minutesRun.Id).Select(x => x.RequestJson).SingleAsync());
        Assert.Equal(120, minutesPlan.RootElement.GetProperty("total_duration").GetInt32());

        var defaulted = await services.CreateAsync(10, 1, "quick-edit", new SkillRunCreateRequest(null, """{"media_location":"/nas/videos/b.mp4"}"""), default);
        Assert.True(defaulted.Succeeded);
        var defaultRun = await db.AgentRuns.SingleAsync(x => x.Id != minutesRun.Id);
        using var defaultPlan = JsonDocument.Parse(await db.ExpertRunActions.Where(x => x.RunId == defaultRun.Id).Select(x => x.RequestJson).SingleAsync());
        Assert.Equal(15, defaultPlan.RootElement.GetProperty("total_duration").GetInt32());
    }

    /// <summary>跨租户、跨用户或不存在查询一律 404。</summary>
    [Fact]
    public async Task Get_Rejects_Cross_Tenant_And_Other_User_With_404()
    {
        await using var db = NewDb("cross");
        SeedQuickEdit(db);
        var services = NewServices(db);
        var created = await services.CreateAsync(10, 1, "quick-edit", new SkillRunCreateRequest(null, """{"media_location":"/nas/videos/a.mp4"}"""), default);
        var runId = Assert.IsType<SkillRunView>(created.Data).Id;

        var own = await services.GetAsync(10, 1, runId, default);
        var otherUser = await services.GetAsync(11, 1, runId, default);
        var otherTenant = await services.GetAsync(10, 2, runId, default);
        var missing = await services.GetAsync(10, 1, 9999, default);

        Assert.Equal(200, own.StatusCode);
        Assert.Equal(404, otherUser.StatusCode);
        Assert.Equal(404, otherTenant.StatusCode);
        Assert.Equal(404, missing.StatusCode);
    }

    /// <summary>同一幂等键已用于其他运行类型（如 scenario）时返回 409。</summary>
    [Fact]
    public async Task Create_Rejects_Idempotency_Key_Used_By_Other_Run_Type()
    {
        await using var db = NewDb("key-conflict");
        SeedQuickEdit(db);
        var services = NewServices(db);
        var key = Guid.NewGuid().ToString();
        db.AgentRuns.Add(new AgentRun
        {
            TenantId = 1,
            UserId = 10,
            SourceType = "scenario",
            RequestIdempotencyKey = key,
            Input = "{}",
            Status = "completed",
            Mode = "steward",
            AutoConfirmPolicy = "L3_only",
            PermissionSnapshot = "{}",
            EstimatedCredits = 0,
            StartedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var result = await services.CreateAsync(10, 1, "quick-edit", new SkillRunCreateRequest(key, """{"media_location":"/nas/videos/a.mp4"}"""), default);

        Assert.Equal(409, result.StatusCode);
        Assert.Equal(1, await db.AgentRuns.CountAsync());
    }

    private static SkillRunServices NewServices(HomeMindDbContext db) =>
        new(db, new FamilyAuditLogger(db, NullLogger<FamilyAuditLogger>.Instance));

    private static HomeMindDbContext NewDb(string name) =>
        new(new DbContextOptionsBuilder<HomeMindDbContext>()
            .UseInMemoryDatabase($"hm-b24-{name}-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private static void SeedQuickEdit(HomeMindDbContext db)
    {
        db.SkillCatalogs.Add(new SkillCatalog
        {
            TenantId = 1,
            Key = "quick-edit",
            Name = "快速剪辑",
            Category = "media",
            Description = "把本机/NAS 素材按创作目标和指令生成可编辑的剪映草稿。",
            InputSchema = """{"type":"object","required":["media_location"]}""",
            OutputSchema = """{"type":"object"}""",
            RequiredPermission = "media.read",
            RiskLevel = "L1",
            Status = "active",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        db.SaveChanges();
    }
}
