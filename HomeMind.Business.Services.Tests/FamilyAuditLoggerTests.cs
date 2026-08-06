using HomeMind.Business.IServices.Family;
using HomeMind.Business.Services.Family;
using HomeMind.Common.Model.Entities.Family;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HomeMind.Business.Services.Tests;

/// <summary>家庭审计日志写入器定向测试：覆盖白名单校验、写库失败不抛与成功路径。</summary>
public class FamilyAuditLoggerTests
{
    /// <summary>非法 action 抛出 ArgumentException。</summary>
    [Fact]
    public async Task Throws_On_Invalid_Action()
    {
        await using var db = NewDb("audit-invalid-action");
        var logger = new FamilyAuditLogger(db, NullLogger<FamilyAuditLogger>.Instance);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            logger.LogAsync(1, 1, "bad_action", FamilyAuditTargetTypes.FamilyMember, 1, null, null, null, null));
    }

    /// <summary>非法 targetType 抛出 ArgumentException。</summary>
    [Fact]
    public async Task Throws_On_Invalid_TargetType()
    {
        await using var db = NewDb("audit-invalid-target");
        var logger = new FamilyAuditLogger(db, NullLogger<FamilyAuditLogger>.Instance);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            logger.LogAsync(1, 1, FamilyAuditActions.DecisionRecord, "bad_target", 1, null, null, null, null));
    }

    /// <summary>合法 action + targetType 写入成功。</summary>
    [Fact]
    public async Task Succeeds_For_Valid_Audit()
    {
        await using var db = NewDb("audit-valid");
        var logger = new FamilyAuditLogger(db, NullLogger<FamilyAuditLogger>.Instance);
        var ok = await logger.LogAsync(1, 1, FamilyAuditActions.KnowledgeWrite, FamilyAuditTargetTypes.FamilyKnowledge, 1, null, new { value = "new" }, "test", null);
        Assert.True(ok);
    }

    private static HomeMindDbContext NewDb(string name) =>
        new(new DbContextOptionsBuilder<HomeMindDbContext>()
            .UseInMemoryDatabase($"hm-b11-audit-{name}-{Guid.NewGuid()}")
            .Options);
}
