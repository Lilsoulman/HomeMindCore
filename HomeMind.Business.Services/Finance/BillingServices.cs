using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using HomeMind.Business.IServices.Family;
using HomeMind.Business.IServices.Finance;
using HomeMind.Common.Model.Entities.Family;
using HomeMind.Common.Model.Entities.Finance;
using HomeMind.Common.Model.Entities.Steward;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.Finance;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;

namespace HomeMind.Business.Services.Finance;

/// <summary>家庭缴费管家服务；管理到期日历和缴后入账，不接入任何第三方支付能力。</summary>
public sealed class BillingServices : IBillingServices
{
    private readonly HomeMindDbContext _db;
    private readonly IFamilyAuditLogger _audit;

    /// <summary>构造缴费管家服务。</summary>
    /// <param name="db">业务数据上下文。</param>
    /// <param name="audit">家庭审计日志写入器。</param>
    public BillingServices(HomeMindDbContext db, IFamilyAuditLogger audit)
    {
        _db = db;
        _audit = audit;
    }

    /// <inheritdoc />
    public async Task<ServiceResult> CreateAccountAsync(long homeId, long actorUserId, BillingAccountCreateRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null || !BillingTypes.All.Contains(request.BillingType))
            return new ServiceResult(422, "billingType 必须是 water、electricity、gas、property、mobile、insurance 或 other。");
        if (string.IsNullOrWhiteSpace(request.Provider) || request.Provider.Trim().Length > 128 || string.IsNullOrWhiteSpace(request.Label) || request.Label.Trim().Length > 128)
            return new ServiceResult(422, "缴费机构和家庭标签均为必填项，且不能超过 128 个字符。");
        if (request.BillingCycleMonths is < 1 or > 24 || request.ExpectedAmount is <= 0 || !BillingSourceTypes.All.Contains(request.SourceType))
            return new ServiceResult(422, "账单周期应为 1 至 24 个月，预计金额必须为正数，且 sourceType 无效。");
        if (string.IsNullOrWhiteSpace(request.Currency) || request.Currency.Trim().Length > 8 || request.SourceRef?.Length > 256)
            return new ServiceResult(422, "货币代码或来源引用不符合约束。");

        var now = DateTime.UtcNow;
        var account = new BillingAccount
        {
            HomeId = homeId,
            CreatedByUserId = actorUserId,
            BillingType = request.BillingType,
            Provider = request.Provider.Trim(),
            Label = request.Label.Trim(),
            BillingCycleMonths = request.BillingCycleMonths,
            ExpectedAmount = request.ExpectedAmount,
            Currency = request.Currency.Trim().ToUpperInvariant(),
            NextDueDate = request.NextDueDate.Date,
            SourceType = request.SourceType,
            SourceRef = request.SourceRef?.Trim(),
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.BillingAccounts.Add(account);
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync(homeId, actorUserId, FamilyAuditActions.BillingAccountCreate, FamilyAuditTargetTypes.BillingAccount,
            account.Id, null, AccountAuditView(account), "本地缴费账户建档", null, cancellationToken);
        return new ServiceResult(201, "缴费账户建档成功。", ToAccountView(account));
    }

    /// <inheritdoc />
    public async Task<ServiceResult> ListAccountsAsync(long homeId, CancellationToken cancellationToken = default)
    {
        var accounts = await _db.BillingAccounts.Where(item => item.HomeId == homeId)
            .OrderBy(item => item.NextDueDate).ThenBy(item => item.Id).Select(ToAccountViewExpression()).ToListAsync(cancellationToken);
        return new ServiceResult(200, "查询成功。", accounts);
    }

    /// <inheritdoc />
    public async Task<ServiceResult> RecordPaymentAsync(long homeId, long actorUserId, long accountId, BillingPaymentRecordRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null || request.Amount <= 0 || !BillingSourceTypes.All.Contains(request.SourceType))
            return new ServiceResult(422, "缴费金额必须为正数，且 sourceType 必须为 manual、ocr 或 finance。");
        var account = await _db.BillingAccounts.SingleOrDefaultAsync(item => item.Id == accountId && item.HomeId == homeId && item.IsActive, cancellationToken);
        if (account is null) return new ServiceResult(404, "缴费账户不存在或已停用。");

