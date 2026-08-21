using HomeMind.Business.IServices.Family;
using HomeMind.Business.Services.Finance;
using HomeMind.Business.Services.Steward;
using HomeMind.Common.Model.Entities.Family;
using HomeMind.Common.Model.Entities.Finance;
using HomeMind.Common.Model.Entities.Steward;
using HomeMind.Common.Model.ViewModel.Data.Finance;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HomeMind.Business.Services.Tests;

/// <summary>家庭财务导入、去重与聚合建议定向测试。</summary>
public sealed class FinanceServicesTests
{
    /// <summary>同一账单行重复导入只保留一条。</summary>
    [Fact]
    public async Task Import_Deduplicates_Within_Home()
    {
        await using var db = NewDb(); var audit = new FakeAuditLogger(); var service = new FinanceServices(db, audit);
        const string csv = "date,merchant,amount,currency,category,notes\n2026-08-01,超市,12.50,CNY,餐饮,午餐";
        var first = await service.ImportAsync(1, 10, new FinanceImportRequest(csv));
        var second = await service.ImportAsync(1, 10, new FinanceImportRequest(csv));
        Assert.True(first.Succeeded); Assert.True(second.Succeeded); Assert.Equal(1, db.FinanceTransactions.Count()); Assert.Equal(FamilyAuditActions.FinanceImport, audit.LastAction);
    }

    /// <summary>汇总返回分类金额和重复扣款建议。</summary>
    [Fact]
    public async Task Summary_Returns_Category_And_Duplicate_Suggestion()
    {
        await using var db = NewDb(); db.FinanceTransactions.AddRange(new FinanceTransaction { HomeId = 1, TransactionDate = DateTime.UtcNow.Date, Merchant = "订阅服务", Amount = 20, Currency = "CNY", Category = "订阅", SourceType = "csv", ContentHash = "a", CreatedAt = DateTime.UtcNow }, new FinanceTransaction { HomeId = 1, TransactionDate = DateTime.UtcNow.Date, Merchant = "订阅服务", Amount = 20, Currency = "CNY", Category = "订阅", SourceType = "csv", ContentHash = "b", CreatedAt = DateTime.UtcNow }); await db.SaveChangesAsync();
        var result = await new FinanceServices(db, new FakeAuditLogger()).SummarizeAsync(1, null, null);
        Assert.True(result.Succeeded); var summary = Assert.IsType<FinanceSummaryView>(result.Data); Assert.Equal(40, summary.TotalAmount); Assert.Contains(summary.Suggestions, item => item.Contains("重复扣款"));
        Assert.Equal(3, db.ConfirmationItems.Count());
        Assert.All(db.ConfirmationItems, item => Assert.Equal(ConfirmationRiskLevel.L1, item.RiskLevel));
        Assert.Equal(3, summary.ConfirmationIds?.Count);
        var confirmationList = await new StewardServices(db, new FakeAuditLogger()).ListConfirmationsAsync(1, ConfirmationRiskLevel.L1, ConfirmationItemStatus.Pending);
        Assert.True(confirmationList.Succeeded);
        Assert.NotNull(confirmationList.Data);
        var repeated = await new FinanceServices(db, new FakeAuditLogger()).SummarizeAsync(1, null, null);
        Assert.True(repeated.Succeeded); Assert.Equal(3, db.ConfirmationItems.Count());
    }

    /// <summary>非法日期或金额行不会写入事实表。</summary>
    [Fact]
    public async Task Import_Skips_Invalid_Rows()
    {
        await using var db = NewDb(); var result = await new FinanceServices(db, new FakeAuditLogger()).ImportAsync(1, 10, new FinanceImportRequest("date,merchant,amount,currency,category\ninvalid,店铺,-2,CNY,其他"));
        Assert.Equal(422, result.StatusCode); Assert.Empty(db.FinanceTransactions);
    }

    /// <summary>同一账单内容在不同家庭中分别保留，租户隔离不依赖客户端来源字段。</summary>
    [Fact]
    public async Task Import_Keeps_Same_Row_In_Different_Homes()
    {
        await using var db = NewDb();
        var service = new FinanceServices(db, new FakeAuditLogger());
        const string csv = "date,merchant,amount,currency,category\n2026-08-01,超市,12.50,CNY,餐饮";

        Assert.True((await service.ImportAsync(1, 10, new FinanceImportRequest(csv))).Succeeded);
        Assert.True((await service.ImportAsync(2, 20, new FinanceImportRequest(csv))).Succeeded);
        Assert.Equal(2, await db.FinanceTransactions.CountAsync());
        Assert.Equal(new[] { 1L, 2L }, await db.FinanceTransactions.OrderBy(x => x.HomeId).Select(x => x.HomeId).ToArrayAsync());
    }

    /// <summary>来源类型仅允许已定义的本地解析来源，未知值不会写入账单事实源。</summary>
    [Fact]
    public async Task Import_Rejects_Unknown_Source_Type()
    {
        await using var db = NewDb();
        var result = await new FinanceServices(db, new FakeAuditLogger()).ImportAsync(
            1, 10, new FinanceImportRequest("date,merchant,amount,currency,category\n2026-08-01,超市,12.50,CNY,餐饮", "bank_api"));

        Assert.Equal(422, result.StatusCode);
        Assert.Empty(db.FinanceTransactions);
    }

    /// <summary>支付宝和微信常见的带引号逗号字段可被本地解析并进入同一事实源。</summary>
    [Fact]
    public async Task Import_Parses_Quoted_Alipay_And_Wechat_Rows()
    {
        await using var db = NewDb();
        var service = new FinanceServices(db, new FakeAuditLogger());
        const string csv = "date,merchant,amount,currency,category,notes\n2026-08-01,\"超市,滨江店\",12.50,CNY,餐饮,支付宝\n2026-08-02,微信支付,8.00,CNY,交通,微信";

        var result = await service.ImportAsync(1, 10, new FinanceImportRequest(csv));

        Assert.True(result.Succeeded);
        Assert.Equal(2, await db.FinanceTransactions.CountAsync());
        Assert.Equal("超市,滨江店", (await db.FinanceTransactions.OrderBy(x => x.Id).FirstAsync()).Merchant);
    }

    private static HomeMindDbContext NewDb() => new(new DbContextOptionsBuilder<HomeMindDbContext>().UseInMemoryDatabase($"finance-{Guid.NewGuid()}").Options);
    private sealed class FakeAuditLogger : IFamilyAuditLogger
    {
        public string? LastAction { get; private set; }
        public Task<bool> LogAsync(long homeId, long? actorUserId, string action, string targetType, long? targetId, object? before, object? after, string? reason, long? relatedRunId, CancellationToken cancellationToken = default) { LastAction = action; return Task.FromResult(true); }
    }
}
