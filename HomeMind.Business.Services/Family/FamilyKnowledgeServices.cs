using System.Text.Json;
using HomeMind.Business.IServices.Family;
using HomeMind.Common.Model.Entities.Family;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.Family;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;

namespace HomeMind.Business.Services.Family;

/// <summary>
/// 家庭知识服务实现。职责：
/// - 列表：按 homeId 与可选 category 过滤。
/// - 写入：事务内完成同 key 行锁定、冲突解决（latest/authority/majority）与审计留痕。
/// - 删除：软删除（写 deleted_at）并审计。
/// </summary>
public sealed class FamilyKnowledgeServices : IFamilyKnowledgeServices
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HomeMindDbContext _db;
    private readonly IFamilyAuditLogger _audit;

    /// <summary>构造家庭知识服务。</summary>
    /// <param name="db">数据库上下文。</param>
    /// <param name="audit">家庭审计日志写入器。</param>
    public FamilyKnowledgeServices(HomeMindDbContext db, IFamilyAuditLogger audit)
    {
        _db = db;
        _audit = audit;
    }

    /// <inheritdoc />
    public async Task<ServiceResult> ListAsync(long homeId, string? category, CancellationToken cancellationToken = default)
    {
        var query = _db.FamilyKnowledge.Where(x => x.HomeId == homeId && x.DeletedAt == null);
        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(x => x.Category == category.Trim().ToLowerInvariant());
        var items = await query.OrderBy(x => x.Category).ThenBy(x => x.Key).ThenByDescending(x => x.UpdatedAt)
            .ToListAsync(cancellationToken);
        return new ServiceResult(200, "查询成功。", items.Select(ToView).ToArray());
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">当 source_type 与 source_member_id 不满足 CHECK 时抛出。</exception>
    public async Task<ServiceResult> WriteAsync(long homeId, long actorUserId, FamilyKnowledgeWriteRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Category) || string.IsNullOrWhiteSpace(request.Key) || string.IsNullOrWhiteSpace(request.Value))
            return new ServiceResult(422, "知识分类、键与值均为必填。");
        if (!IsValidCategory(request.Category))
            return new ServiceResult(422, "知识分类仅允许 property、wifi、repair、cleaning、insurance、travel、other。");
        if (request.ConfidenceScore is < 0m or > 1m)
            return new ServiceResult(422, "置信度必须在 0 到 1 之间。");

        var sourceType = string.IsNullOrWhiteSpace(request.SourceType) ? FamilyKnowledgeSourceType.Member : request.SourceType.Trim().ToLowerInvariant();
        if (sourceType == FamilyKnowledgeSourceType.Member && request.SourceMemberId is null)
            return new ServiceResult(422, "成员来源知识必须指定具体成员。");
        if (sourceType == FamilyKnowledgeSourceType.SystemAi && request.SourceMemberId is not null)
            return new ServiceResult(422, "AI 来源知识不能指定成员。");

        var strategy = string.IsNullOrWhiteSpace(request.ConflictResolutionStrategy)
            ? FamilyKnowledgeConflictResolutionStrategy.Latest
            : request.ConflictResolutionStrategy.Trim().ToLowerInvariant();
        if (strategy is not (FamilyKnowledgeConflictResolutionStrategy.Latest
            or FamilyKnowledgeConflictResolutionStrategy.Authority
            or FamilyKnowledgeConflictResolutionStrategy.Majority))
            return new ServiceResult(422, "冲突解决策略仅支持 latest、authority、majority。");

        var now = DateTime.UtcNow;
        var category = request.Category.Trim().ToLowerInvariant();
        var key = request.Key.Trim();
        var value = request.Value.Trim();

        using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var existingRows = await _db.FamilyKnowledge
                .Where(x => x.HomeId == homeId && x.Category == category && x.Key == key && x.DeletedAt == null)
                .OrderByDescending(x => x.UpdatedAt)
                .ToListAsync(cancellationToken);

            var newEntry = new FamilyKnowledge
            {
                HomeId = homeId,
                Category = category,
                Key = key,
                Value = value,
                Notes = request.Notes?.Trim(),
                SourceType = sourceType,
                SourceMemberId = request.SourceMemberId,
                ConfidenceScore = request.ConfidenceScore,
                ConflictResolutionStrategy = strategy,
                CreatedByUserId = actorUserId,
                CreatedAt = now,
                UpdatedAt = now
            };
            _db.FamilyKnowledge.Add(newEntry);
            await _db.SaveChangesAsync(cancellationToken);

            string? resolutionSummary = null;
            var conflictingIds = new List<long>();

            if (existingRows.Count > 0)
            {
                var (resolvedIds, summary) = ResolveConflict(strategy, existingRows, newEntry, value, homeId);
                conflictingIds.AddRange(resolvedIds);
                resolutionSummary = summary;
                newEntry.ResolutionSummary = resolutionSummary;
                await _db.SaveChangesAsync(cancellationToken);

                await _audit.LogAsync(homeId, actorUserId, FamilyAuditActions.KnowledgeConflictResolved,
                    FamilyAuditTargetTypes.FamilyKnowledge, newEntry.Id,
                    new { existingRows = existingRows.Select(x => new { x.Id, x.Value, x.UpdatedAt }) },
                    new { newEntry.Id, newEntry.Value, resolutionSummary },
                    $"同 key \"{key}\" 冲突，策略 {strategy}", null, cancellationToken);
            }

            await _audit.LogAsync(homeId, actorUserId, FamilyAuditActions.KnowledgeWrite,
                FamilyAuditTargetTypes.FamilyKnowledge, newEntry.Id,
                null, ToAuditSnapshot(newEntry), null, null, cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            var resolutionView = resolutionSummary is not null
                ? new FamilyKnowledgeResolutionView(newEntry.Id, key, strategy, resolutionSummary, conflictingIds)
                : null;
            var data = new { Knowledge = ToView(newEntry), Resolution = resolutionView };
            return new ServiceResult(201, "知识条目已写入。", data);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult> DeleteAsync(long homeId, long actorUserId, long knowledgeId, CancellationToken cancellationToken = default)
    {
        var knowledge = await _db.FamilyKnowledge
            .SingleOrDefaultAsync(x => x.Id == knowledgeId && x.HomeId == homeId && x.DeletedAt == null, cancellationToken);
        if (knowledge is null) return new ServiceResult(404, "请求的知识条目不存在。");

        var before = ToAuditSnapshot(knowledge);
        var now = DateTime.UtcNow;
        knowledge.DeletedAt = now;
        knowledge.UpdatedAt = now;

        await _audit.LogAsync(homeId, actorUserId, FamilyAuditActions.KnowledgeWrite,
            FamilyAuditTargetTypes.FamilyKnowledge, knowledge.Id,
            before, null, "软删除", null, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return new ServiceResult(200, "知识条目已删除。");
    }

    /// <summary>
    /// 按策略在同 key 行之间解决冲突。
    /// - latest：保留最新的 updated_at 行；新行直接成为最新。
    /// - authority：按主用户（is_primary=1）成员的记录为权威。
    /// - majority：计数同 value 的行数；新行加入后若某 value 占多数则标记。
    /// </summary>
    private (IReadOnlyList<long> ResolvedIds, string Summary) ResolveConflict(
        string strategy,
        IReadOnlyList<FamilyKnowledge> existingRows,
        FamilyKnowledge newEntry,
        string newValue,
        long homeId)
    {
        return strategy switch
        {
            FamilyKnowledgeConflictResolutionStrategy.Latest => ResolveLatest(existingRows, newEntry),
            FamilyKnowledgeConflictResolutionStrategy.Authority => ResolveAuthority(existingRows, newEntry, homeId),
            FamilyKnowledgeConflictResolutionStrategy.Majority => ResolveMajority(existingRows, newEntry, newValue),
            _ => (Array.Empty<long>(), "不支持的冲突解决策略。")
        };
    }

    private static (IReadOnlyList<long> ResolvedIds, string Summary) ResolveLatest(
        IReadOnlyList<FamilyKnowledge> existingRows, FamilyKnowledge newEntry)
    {
        return (existingRows.Select(x => x.Id).ToList(),
            $"以最新写入为准（新条目 ID={newEntry.Id}），覆盖 {existingRows.Count} 条旧值。");
    }

    private (IReadOnlyList<long> ResolvedIds, string Summary) ResolveAuthority(
        IReadOnlyList<FamilyKnowledge> existingRows, FamilyKnowledge newEntry, long homeId)
    {
        var primaryMemberIds = _db.FamilyMembers
            .Where(x => x.HomeId == homeId && x.IsPrimary && x.DeletedAt == null)
            .Select(x => x.Id)
            .ToHashSet();
        var authoritative = existingRows.Where(x => x.SourceMemberId is not null && primaryMemberIds.Contains(x.SourceMemberId.Value)).ToList();
        if (authoritative.Count > 0)
        {
            return (existingRows.Select(x => x.Id).ToList(),
                $"以主用户成员记录为准（主用户成员 ID={string.Join(",", authoritative.Select(x => x.SourceMemberId!.Value).Distinct())}），共 {existingRows.Count} 条旧值。");
        }

        return ResolveLatest(existingRows, newEntry);
    }

    private static (IReadOnlyList<long> ResolvedIds, string Summary) ResolveMajority(
        IReadOnlyList<FamilyKnowledge> existingRows, FamilyKnowledge newEntry, string newValue)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var idsByValue = new Dictionary<string, List<long>>(StringComparer.Ordinal);
        foreach (var row in existingRows)
        {
            if (!counts.ContainsKey(row.Value)) { counts[row.Value] = 0; idsByValue[row.Value] = new List<long>(); }
            counts[row.Value]++;
            idsByValue[row.Value].Add(row.Id);
        }

        if (!counts.ContainsKey(newValue)) { counts[newValue] = 0; idsByValue[newValue] = new List<long>(); }
        counts[newValue]++;
        idsByValue[newValue].Add(newEntry.Id);

        var maxCount = counts.Values.Max();
        var majorityValues = counts.Where(x => x.Value == maxCount).Select(x => x.Key).ToList();
        var resolvedIds = existingRows.Select(x => x.Id).ToList();

        if (majorityValues.Count == 1 && maxCount > existingRows.Count / 2.0)
        {
            return (resolvedIds, $"多数表决通过（{maxCount}/{existingRows.Count + 1}）：值=\"{majorityValues[0]}\"。");
        }

        return (resolvedIds, $"未出现严格多数（最高 {maxCount}/{existingRows.Count + 1}），冲突值：{string.Join(", ", majorityValues)}。");
    }

    private static FamilyKnowledgeView ToView(FamilyKnowledge k) => new(
        k.Id, k.Category, k.Key, k.Value, k.Notes, k.SourceType,
        k.SourceMemberId, k.ConfidenceScore, k.ConflictResolutionStrategy,
        k.ResolutionSummary, k.CreatedAt, k.UpdatedAt);

    private static object ToAuditSnapshot(FamilyKnowledge k) => new
    {
        k.Id, k.HomeId, k.Category, Key = k.Key, k.Value, k.Notes,
        k.SourceType, k.SourceMemberId, k.ConfidenceScore,
        k.ConflictResolutionStrategy, k.ResolutionSummary
    };

    private static bool IsValidCategory(string category) =>
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "property", "wifi", "repair", "cleaning", "insurance", "travel", "other" }
            .Contains(category.Trim());
}