        var dueDate = (request.DueDate ?? account.NextDueDate).Date;
        var paidAt = (request.PaidAt ?? DateTime.UtcNow).Date;
        var duplicate = await _db.BillingPaymentRecords.AnyAsync(item => item.BillingAccountId == accountId && item.DueDate == dueDate, cancellationToken);
        if (duplicate) return new ServiceResult(409, "该到期账单已登记缴费记录。");

        var category = $"{BillingTypeName(account.BillingType)}缴费";
        var hash = FinanceHash(homeId, paidAt, account.Provider, request.Amount, account.Currency, category);
        var transaction = await _db.FinanceTransactions.SingleOrDefaultAsync(item => item.HomeId == homeId && item.ContentHash == hash, cancellationToken);
        if (transaction is null)
        {
            transaction = new FinanceTransaction
            {
                HomeId = homeId,
                CreatedByUserId = actorUserId,
                TransactionDate = paidAt,
                Merchant = account.Provider,
                Amount = request.Amount,
                Currency = account.Currency,
                Category = category,
                SourceType = request.SourceType == BillingSourceTypes.Ocr ? "ocr" : "manual",
                SourceRef = $"billing:{account.Id}:{dueDate:yyyyMMdd}",
                ContentHash = hash,
                Notes = $"缴费账户：{account.Label}",
                CreatedAt = DateTime.UtcNow
            };
            _db.FinanceTransactions.Add(transaction);
            await _db.SaveChangesAsync(cancellationToken);
        }

