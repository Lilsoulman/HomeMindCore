namespace HomeMind.Common.Model.ViewModel.Data.Finance;

/// <summary>缴费账户建档请求；OCR 来源仅接受本地提取后的结构化字段，不上传原始票据。</summary>
public sealed record BillingAccountCreateRequest(
    string BillingType,
    string Provider,
    string Label,
    DateTime NextDueDate,
    int BillingCycleMonths = 1,
    decimal? ExpectedAmount = null,
    string Currency = "CNY",
    string SourceType = "manual",
    string? SourceRef = null);

/// <summary>缴费完成登记请求。</summary>
public sealed record BillingPaymentRecordRequest(
    decimal Amount,
    DateTime? DueDate = null,
    DateTime? PaidAt = null,
    DateTime? NextDueDate = null,
    string SourceType = "manual");

/// <summary>缴费账户对外视图，供到期日历展示。</summary>
public sealed record BillingAccountView(
    long Id,
    string BillingType,
    string Provider,
    string Label,
    int BillingCycleMonths,
    decimal? ExpectedAmount,
    string Currency,
    DateTime NextDueDate,
    string SourceType,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>缴费完成记录对外视图。</summary>
public sealed record BillingPaymentRecordView(
    long Id,
    long BillingAccountId,
    DateTime DueDate,
    DateTime PaidAt,
    decimal Amount,
    string Currency,
    string SourceType,
    long FinanceTransactionId,
    DateTime CreatedAt);

/// <summary>到期提醒视图；确认卡为未实现推送通道时的站内承载。</summary>
public sealed record BillingReminderView(
    long BillingAccountId,
    string BillingType,
    string Label,
    DateTime DueDate,
    int DaysUntilDue,
    string Level,
    long? ConfirmationId);

/// <summary>年度缴费趋势中的月份聚合行。</summary>
public sealed record BillingMonthlyTrend(int Month, decimal Amount, int PaymentCount);

/// <summary>年度缴费趋势视图。</summary>
public sealed record BillingAnnualTrendView(int Year, decimal TotalAmount, IReadOnlyList<BillingMonthlyTrend> Months);
