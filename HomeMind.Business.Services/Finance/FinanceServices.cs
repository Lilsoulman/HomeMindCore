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

/// <summary>家庭财务服务，本地解析账单并提供可审计的聚合建议。</summary>
public sealed class FinanceServices : IFinanceServices
{
    private readonly HomeMindDbContext _db;
    private readonly IFamilyAuditLogger _audit;

    /// <summary>构造财务服务。</summary>
    public FinanceServices(HomeMindDbContext db, IFamilyAuditLogger audit) { _db = db; _audit = audit; }

    /// <inheritdoc />
    public async Task<ServiceResult> ImportAsync(long homeId, long actorUserId, FinanceImportRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Csv)) return new ServiceResult(422, "CSV 内容不能为空。");
        if (request.Csv.Length > 5_000_000) return new ServiceResult(422, "CSV 内容不能超过 5 MB。");
        var sourceType = request.SourceType is "csv" or "ocr" or "manual" ? request.SourceType : null;
        if (sourceType is null) return new ServiceResult(422, "sourceType 仅支持 csv、ocr 或 manual。");
        var rows = ParseCsv(request.Csv);
        if (rows.Count == 0) return new ServiceResult(422, "CSV 未包含有效账单行。");
        var hashes = rows.Select(row => Hash(homeId, row.Date, row.Merchant, row.Amount, row.Currency, row.Category)).Distinct().ToArray();
        var existing = (await _db.FinanceTransactions.Where(item => item.HomeId == homeId && hashes.Contains(item.ContentHash)).Select(item => item.ContentHash).ToListAsync(cancellationToken)).ToHashSet(StringComparer.Ordinal);
        var now = DateTime.UtcNow;
        var added = new List<FinanceTransaction>();
        foreach (var row in rows)
        {
            var hash = Hash(homeId, row.Date, row.Merchant, row.Amount, row.Currency, row.Category);
            if (existing.Contains(hash)) continue;
            var transaction = new FinanceTransaction { HomeId = homeId, CreatedByUserId = actorUserId, TransactionDate = row.Date, Merchant = row.Merchant, Amount = row.Amount, Currency = row.Currency, Category = row.Category, SourceType = sourceType, SourceRef = request.SourceRef, ContentHash = hash, Notes = row.Notes, CreatedAt = now };
            _db.FinanceTransactions.Add(transaction); added.Add(transaction); existing.Add(hash);
        }
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync(homeId, actorUserId, FamilyAuditActions.FinanceImport, FamilyAuditTargetTypes.FinanceTransaction, null, null, new { imported = added.Count, skipped = rows.Count - added.Count }, "本地账单导入", null, cancellationToken);
        return new ServiceResult(201, "账单导入完成。", new { imported = added.Count, skipped = rows.Count - added.Count });
    }

    /// <inheritdoc />
    public async Task<ServiceResult> ListAsync(long homeId, DateTime? from, DateTime? to, string? category, CancellationToken cancellationToken = default)
    {
        var query = _db.FinanceTransactions.Where(item => item.HomeId == homeId);
        if (from.HasValue) query = query.Where(item => item.TransactionDate >= from.Value.Date);
        if (to.HasValue) query = query.Where(item => item.TransactionDate <= to.Value.Date);
        if (!string.IsNullOrWhiteSpace(category)) query = query.Where(item => item.Category == category.Trim());
        var items = await query.OrderByDescending(item => item.TransactionDate).ThenBy(item => item.Id).Take(1000).Select(ToViewExpression()).ToListAsync(cancellationToken);
        return new ServiceResult(200, "查询成功。", items);
    }

    /// <inheritdoc />
    public async Task<ServiceResult> SummarizeAsync(long homeId, DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
    {
        var end = (to ?? DateTime.UtcNow).Date; var start = (from ?? end.AddDays(-29)).Date;
        if (start > end || (end - start).TotalDays > 366) return new ServiceResult(422, "统计时间范围须为 1 至 366 天。");
        var items = await _db.FinanceTransactions.Where(item => item.HomeId == homeId && item.TransactionDate >= start && item.TransactionDate <= end).ToListAsync(cancellationToken);
        var categories = items.GroupBy(item => item.Category).OrderByDescending(group => group.Sum(item => item.Amount)).Select(group => new FinanceCategorySummary(group.Key, group.Sum(item => item.Amount), group.Count())).ToArray();
        var suggestions = new List<SuggestionCandidate>();
        var subscriptions = items.Where(item => item.Category == "订阅").Sum(item => item.Amount);
        if (subscriptions > 0)
            suggestions.Add(new SuggestionCandidate(
                $"省钱建议：检查闲置订阅（{start:yyyy-MM-dd}至{end:yyyy-MM-dd}）",
                $"分析窗口内订阅类支出合计 {subscriptions:0.00} 元。",
                "建议核对并取消不再使用的订阅服务。",
                $"订阅类支出 {subscriptions:0.00} 元，请检查闲置服务。"));
        var duplicates = items.GroupBy(item => new { item.Merchant, item.Amount }).Where(group => group.Count() > 1).ToArray();
        if (duplicates.Length > 0)
            suggestions.Add(new SuggestionCandidate(
                $"省钱建议：核对重复扣款（{start:yyyy-MM-dd}至{end:yyyy-MM-dd}）",
                "分析窗口内发现相同商户和金额的重复流水，可能存在重复扣款。",
                "建议逐笔核对重复流水，确认后再联系商户处理。",
                "发现可能重复扣款，请核对相同商户和金额的流水。"));
        var topCategory = categories.FirstOrDefault();
        if (topCategory is not null && topCategory.Amount > items.Sum(item => item.Amount) * 0.5m)
            suggestions.Add(new SuggestionCandidate(
                $"省钱建议：关注高占比支出（{topCategory.Category}，{start:yyyy-MM-dd}至{end:yyyy-MM-dd}）",
                $"{topCategory.Category} 占分析窗口支出的过半（{topCategory.Amount:0.00} 元）。",
                "建议设置该分类的预算提醒并复核可削减项目。",
                $"{topCategory.Category} 占近期支出过半，可设置预算提醒。"));

        var confirmationIds = await EnsureSuggestionConfirmationsAsync(homeId, suggestions, cancellationToken);
        return new ServiceResult(200, "统计成功。", new FinanceSummaryView(
            start, end, items.Sum(item => item.Amount), items.Count, categories,
            suggestions.Select(item => item.SummaryText).ToArray(), confirmationIds));
    }

    /// <summary>将确定性建议投影为确认中心 L1 卡片，并按家庭和分析窗口幂等。</summary>
    private async Task<IReadOnlyList<long>> EnsureSuggestionConfirmationsAsync(
        long homeId,
        IReadOnlyList<SuggestionCandidate> suggestions,
        CancellationToken cancellationToken)
    {
        if (suggestions.Count == 0) return Array.Empty<long>();
        var titles = suggestions.Select(item => item.Title).ToArray();
        var existing = await _db.ConfirmationItems
            .Where(item => item.HomeId == homeId && titles.Contains(item.Title))
            .Select(item => new { item.Title, item.Id })
            .ToListAsync(cancellationToken);
        var existingTitles = existing.Select(item => item.Title).ToHashSet(StringComparer.Ordinal);
        var now = DateTime.UtcNow;
        var created = new List<ConfirmationItem>();
        foreach (var suggestion in suggestions)
        {
            if (existingTitles.Contains(suggestion.Title)) continue;
            var item = new ConfirmationItem
            {
                HomeId = homeId,
                RiskLevel = ConfirmationRiskLevel.L1,
                Title = suggestion.Title,
                Description = suggestion.Description,
                ImpactSummary = "仅展示财务聚合结论，不包含原始账单明细。",
                SuggestedAction = suggestion.SuggestedAction,
                Status = ConfirmationItemStatus.Pending,
                ExpiresAt = now.AddDays(14),
                CreatedAt = now,
                UpdatedAt = now
            };
            _db.ConfirmationItems.Add(item);
            created.Add(item);
            existingTitles.Add(suggestion.Title);
        }

        if (created.Count > 0) await _db.SaveChangesAsync(cancellationToken);
        return existing.Where(item => titles.Contains(item.Title)).Select(item => item.Id)
            .Concat(created.Select(item => item.Id)).ToArray();
    }

    private static readonly Func<FinanceTransaction, FinanceTransactionView> ToView = item => new(item.Id, item.TransactionDate, item.Merchant, item.Amount, item.Currency, item.Category, item.SourceType, item.Notes, item.CreatedAt);
    private static System.Linq.Expressions.Expression<Func<FinanceTransaction, FinanceTransactionView>> ToViewExpression() => item => new FinanceTransactionView(item.Id, item.TransactionDate, item.Merchant, item.Amount, item.Currency, item.Category, item.SourceType, item.Notes, item.CreatedAt);

    private static string Hash(long homeId, DateTime date, string merchant, decimal amount, string currency, string category) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{homeId}|{date:yyyy-MM-dd}|{merchant.Trim().ToUpperInvariant()}|{amount.ToString("0.00", CultureInfo.InvariantCulture)}|{currency}|{category}"))).ToLowerInvariant();

    private static List<ParsedRow> ParseCsv(string csv)
    {
        var lines = csv.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries); var result = new List<ParsedRow>();
        foreach (var line in lines.Skip(1))
        {
            var parts = ParseCsvLine(line);
            if (parts.Length < 5 || !DateTime.TryParse(parts[0], CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) || !decimal.TryParse(parts[2], NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) || amount <= 0 || string.IsNullOrWhiteSpace(parts[1])) continue;
            result.Add(new ParsedRow(date.Date, parts[1], amount, string.IsNullOrWhiteSpace(parts[3]) ? "CNY" : parts[3].ToUpperInvariant(), string.IsNullOrWhiteSpace(parts[4]) ? "其他" : parts[4], parts.Length > 5 ? parts[5] : null));
        }
        return result;
    }

    private static string[] ParseCsvLine(string line)
    {
        var fields = new List<string>(); var field = new StringBuilder(); var quoted = false;
        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (character == '"')
            {
                if (quoted && index + 1 < line.Length && line[index + 1] == '"') { field.Append('"'); index++; }
                else quoted = !quoted;
            }
            else if (character == ',' && !quoted) { fields.Add(field.ToString().Trim()); field.Clear(); }
            else field.Append(character);
        }
        fields.Add(field.ToString().Trim());
        return fields.ToArray();
    }

    private sealed record ParsedRow(DateTime Date, string Merchant, decimal Amount, string Currency, string Category, string? Notes);
    private sealed record SuggestionCandidate(string Title, string Description, string SuggestedAction, string SummaryText);
}
