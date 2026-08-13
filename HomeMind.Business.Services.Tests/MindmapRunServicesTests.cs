using HomeMind.Business.IServices.AI;
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

/// <summary>B33 思维导图 Skill 定向测试：同步完成、展示安全摘要、幂等、输入边界、隔离与审计。</summary>
public sealed class MindmapRunServicesTests
{
    /// <summary>创建同步完成，不产生 Action，返回字符数和首个一级标题，不回显 markdown。</summary>
    [Fact]
    public async Task Create_Completes_Synchronously_With_Safe_Summary()
    {
        await using var db = NewDb("create");
        SeedMindmap(db);
        var service = NewService(db);

        var result = await service.CreateAsync(10, 1, new MindmapRunCreateRequest(null, "# 项目规划\n## 任务\n- 实现 B33"));

        Assert.Equal(201, result.StatusCode);
        var view = Assert.IsType<MindmapRunView>(result.Data);
        Assert.Equal("completed", view.Status);
        Assert.Equal("项目规划", view.FirstHeading);
        Assert.Equal(21, view.CharacterCount);
        Assert.Empty(db.ExpertRunActions);
        Assert.Single(db.RunEvents);
        var audit = await db.FamilyAuditLogs.SingleAsync();
        Assert.Equal(FamilyAuditActions.SkillRunCreated, audit.Action);
        Assert.Equal(FamilyAuditTargetTypes.SkillRun, audit.TargetType);
        Assert.DoesNotContain("实现 B33", view.ResultSummary);
    }

    /// <summary>同一幂等键仅创建一次并返回首次摘要。</summary>
    [Fact]
    public async Task Create_Replays_Same_Idempotency_Key()
    {
        await using var db = NewDb("replay");
        SeedMindmap(db);
        var service = NewService(db);
        var key = Guid.NewGuid().ToString();

        var first = await service.CreateAsync(10, 1, new MindmapRunCreateRequest(key, "# 标题"));
        var replay = await service.CreateAsync(10, 1, new MindmapRunCreateRequest(key, "# 已忽略"));

        Assert.Equal(201, first.StatusCode);
        Assert.Equal(200, replay.StatusCode);
        Assert.Single(db.AgentRuns);
        Assert.Equal(Assert.IsType<MindmapRunView>(first.Data).Id, Assert.IsType<MindmapRunView>(replay.Data).Id);
    }

    /// <summary>空输入、超限输入和未注册 Skill 分别返回 422。</summary>
    [Fact]
    public async Task Create_Rejects_Invalid_Input_And_Unknown_Skill()
    {
        await using var db = NewDb("invalid");
        var service = NewService(db);

        var unknown = await service.CreateAsync(10, 1, new MindmapRunCreateRequest(null, "# 标题"));
        SeedMindmap(db);
        var empty = await service.CreateAsync(10, 1, new MindmapRunCreateRequest(null, " "));
        var oversized = await service.CreateAsync(10, 1, new MindmapRunCreateRequest(null, new string('a', 100001)));

        Assert.Equal(422, unknown.StatusCode);
        Assert.Equal(422, empty.StatusCode);
        Assert.Equal(422, oversized.StatusCode);
    }

    /// <summary>跨用户、跨租户查询一律返回 404。</summary>
    [Fact]
    public async Task Get_Hides_Cross_User_And_Tenant_Runs()
    {
        await using var db = NewDb("isolation");
        SeedMindmap(db);
        var service = NewService(db);
        var created = await service.CreateAsync(10, 1, new MindmapRunCreateRequest(null, "# 标题"));
        var runId = Assert.IsType<MindmapRunView>(created.Data).Id;

        Assert.Equal(200, (await service.GetAsync(10, 1, runId)).StatusCode);
        Assert.Equal(404, (await service.GetAsync(11, 1, runId)).StatusCode);
        Assert.Equal(404, (await service.GetAsync(10, 2, runId)).StatusCode);
    }

    private static MindmapRunServices NewService(HomeMindDbContext db) => new(db, new FamilyAuditLogger(db, NullLogger<FamilyAuditLogger>.Instance));

    private static HomeMindDbContext NewDb(string name) => new(new DbContextOptionsBuilder<HomeMindDbContext>()
        .UseInMemoryDatabase($"hm-b33-{name}-{Guid.NewGuid()}")
        .Options);

    private static void SeedMindmap(HomeMindDbContext db)
    {
        db.SkillCatalogs.Add(new SkillCatalog
        {
            TenantId = 1,
            Key = "mindmap",
            Name = "生成思维导图",
            Category = "productivity",
            InputSchema = "{}",
            RequiredPermission = "mindmap.read",
            RiskLevel = "L1",
            Status = "active",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        db.SaveChanges();
    }
}
