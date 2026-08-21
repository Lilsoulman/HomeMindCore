namespace HomeMind.Common.Model.ViewModel.Data.Finance;

/// <summary>财务账单条目对外视图。</summary>
public sealed record FinanceTransactionView(long Id, DateTime TransactionDate, string Merchant, decimal Amount, string Currency, string Category, string SourceType, string? Notes, DateTime CreatedAt);

/// <summary>CSV 账单导入请求。</summary>
public sealed record FinanceImportRequest(string Csv, string SourceType = "csv", string? SourceRef = null);

/// <summary>账单聚合分类行。</summary>
public sealed record FinanceCategorySummary(string Category, decimal Amount, int Count);

/// <summary>财务周报及节省建议视图。</summary>
public sealed record FinanceSummaryView(
    DateTime From,
    DateTime To,
    decimal TotalAmount,
    int TransactionCount,
    IReadOnlyList<FinanceCategorySummary> Categories,
    IReadOnlyList<string> Suggestions,
    IReadOnlyList<long>? ConfirmationIds = null);
