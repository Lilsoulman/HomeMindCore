using HomeMind.Business.IServices.Family;
using HomeMind.Business.Services.Family;
using HomeMind.Common.Model.Entities.Family;
using HomeMind.Common.Model.ViewModel.Data.Family;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HomeMind.Business.Services.Tests;

/// <summary>家庭决策历史服务定向测试：覆盖记录、游标分页与审计。</summary>
public class FamilyDecisionServicesTests
{
    /// <summary>记录决策后写入审计。</summary>
    [Fact]
    public async Task Recording_Decision_Writes_Audit()
    {
        await using var db = NewDb("decision-record");
        var audit = new CountingAudit();
        var services = new FamilyDecisionServices(db, audit);

        var result = await services.RecordAsync(1, 1, new FamilyDecisionWriteRequest
        {
            Scenario = "晚餐安排",
            DecisionMade = "今晚在家做饭",
            Rationale = "全家人都想在家吃",
            MadeByMemberId = 1
        });
        Assert.True(result.Succeeded);
        Assert.Equal(201, result.StatusCode);
        Assert.True(audit.Count > 0);
    }

    /// <summary>游标分页：limit + 1 决定 hasMore。</summary>
    [Fact]
    public async Task Cursor_Pagination_Returns_Next_Cursor()
    {
        await using var db = NewDb("decision-cursor");
        var services = new FamilyDecisionServices(db, new CountingAudit());

        var now = DateTime.UtcNow;
        for (var i = 0; i < 5; i++)
        {
            db.DecisionHistory.Add(new DecisionHistory
            {
                HomeId = 1, Scenario = $"s{i}", DecisionMade = $"d{i}",
                DecidedAt = now.AddMinutes(-i), CreatedAt = now, UpdatedAt = now
            });
        }
        await db.SaveChangesAsync();

        var page1 = await services.ListAsync(1, null, 2, null, default);
        Assert.True(page1.Succeeded);
    }

    /// <summary>未传场景或内容返回 422。</summary>
    [Fact]
    public async Task Record_Requires_Scenario_And_Decision()
    {
        await using var db = NewDb("decision-validation");
        var services = new FamilyDecisionServices(db, new CountingAudit());

        var result = await services.RecordAsync(1, 1, new FamilyDecisionWriteRequest
        {
            Scenario = "", DecisionMade = ""
        });
        Assert.False(result.Succeeded);
        Assert.Equal(422, result.StatusCode);
    }

    private static HomeMindDbContext NewDb(string name) =>
        new(new DbContextOptionsBuilder<HomeMindDbContext>()
            .UseInMemoryDatabase($"hm-b11-decision-{name}-{Guid.NewGuid()}")
            .Options);

    /// <summary>纯计数的假审计器。</summary>
    private sealed class CountingAudit : IFamilyAuditLogger
    {
        public int Count { get; private set; }
        public Task<bool> LogAsync(long homeId, long? actorUserId, string action, string targetType, long? targetId, object? before, object? after, string? reason, long? relatedRunId, CancellationToken cancellationToken = default)
        {
            Count++;
            return Task.FromResult(true);
        }
    }
}
