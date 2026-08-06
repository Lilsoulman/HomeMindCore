using HomeMind.Business.IServices.Family;
using HomeMind.Business.Services.Family;
using HomeMind.Common.Model.Entities.Family;
using HomeMind.Common.Model.ViewModel.Data.Family;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xunit;

namespace HomeMind.Business.Services.Tests;

/// <summary>家庭成员服务定向测试：覆盖 active↔away 双向、终态进入与恢复、审计写入。</summary>
public class FamilyMemberServicesTests
{
    /// <summary>active→away→active 双向切换应成功，不写审计。</summary>
    [Fact]
    public async Task Active_Away_Transition_Works_Both_Ways()
    {
        await using var db = NewDb("member-bidi");
        var audit = new FakeAuditLogger();
        var services = new FamilyMemberServices(db, audit);

        SeedMember(db, 1, FamilyMemberStatus.Active, "Alice");
        await db.SaveChangesAsync();

        // active → away
        var awayResult = await services.UpdateAsync(1, 1, 1, new FamilyMemberUpdateRequest { MemberStatus = FamilyMemberStatus.Away });
        Assert.True(awayResult.Succeeded);
        Assert.Equal(0, audit.LoggedCount);

        // away → active
        var backResult = await services.UpdateAsync(1, 1, 1, new FamilyMemberUpdateRequest { MemberStatus = FamilyMemberStatus.Active });
        Assert.True(backResult.Succeeded);
    }

    /// <summary>进入终态 permanently_left 必须三字段同写且审计。</summary>
    [Fact]
    public async Task Entering_Terminal_State_Writes_Correction_Audit()
    {
        await using var db = NewDb("member-terminal");
        var audit = new FakeAuditLogger();
        var services = new FamilyMemberServices(db, audit);

        SeedMember(db, 1, FamilyMemberStatus.Active, "Bob");
        await db.SaveChangesAsync();

        var result = await services.CorrectAsync(1, 1, 1,
            new FamilyMemberCorrectionRequest(FamilyMemberStatus.PermanentlyLeft, "搬离家庭"));
        Assert.True(result.Succeeded);
        Assert.Equal(1, audit.LoggedCount);

        var member = Assert.IsType<FamilyMemberView>(result.Data);
        Assert.Equal(FamilyMemberStatus.PermanentlyLeft, member.MemberStatus);
    }

    /// <summary>从终态恢复到 active 写入 terminal_restore 审计。</summary>
    [Fact]
    public async Task Restoring_From_Terminal_State_Writes_Restore_Audit()
    {
        await using var db = NewDb("member-restore");
        var audit = new FakeAuditLogger();
        var services = new FamilyMemberServices(db, audit);

        var member = SeedMember(db, 1, FamilyMemberStatus.PermanentlyLeft, "Carol");
        member.TerminalCorrectedByUserId = 1;
        member.TerminalCorrectionReason = "test";
        member.TerminalCorrectedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var result = await services.CorrectAsync(1, 1, member.Id,
            new FamilyMemberCorrectionRequest(FamilyMemberStatus.Active, "重新加入"));
        Assert.True(result.Succeeded);
        Assert.True(audit.LastAction == FamilyAuditActions.MemberTerminalRestore);
    }

    /// <summary>非终态成员走 Update 试图进入终态应拒绝。</summary>
    [Fact]
    public async Task Update_Rejects_Terminal_States()
    {
        await using var db = NewDb("member-update-reject");
        var services = new FamilyMemberServices(db, new FakeAuditLogger());

        SeedMember(db, 1, FamilyMemberStatus.Active, "Dave");
        await db.SaveChangesAsync();

        var result = await services.UpdateAsync(1, 1, 1,
            new FamilyMemberUpdateRequest { MemberStatus = FamilyMemberStatus.Deceased });
        Assert.False(result.Succeeded);
        Assert.Equal(422, result.StatusCode);
    }

    private static HomeMindDbContext NewDb(string name) =>
        new(new DbContextOptionsBuilder<HomeMindDbContext>()
            .UseInMemoryDatabase($"hm-b11-members-{name}-{Guid.NewGuid()}")
            .Options);

    private static FamilyMember SeedMember(HomeMindDbContext db, long homeId, string status, string name)
    {
        var now = DateTime.UtcNow;
        var m = new FamilyMember
        {
            HomeId = homeId, Name = name, Relation = "self",
            MemberStatus = status, CreatedByUserId = 1,
            CreatedAt = now, UpdatedAt = now
        };
        db.FamilyMembers.Add(m);
        return m;
    }

    /// <summary>假审计日志写入器，用于验证审计调用次数与动作。</summary>
    private sealed class FakeAuditLogger : IFamilyAuditLogger
    {
        public int LoggedCount { get; private set; }
        public string? LastAction { get; private set; }

        public Task<bool> LogAsync(long homeId, long? actorUserId, string action, string targetType, long? targetId, object? before, object? after, string? reason, long? relatedRunId, CancellationToken cancellationToken = default)
        {
            LoggedCount++;
            LastAction = action;
            return Task.FromResult(true);
        }
    }
}
