using HomeMind.Business.IServices.Family;
using HomeMind.Business.Services.Finance;
using HomeMind.Common.Model.Entities.Family;
using HomeMind.Common.Model.Entities.Finance;
using HomeMind.Common.Model.Entities.Steward;
using HomeMind.Common.Model.ViewModel.Data.Finance;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HomeMind.Business.Services.Tests;

/// <summary>家庭缴费管家的建档、提醒、缴后入账与年度趋势定向测试。</summary>
public sealed class BillingServicesTests
{
    /// <summary>缴后记录会推进下次到期日，并同步生成家庭财务账单事实。</summary>
    [Fact]
    public async Task RecordPayment_Advances_Due_Date_And_Writes_Finance_Transaction()
    {
        await using var db = NewDb();
        var audit = new FakeAuditLogger();
        var service = new BillingServices(db, audit);
        var dueDate = new DateTime(2026, 9, 10);
        var created = await service.CreateAccountAsync(1, 10, new BillingAccountCreateRequest(BillingTypes.Electricity, "供电公司", "家中电费", dueDate, ExpectedAmount: 80));
        var account = Assert.IsType<BillingAccountView>(created.Data);

        var result = await service.RecordPaymentAsync(1, 10, account.Id, new BillingPaymentRecordRequest(86.5m, PaidAt: dueDate));

        Assert.Equal(201, result.StatusCode);
        Assert.Single(db.BillingPaymentRecords);
        Assert.Single(db.FinanceTransactions);
        Assert.Equal("电费缴费", db.FinanceTransactions.Single().Category);
        Assert.Equal(dueDate.AddMonths(1), db.BillingAccounts.Single().NextDueDate);
        Assert.Equal(FamilyAuditActions.BillingPaymentRecord, audit.LastAction);
    }

    /// <summary>提前三天提醒重复读取只创建一张 L1 确认卡。</summary>
    [Fact]
    public async Task Reminders_Are_Idempotently_Projected_To_Confirmation_Center()
    {
        await using var db = NewDb();
        var service = new BillingServices(db, new FakeAuditLogger());
        var today = new DateTime(2026, 9, 1);
        Assert.True((await service.CreateAccountAsync(1, 10, new BillingAccountCreateRequest(BillingTypes.Water, "自来水公司", "家中水费", today.AddDays(3)))).Succeeded);

        var first = await service.ListRemindersAsync(1, today);
        var second = await service.ListRemindersAsync(1, today);

        Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<BillingReminderView>>(first.Data));
        Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<BillingReminderView>>(second.Data));
        var confirmation = Assert.Single(db.ConfirmationItems);
        Assert.Equal(ConfirmationRiskLevel.L1, confirmation.RiskLevel);
        Assert.Equal(ConfirmationItemStatus.Pending, confirmation.Status);
    }

    /// <summary>年度趋势仅统计当前家庭的已登记缴费记录，并按实际缴费月份聚合。</summary>
    [Fact]
    public async Task AnnualTrend_Isolated_By_Home_And_Grouped_By_Paid_Month()
    {
        await using var db = NewDb();
        db.BillingPaymentRecords.AddRange(
            new BillingPaymentRecord { HomeId = 1, BillingAccountId = 1, PaidAt = new DateTime(2026, 1, 3), DueDate = new DateTime(2026, 1, 5), Amount = 20, Currency = "CNY", SourceType = BillingSourceTypes.Manual },
            new BillingPaymentRecord { HomeId = 1, BillingAccountId = 2, PaidAt = new DateTime(2026, 1, 18), DueDate = new DateTime(2026, 1, 20), Amount = 30, Currency = "CNY", SourceType = BillingSourceTypes.Manual },
            new BillingPaymentRecord { HomeId = 2, BillingAccountId = 3, PaidAt = new DateTime(2026, 1, 10), DueDate = new DateTime(2026, 1, 12), Amount = 999, Currency = "CNY", SourceType = BillingSourceTypes.Manual });
        await db.SaveChangesAsync();

        var result = await new BillingServices(db, new FakeAuditLogger()).GetAnnualTrendAsync(1, 2026);

        var trend = Assert.IsType<BillingAnnualTrendView>(result.Data);
        Assert.Equal(50, trend.TotalAmount);
        var january = Assert.Single(trend.Months);
        Assert.Equal(1, january.Month);
        Assert.Equal(2, january.PaymentCount);
    }

    /// <summary>跨家庭账户不能被错误地登记为当前家庭缴费。</summary>
    [Fact]
    public async Task RecordPayment_Rejects_Account_From_Another_Home()
    {
        await using var db = NewDb();
        var service = new BillingServices(db, new FakeAuditLogger());
        var created = await service.CreateAccountAsync(1, 10, new BillingAccountCreateRequest(BillingTypes.Gas, "燃气公司", "家中燃气", new DateTime(2026, 9, 10)));
        var account = Assert.IsType<BillingAccountView>(created.Data);

        var result = await service.RecordPaymentAsync(2, 20, account.Id, new BillingPaymentRecordRequest(50));

        Assert.Equal(404, result.StatusCode);
        Assert.Empty(db.BillingPaymentRecords);
    }

    /// <summary>创建独立的内存数据库上下文。</summary>
    private static HomeMindDbContext NewDb() => new(new DbContextOptionsBuilder<HomeMindDbContext>().UseInMemoryDatabase($"billing-{Guid.NewGuid()}").Options);

    /// <summary>记录最后一次审计动作的测试替身。</summary>
    private sealed class FakeAuditLogger : IFamilyAuditLogger
    {
        /// <summary>最后一次写入的审计动作。</summary>
        public string? LastAction { get; private set; }

        /// <summary>记录审计动作而不写入数据库。</summary>
        public Task<bool> LogAsync(long homeId, long? actorUserId, string action, string targetType, long? targetId, object? before, object? after, string? reason, long? relatedRunId, CancellationToken cancellationToken = default)
        {
            LastAction = action;
            return Task.FromResult(true);
        }
    }
}
