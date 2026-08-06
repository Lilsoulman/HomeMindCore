using System.Buffers.Text;
using System.Text;
using HomeMind.Business.IServices.Family;
using HomeMind.Common.Model.Entities.Family;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.Family;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;

namespace HomeMind.Business.Services.Family;

/// <summary>
/// 家庭决策历史服务实现。职责：
/// - 列表：基于 decided_at + id 复合游标的分页查询，仅追加。
/// - 记录：创建决策并写入审计。
/// </summary>
public sealed class FamilyDecisionServices : IFamilyDecisionServices
{
    private const int MaxLimit = 50;
    private readonly HomeMindDbContext _db;
    private readonly IFamilyAuditLogger _audit;

    /// <summary>构造家庭决策历史服务。</summary>
    /// <param name="db">数据库上下文。</param>
    /// <param name="audit">家庭审计日志写入器。</param>
    public FamilyDecisionServices(HomeMindDbContext db, IFamilyAuditLogger audit)
    {
        _db = db;
        _audit = audit;
    }

    /// <inheritdoc />
    public async Task<ServiceResult> ListAsync(long homeId, long? memberId, int limit, string? cursor, CancellationToken cancellationToken = default)
    {
        if (limit <= 0 || limit > MaxLimit) limit = MaxLimit;
        var query = _db.DecisionHistory.Where(x => x.HomeId == homeId && x.DeletedAt == null);
        if (memberId is not null)
            query = query.Where(x => x.MadeByMemberId == memberId);

        if (TryDecodeCursor(cursor, out var decidedAt, out var id))
            query = query.Where(x => x.DecidedAt < decidedAt || (x.DecidedAt == decidedAt && x.Id < id));

        var items = await query
            .OrderByDescending(x => x.DecidedAt).ThenByDescending(x => x.Id)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);

        var hasMore = items.Count > limit;
        if (hasMore) items = items.Take(limit).ToList();
        var nextCursor = hasMore && items.Count > 0
            ? EncodeCursor(items[^1].DecidedAt, items[^1].Id)
            : null;

        var data = new
        {
            Items = items.Select(ToView).ToArray(),
            Cursor = nextCursor
        };
        return new ServiceResult(200, "查询成功。", data);
    }

    /// <inheritdoc />
    public async Task<ServiceResult> RecordAsync(long homeId, long actorUserId, FamilyDecisionWriteRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Scenario) || string.IsNullOrWhiteSpace(request.DecisionMade))
            return new ServiceResult(422, "决策场景与决策内容为必填项。");

        var now = DateTime.UtcNow;
        var decidedAt = request.DecidedAt ?? now;
        var decision = new DecisionHistory
        {
            HomeId = homeId,
            Scenario = request.Scenario.Trim(),
            DecisionMade = request.DecisionMade.Trim(),
            Rationale = request.Rationale?.Trim(),
            Alternatives = request.Alternatives,
            MadeByMemberId = request.MadeByMemberId,
            MadeByUserId = actorUserId,
            DecidedAt = decidedAt,
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.DecisionHistory.Add(decision);
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync(homeId, actorUserId, FamilyAuditActions.DecisionRecord,
            FamilyAuditTargetTypes.DecisionHistory, decision.Id,
            null, ToAuditSnapshot(decision), null, null, cancellationToken);

        return new ServiceResult(201, "决策已记录。", ToView(decision));
    }

    private static DecisionHistoryView ToView(DecisionHistory d) => new(
        d.Id, d.Scenario, d.DecisionMade, d.Rationale, d.Alternatives,
        d.MadeByMemberId, d.DecidedAt, d.UpdatedAt);

    private static object ToAuditSnapshot(DecisionHistory d) => new
    {
        d.Id, d.Scenario, d.DecisionMade, d.Rationale, d.Alternatives,
        d.MadeByMemberId, d.DecidedAt
    };

    /// <summary>将 decided_at + id 编码为 base64 游标。</summary>
    private static string EncodeCursor(DateTime decidedAt, long id)
    {
        var key = $"{decidedAt:O}|{id}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(key));
    }

    /// <summary>将 base64 游标解码为 (decided_at, id)；解码失败返回 false。</summary>
    private static bool TryDecodeCursor(string? cursor, out DateTime decidedAt, out long id)
    {
        decidedAt = default;
        id = 0;
        if (string.IsNullOrWhiteSpace(cursor)) return false;
        try
        {
            var raw = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            var parts = raw.Split('|');
            return parts.Length == 2 && DateTime.TryParse(parts[0], out decidedAt) && long.TryParse(parts[1], out id);
        }
        catch
        {
            return false;
        }
    }
}