        var now = DateTime.UtcNow;
        var payment = new BillingPaymentRecord
        {
            BillingAccountId = account.Id,
            HomeId = homeId,
            RecordedByUserId = actorUserId,
            DueDate = dueDate,
            PaidAt = paidAt,
            Amount = request.Amount,
            Currency = account.Currency,
            SourceType = request.SourceType,
            FinanceTransactionId = transaction.Id,
            CreatedAt = now
        };
        account.NextDueDate = (request.NextDueDate ?? dueDate.AddMonths(account.BillingCycleMonths)).Date;
        account.UpdatedAt = now;
        _db.BillingPaymentRecords.Add(payment);
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync(homeId, actorUserId, FamilyAuditActions.BillingPaymentRecord, FamilyAuditTargetTypes.BillingPaymentRecord,
            payment.Id, null, new { payment.Id, payment.BillingAccountId, payment.DueDate, payment.PaidAt, payment.Amount, payment.Currency, payment.FinanceTransactionId, account.NextDueDate }, "本地缴费完成登记", null, cancellationToken);
        return new ServiceResult(201, "缴费记录已登记并同步至家庭财务账单。", ToPaymentView(payment));
    }

    /// <inheritdoc />
    public async Task<ServiceResult> ListRemindersAsync(long homeId, DateTime? asOf, CancellationToken cancellationToken = default)
    {
        var today = (asOf ?? DateTime.UtcNow).Date;
        var accounts = await _db.BillingAccounts.Where(item => item.HomeId == homeId && item.IsActive && (item.NextDueDate == today.AddDays(3) || item.NextDueDate == today.AddDays(1)))
            .OrderBy(item => item.NextDueDate).ThenBy(item => item.Id).ToListAsync(cancellationToken);
        var reminders = new List<BillingReminderView>();
        foreach (var account in accounts)
        {
            var days = (int)(account.NextDueDate - today).TotalDays;
            var level = days == 1 ? "one_day" : "three_days";
            var title = $"缴费提醒：{account.Label} 将于 {account.NextDueDate:yyyy-MM-dd} 到期（提前{days}天）";
            var confirmationId = await EnsureReminderConfirmationAsync(homeId, account, days, title, cancellationToken);
            reminders.Add(new BillingReminderView(account.Id, account.BillingType, account.Label, account.NextDueDate, days, level, confirmationId));
        }
        return new ServiceResult(200, "查询成功。", reminders);
    }

    /// <inheritdoc />
    public async Task<ServiceResult> GetAnnualTrendAsync(long homeId, int? year, CancellationToken cancellationToken = default)
    {
        var targetYear = year ?? DateTime.UtcNow.Year;
        if (targetYear is < 2000 or > 2100) return new ServiceResult(422, "year 必须在 2000 至 2100 之间。");
        var start = new DateTime(targetYear, 1, 1);
        var end = start.AddYears(1);
        var records = await _db.BillingPaymentRecords.Where(item => item.HomeId == homeId && item.PaidAt >= start && item.PaidAt < end)
            .ToListAsync(cancellationToken);
        var months = records.GroupBy(item => item.PaidAt.Month).OrderBy(item => item.Key)
            .Select(item => new BillingMonthlyTrend(item.Key, item.Sum(record => record.Amount), item.Count())).ToArray();
        return new ServiceResult(200, "查询成功。", new BillingAnnualTrendView(targetYear, records.Sum(item => item.Amount), months));
    }

    /// <summary>按家庭、到期账单和提醒级别创建幂等的站内 L1 提醒卡。</summary>
    private async Task<long?> EnsureReminderConfirmationAsync(long homeId, BillingAccount account, int days, string title, CancellationToken cancellationToken)
    {
        var existing = await _db.ConfirmationItems.SingleOrDefaultAsync(item => item.HomeId == homeId && item.Title == title, cancellationToken);
        if (existing is not null) return existing.Id;
        var now = DateTime.UtcNow;
        var confirmation = new ConfirmationItem
        {
            HomeId = homeId,
            RiskLevel = ConfirmationRiskLevel.L1,
            Title = title,
            Description = $"{BillingTypeName(account.BillingType)}缴费将在 {days} 天后到期。",
            ImpactSummary = "提醒仅用于站内展示，不会发起缴费或访问第三方支付服务。",
            SuggestedAction = "查看账单并在缴费完成后登记记录。",
            Status = ConfirmationItemStatus.Pending,
            ExpiresAt = account.NextDueDate.AddDays(1),
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.ConfirmationItems.Add(confirmation);
        await _db.SaveChangesAsync(cancellationToken);
        return confirmation.Id;
    }

    /// <summary>将缴费类别转换为面向用户的中文名称。</summary>
    private static string BillingTypeName(string billingType) => billingType switch
    {
        BillingTypes.Water => "水费",
        BillingTypes.Electricity => "电费",
        BillingTypes.Gas => "燃气费",
        BillingTypes.Property => "物业费",
        BillingTypes.Mobile => "话费",
        BillingTypes.Insurance => "保险费",
        _ => "其他"
    };

    /// <summary>生成与财务导入一致的家庭内账单行去重哈希。</summary>
    private static string FinanceHash(long homeId, DateTime date, string merchant, decimal amount, string currency, string category) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{homeId}|{date:yyyy-MM-dd}|{merchant.Trim().ToUpperInvariant()}|{amount.ToString("0.00", CultureInfo.InvariantCulture)}|{currency}|{category}"))).ToLowerInvariant();

    /// <summary>转换缴费账户为对外视图。</summary>
    private static BillingAccountView ToAccountView(BillingAccount item) => new(item.Id, item.BillingType, item.Provider, item.Label, item.BillingCycleMonths, item.ExpectedAmount, item.Currency, item.NextDueDate, item.SourceType, item.IsActive, item.CreatedAt, item.UpdatedAt);

    /// <summary>构造可由 EF 转译的缴费账户对外视图表达式。</summary>
    private static System.Linq.Expressions.Expression<Func<BillingAccount, BillingAccountView>> ToAccountViewExpression() => item => new BillingAccountView(item.Id, item.BillingType, item.Provider, item.Label, item.BillingCycleMonths, item.ExpectedAmount, item.Currency, item.NextDueDate, item.SourceType, item.IsActive, item.CreatedAt, item.UpdatedAt);

    /// <summary>转换缴费记录为对外视图。</summary>
    private static BillingPaymentRecordView ToPaymentView(BillingPaymentRecord item) => new(item.Id, item.BillingAccountId, item.DueDate, item.PaidAt, item.Amount, item.Currency, item.SourceType, item.FinanceTransactionId, item.CreatedAt);

    /// <summary>构造不包含来源引用的缴费账户审计快照。</summary>
    private static object AccountAuditView(BillingAccount item) => new { item.Id, item.BillingType, item.Provider, item.Label, item.BillingCycleMonths, item.ExpectedAmount, item.Currency, item.NextDueDate, item.SourceType, item.IsActive };
}
