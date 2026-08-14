using HomeMind.Business.IServices.Family;
using HomeMind.Business.IServices.Memory;
using HomeMind.Common.Model.Entities.Family;
using HomeMind.Common.Model.Entities.Memory;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.Memory;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;

namespace HomeMind.Business.Services.Memory;

/// <summary>记忆候选审核实现，将接受的候选受控写入事实源与学习投影。</summary>
public sealed class MemoryCandidateServices : IMemoryCandidateServices
{
    private readonly HomeMindDbContext _db;
    private readonly IFamilyAuditLogger _audit;

    /// <summary>构造候选审核服务。</summary>
    public MemoryCandidateServices(HomeMindDbContext db, IFamilyAuditLogger audit)
    {
        _db = db;
        _audit = audit;
    }

    /// <inheritdoc />
    public async Task<ServiceResult> ListAsync(long homeId, long actorUserId, string? scope, string? status, CancellationToken cancellationToken = default)
    {
        if (!IsScope(scope) || !IsCandidateStatus(status)) return new ServiceResult(422, "记忆候选筛选条件无效。");
        var query = _db.MemoryCandidates.Where(x => x.HomeId == homeId);
        if (scope == MemoryVisibility.Personal) query = query.Where(x => x.OwnerUserId == actorUserId);
        else if (scope == MemoryVisibility.Family) query = query.Where(x => x.Visibility == MemoryVisibility.Family);
        else query = query.Where(x => x.Visibility == MemoryVisibility.Family || x.OwnerUserId == actorUserId);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        var items = await query.OrderByDescending(x => x.CreatedAt).ToListAsync(cancellationToken);
        return new ServiceResult(200, "查询成功。", items.Select(ToCandidateView).ToArray());
    }

    /// <inheritdoc />
    public async Task<ServiceResult> AcceptAsync(long homeId, long actorUserId, long candidateId, ResolveMemoryCandidateRequest request, CancellationToken cancellationToken = default)
    {
        var candidate = await FindVisibleCandidateAsync(homeId, actorUserId, candidateId, cancellationToken);
        if (candidate is null) return new ServiceResult(404, "请求的记忆候选不存在。");
        if (candidate.Status == MemoryCandidateStatus.Accepted)
        {
            var existing = await _db.LearningMemoryRecords.SingleOrDefaultAsync(x => x.CandidateId == candidate.Id, cancellationToken);
            return existing is null ? new ServiceResult(409, "候选已被接受，但学习投影不存在。") : new ServiceResult(200, "候选已处理。", ToLearningView(existing));
        }
        if (candidate.Status != MemoryCandidateStatus.Pending || (candidate.ExpiresAt.HasValue && candidate.ExpiresAt.Value <= DateTime.UtcNow))
            return new ServiceResult(409, "仅可接受未过期的待审核记忆候选。");

        var value = string.IsNullOrWhiteSpace(request.Value) ? candidate.ProposedValue : request.Value.Trim();
        var summary = string.IsNullOrWhiteSpace(request.DisplaySummary) ? candidate.DisplaySummary : request.DisplaySummary.Trim();
        if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(summary)) return new ServiceResult(422, "记忆值与展示摘要不能为空。");

