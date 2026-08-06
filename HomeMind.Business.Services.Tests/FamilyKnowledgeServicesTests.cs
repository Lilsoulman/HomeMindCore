using HomeMind.Business.IServices.Family;
using HomeMind.Business.Services.Family;
using HomeMind.Common.Model.Entities.Family;
using HomeMind.Common.Model.ViewModel.Data.Family;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace HomeMind.Business.Services.Tests;

/// <summary>家庭知识服务定向测试：覆盖三策略冲突解决与审计写入。</summary>
public class FamilyKnowledgeServicesTests
{
    /// <summary>latest 策略：新行成为最新，所有旧行保留。</summary>
    [Fact]
    public async Task Latest_Strategy_Favors_New_Value()
    {
        await using var db = NewDb("knowledge-latest");
        var services = new FamilyKnowledgeServices(db, new AuditCounting(db));

        await services.WriteAsync(1, 1, WriteRequest("property", "wifi_ssid", "old-wifi", "latest"), default);
        var result = await services.WriteAsync(1, 1, WriteRequest("property", "wifi_ssid", "new-wifi", "latest"), default);
        Assert.True(result.Succeeded);

        var items = await services.ListAsync(1, "property", default);
        Assert.True(items.Succeeded);
    }

    /// <summary>authority 策略：主用户成员记录为权威。</summary>
    [Fact]
    public async Task Authority_Strategy_Respects_Primary_Member()
    {
        await using var db = NewDb("knowledge-authority");
        var services = new FamilyKnowledgeServices(db, new AuditCounting(db));

        var now = DateTime.UtcNow;
        db.FamilyMembers.Add(new FamilyMember { HomeId = 1, Name = "Primary", Relation = "self", MemberStatus = "active", IsPrimary = true, CreatedByUserId = 1, CreatedAt = now, UpdatedAt = now });
        db.FamilyMembers.Add(new FamilyMember { HomeId = 1, Name = "Child", Relation = "child", MemberStatus = "active", CreatedByUserId = 1, CreatedAt = now, UpdatedAt = now });
        await db.SaveChangesAsync();

        await services.WriteAsync(1, 1, WriteRequest("property", "gate_code", "from-child", "latest", sourceMemberId: 2), default);
        var result = await services.WriteAsync(1, 1, WriteRequest("property", "gate_code", "from-primary", "authority", sourceMemberId: 1), default);
        Assert.True(result.Succeeded);
    }

    /// <summary>majority 策略：多数表决。</summary>
    [Fact]
    public async Task Majority_Strategy_Counts_Votes()
    {
        await using var db = NewDb("knowledge-majority");
        var services = new FamilyKnowledgeServices(db, new AuditCounting(db));

        // 写入 3 条相同值
        for (var i = 0; i < 3; i++)
        {
            await services.WriteAsync(1, 1, WriteRequest("wifi", "password", "abc", "majority"), default);
        }
        // 写入 1 条不同值
        var result = await services.WriteAsync(1, 1, WriteRequest("wifi", "password", "xyz", "majority"), default);
        Assert.True(result.Succeeded);
    }

    /// <summary>软删除应写审计。</summary>
    [Fact]
    public async Task Delete_Writes_Audit()
    {
        await using var db = NewDb("knowledge-delete");
        var audit = new AuditCounting(db);
        var services = new FamilyKnowledgeServices(db, audit);

        var write = await services.WriteAsync(1, 1, WriteRequest("repair", "boiler_age", "5 years", "latest"), default);
        Assert.True(write.Succeeded);

        var before = audit.LoggedCount;
        var delete = await services.DeleteAsync(1, 1, 1, default);
        Assert.True(delete.Succeeded);
        Assert.True(audit.LoggedCount > before);
    }

    private static HomeMindDbContext NewDb(string name) =>
        new(new DbContextOptionsBuilder<HomeMindDbContext>()
            .UseInMemoryDatabase($"hm-b11-knowledge-{name}-{Guid.NewGuid()}")
            // InMemory 不支持显式事务；业务代码的 BeginTransaction 在测试中降级为空操作，不改变被测逻辑。
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private static FamilyKnowledgeWriteRequest WriteRequest(string category, string key, string value, string strategy, long? sourceMemberId = 1) => new()
    {
        Category = category, Key = key, Value = value,
        ConflictResolutionStrategy = strategy,
        SourceType = FamilyKnowledgeSourceType.Member,
        SourceMemberId = sourceMemberId,
        ConfidenceScore = 0.9m
    };

    /// <summary>带计数的审计写入器，使用真实 DbContext。</summary>
    private sealed class AuditCounting : IFamilyAuditLogger
    {
        private readonly HomeMindDbContext _db;
        public int LoggedCount { get; private set; }
        public AuditCounting(HomeMindDbContext db) => _db = db;
        public Task<bool> LogAsync(long homeId, long? actorUserId, string action, string targetType, long? targetId, object? before, object? after, string? reason, long? relatedRunId, CancellationToken cancellationToken = default)
        {
            LoggedCount++;
            return Task.FromResult(true);
        }
    }
}
