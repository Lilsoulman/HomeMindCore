using HomeMind.Business.IServices.Family;
using HomeMind.Business.Services.Memory;
using HomeMind.Common.Model.Agent;
using HomeMind.Common.Model.Entities;
using HomeMind.Common.Model.Entities.Family;
using HomeMind.Common.Model.Entities.Memory;
using HomeMind.Common.Model.ViewModel.Data.Memory;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HomeMind.Business.Services.Tests;

/// <summary>记忆候选审核与学习记忆隔离的定向测试。</summary>
public sealed class MemoryServicesTests
{
    /// <summary>接受个人候选会原子写入个人事实与学习投影。</summary>
    [Fact]
    public async Task Accept_Personal_Candidate_Creates_Preference_And_Learning_Record()
    {
        await using var db = NewDb();
        db.MemoryCandidates.Add(NewCandidate(100, MemoryVisibility.Personal, 10));
        await db.SaveChangesAsync();
        var service = new MemoryCandidateServices(db, new FakeAuditLogger());

        var result = await service.AcceptAsync(1, 10, 100, new ResolveMemoryCandidateRequest(), default);

        Assert.Equal(201, result.StatusCode);
        Assert.Single(db.PersonalMemoryPreferences);
        var record = await db.LearningMemoryRecords.SingleAsync();
        Assert.Equal(100, record.CandidateId);
        Assert.Equal(MemoryVisibility.Personal, record.Visibility);
        Assert.Equal(MemoryCandidateStatus.Accepted, (await db.MemoryCandidates.SingleAsync()).Status);
    }

    /// <summary>个人学习记忆不会出现在同家庭其他成员的查询结果中。</summary>
    [Fact]
    public async Task List_Hides_Other_Users_Personal_Memory()
    {
        await using var db = NewDb();
        db.LearningMemoryRecords.Add(new LearningMemoryRecord
        {
            Id = 200, HomeId = 1, OwnerUserId = 10, CandidateId = 100, TargetType = "personal_preference", TargetId = 1,
            Kind = "preference", Visibility = MemoryVisibility.Personal, DisplaySummary = "喜欢安静的餐厅", Stability = 0.9m,
            Status = MemoryRecordStatus.Active, LearnedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        db.LearningMemoryRecords.Add(new LearningMemoryRecord
        {
            Id = 201, HomeId = 1, CandidateId = 101, TargetType = "family_knowledge", TargetId = 2,
            Kind = "fact", Visibility = MemoryVisibility.Family, DisplaySummary = "物业电话已确认", Stability = 0.8m,
            Status = MemoryRecordStatus.Active, LearnedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var service = new LearningMemoryServices(db);

        var result = await service.ListAsync(1, 20, "all", null, null, null, 20, null, default);

        Assert.True(result.Succeeded);
        var page = Assert.IsType<LearningMemoryPageView>(result.Data);
        Assert.Single(page.Items);
        Assert.Equal(201, page.Items[0].Id);
    }

    /// <summary>A completed Run only produces pending candidates from its explicit structured output.</summary>
    [Fact]
    public async Task Review_Creates_Explicit_Proposals_Once_Without_Reading_Summary()
    {
        await using var db = NewDb();
        db.AgentRuns.Add(new AgentRun
        {
            Id = 300, TenantId = 1, UserId = 10, SourceType = "expert", RequestIdempotencyKey = Guid.NewGuid().ToString(),
            Input = "{\"contains\":\"private prompt\"}", Status = AgentRunStatus.Completed,
            Result = "{\"summary\":\"remember this free-form sentence\",\"memoryCandidates\":[{\"kind\":\"preference\",\"visibility\":\"personal\",\"key\":\"dining.noise\",\"value\":\"quiet\",\"summary\":\"Prefers quiet restaurants\",\"confidence\":0.9,\"riskLevel\":\"L1\"}]}",
            ResultSummary = "Do not parse this summary", CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var service = new MemoryReviewServices(db);

        Assert.Equal(1, await service.ProcessNextAsync());
        Assert.Equal(0, await service.ProcessNextAsync());

        var candidate = await db.MemoryCandidates.SingleAsync();
        Assert.Equal(1, await db.MemoryReviewReceipts.CountAsync());
        Assert.Equal(300, candidate.SourceRunId);
        Assert.Equal(10, candidate.OwnerUserId);
        Assert.Equal(MemoryCandidateStatus.Pending, candidate.Status);
        Assert.DoesNotContain("private prompt", candidate.EvidenceRefsJson!);
        Assert.DoesNotContain("free-form", candidate.DisplaySummary);
    }

    /// <summary>Free-form run results cannot create a memory candidate.</summary>
    [Fact]
    public async Task Review_Ignores_Run_Without_Explicit_Memory_Candidates()
    {
        await using var db = NewDb();
        db.AgentRuns.Add(new AgentRun
        {
            Id = 301, TenantId = 1, UserId = 10, SourceType = "expert", RequestIdempotencyKey = Guid.NewGuid().ToString(),
            Input = "{}", Status = AgentRunStatus.Completed, Result = "{\"summary\":\"User likes quiet restaurants\"}",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var result = await new MemoryReviewServices(db).ProcessNextAsync();

        Assert.Equal(0, result);
        Assert.Empty(db.MemoryCandidates);
        Assert.Single(db.MemoryReviewReceipts);
    }

    /// <summary>Only an Expert schema that explicitly opts in receives the candidate prompt contract.</summary>
    [Fact]
    public void Output_Contract_Is_Opt_In()
    {
        Assert.Null(MemoryCandidateOutputContract.GetPromptInstruction("{\"type\":\"object\"}"));

        var instruction = MemoryCandidateOutputContract.GetPromptInstruction("{\"type\":\"object\",\"properties\":{\"memoryCandidates\":{\"type\":\"array\"}}}");

        Assert.NotNull(instruction);
        Assert.Contains("review-only", instruction!);
    }

    private static MemoryCandidate NewCandidate(long id, string visibility, long? ownerUserId) => new()
    {
        Id = id, HomeId = 1, OwnerUserId = ownerUserId, Kind = "preference", Visibility = visibility,
        Key = "dining.noise", ProposedValue = "安静", DisplaySummary = "偏好安静的餐厅", Confidence = 0.9m,
        RiskLevel = "L1", Status = MemoryCandidateStatus.Pending, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    private static HomeMindDbContext NewDb() => new(new DbContextOptionsBuilder<HomeMindDbContext>()
        .UseInMemoryDatabase($"hm-memory-{Guid.NewGuid()}")
        .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
        .Options);

    private sealed class FakeAuditLogger : IFamilyAuditLogger
    {
        public Task<bool> LogAsync(long homeId, long? actorUserId, string action, string targetType, long? targetId, object? before, object? after, string? reason, long? relatedRunId, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }
}