        var now = DateTime.UtcNow;
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var (targetType, targetId, resolutionSummary) = await WriteFactAsync(candidate, actorUserId, value, summary, now, cancellationToken);
            candidate.Status = MemoryCandidateStatus.Accepted;
            candidate.ResolvedByUserId = actorUserId;
            candidate.ResolvedAt = now;
            candidate.UpdatedAt = now;
            var record = new LearningMemoryRecord
            {
                HomeId = homeId,
                OwnerUserId = candidate.OwnerUserId,
                CandidateId = candidate.Id,
                TargetType = targetType,
                TargetId = targetId,
                Kind = candidate.Kind,
                Visibility = candidate.Visibility,
                DisplaySummary = summary,
                Stability = candidate.Confidence,
                Status = MemoryRecordStatus.Active,
                SourceRunId = candidate.SourceRunId,
                LearnedAt = now,
                ExpiresAt = candidate.ExpiresAt,
                CreatedAt = now,
                UpdatedAt = now
            };
            _db.LearningMemoryRecords.Add(record);
            await _db.SaveChangesAsync(cancellationToken);
            await _audit.LogAsync(homeId, actorUserId, FamilyAuditActions.MemoryCandidateAccepted,
                FamilyAuditTargetTypes.LearningMemory, record.Id, ToCandidateView(candidate), ToLearningView(record), resolutionSummary, candidate.SourceRunId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new ServiceResult(201, "记忆候选已接受。", ToLearningView(record));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult> RejectAsync(long homeId, long actorUserId, long candidateId, CancellationToken cancellationToken = default)
    {
        var candidate = await FindVisibleCandidateAsync(homeId, actorUserId, candidateId, cancellationToken);
        if (candidate is null) return new ServiceResult(404, "请求的记忆候选不存在。");
        if (candidate.Status == MemoryCandidateStatus.Rejected) return new ServiceResult(200, "候选已被拒绝。", ToCandidateView(candidate));
        if (candidate.Status != MemoryCandidateStatus.Pending || (candidate.ExpiresAt.HasValue && candidate.ExpiresAt.Value <= DateTime.UtcNow))
            return new ServiceResult(409, "仅可拒绝未过期的待审核记忆候选。");
        candidate.Status = MemoryCandidateStatus.Rejected;
        candidate.ResolvedByUserId = actorUserId;
        candidate.ResolvedAt = DateTime.UtcNow;
        candidate.UpdatedAt = candidate.ResolvedAt.Value;
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync(homeId, actorUserId, FamilyAuditActions.MemoryCandidateRejected,
            FamilyAuditTargetTypes.MemoryCandidate, candidate.Id, null, ToCandidateView(candidate), null, candidate.SourceRunId, cancellationToken);
        return new ServiceResult(200, "记忆候选已拒绝。", ToCandidateView(candidate));
    }

    private async Task<(string TargetType, long TargetId, string Summary)> WriteFactAsync(MemoryCandidate candidate, long actorUserId, string value, string summary, DateTime now, CancellationToken cancellationToken)
    {
        if (candidate.Visibility == MemoryVisibility.Personal)
        {
            var personalPreference = new PersonalMemoryPreference { HomeId = candidate.HomeId, OwnerUserId = candidate.OwnerUserId!.Value, Key = candidate.Key, Value = value, DisplaySummary = summary, CreatedAt = now, UpdatedAt = now };
            _db.PersonalMemoryPreferences.Add(personalPreference);
            await _db.SaveChangesAsync(cancellationToken);
            return ("personal_preference", personalPreference.Id, "已写入个人偏好事实。");
        }
        var category = IsFamilyCategory(candidate.Category) ? candidate.Category! : "other";
        var fact = new FamilyKnowledge { HomeId = candidate.HomeId, Category = category, Key = candidate.Key, Value = value, Notes = summary, SourceType = FamilyKnowledgeSourceType.SystemAi, ConfidenceScore = candidate.Confidence, ConflictResolutionStrategy = FamilyKnowledgeConflictResolutionStrategy.Latest, CreatedByUserId = actorUserId, CreatedAt = now, UpdatedAt = now };
        _db.FamilyKnowledge.Add(fact);
        await _db.SaveChangesAsync(cancellationToken);
        return ("family_knowledge", fact.Id, "已写入家庭知识事实。");
    }

    private Task<MemoryCandidate?> FindVisibleCandidateAsync(long homeId, long actorUserId, long candidateId, CancellationToken cancellationToken) =>
        _db.MemoryCandidates.SingleOrDefaultAsync(x => x.Id == candidateId && x.HomeId == homeId && (x.Visibility == MemoryVisibility.Family || x.OwnerUserId == actorUserId), cancellationToken);

    private static MemoryCandidateView ToCandidateView(MemoryCandidate candidate) => new(candidate.Id, candidate.Kind, candidate.Visibility, candidate.Key, candidate.ProposedValue, candidate.DisplaySummary, candidate.Confidence, candidate.RiskLevel, candidate.Status, candidate.SourceRunId, candidate.CreatedAt, candidate.ExpiresAt);

    private static LearningMemoryView ToLearningView(LearningMemoryRecord record) => new(record.Id, record.DisplaySummary, record.Kind, record.Visibility, record.Stability, record.Status, record.LearnedAt, record.ExpiresAt, record.SourceRunId is long runId ? new[] { new LearningMemorySourceReferenceView("run", runId) } : Array.Empty<LearningMemorySourceReferenceView>(), 0, "候选已审核并写入事实源。");

    private static bool IsScope(string? value) => string.IsNullOrWhiteSpace(value) || value is "all" or MemoryVisibility.Personal or MemoryVisibility.Family;
    private static bool IsCandidateStatus(string? value) => string.IsNullOrWhiteSpace(value) || value is MemoryCandidateStatus.Pending or MemoryCandidateStatus.Accepted or MemoryCandidateStatus.Rejected or MemoryCandidateStatus.Expired;
    private static bool IsFamilyCategory(string? value) => value is "property" or "wifi" or "repair" or "cleaning" or "insurance" or "travel" or "other";
}
