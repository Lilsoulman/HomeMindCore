using System.Text;
using System.Text.Json;
using HomeMind.Business.IServices.Family;
using HomeMind.Business.IServices.Steward;
using HomeMind.Common.Model.Entities.Family;
using HomeMind.Common.Model.Entities.Steward;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.Steward;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;

namespace HomeMind.Business.Services.Steward;

/// <summary>
/// 管家动态与确认中心服务实现。职责：
/// - 管家动态：游标分页列表、详情、撤销（仅 <c>undoable=true</c> 的已完成活动，审计留痕）。
/// - 确认中心：列表过滤、L2/L3 逐项确认与拒绝、L1 批量确认（预验证后单事务原子提交，幂等键重放）。
/// - 所有确认、拒绝、批量确认与撤销均写入 <c>family_audit_logs</c> 审计与可展示的管家动态。
/// 过期（<c>expires_at</c>）一律采用计算语义（<c>ExpiresAt == null || ExpiresAt &gt; now</c>），不写回填。
/// </summary>
public sealed class StewardServices : IStewardServices
{
    private const int MaxLimit = 50;
    private const int MaxBatchSize = 50;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HomeMindDbContext _db;
    private readonly IFamilyAuditLogger _audit;

    /// <summary>构造管家动态与确认中心服务。</summary>
    /// <param name="db">数据库上下文。</param>
    /// <param name="audit">家庭审计日志写入器。</param>
    public StewardServices(HomeMindDbContext db, IFamilyAuditLogger audit)
    {
        _db = db;
        _audit = audit;
    }

    /// <inheritdoc />
    public async Task<ServiceResult> ListActivitiesAsync(long homeId, int limit, string? cursor, CancellationToken cancellationToken = default)
    {
        if (limit <= 0 || limit > MaxLimit) limit = MaxLimit;
        var query = _db.StewardActivities.Where(x => x.HomeId == homeId);

        if (TryDecodeCursor(cursor, out var createdAt, out var id))
            query = query.Where(x => x.CreatedAt < createdAt || (x.CreatedAt == createdAt && x.Id < id));

        var items = await query
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);

        var hasMore = items.Count > limit;
        if (hasMore) items = items.Take(limit).ToList();
        var nextCursor = hasMore && items.Count > 0
            ? EncodeCursor(items[^1].CreatedAt, items[^1].Id)
            : null;

        var data = new
        {
            Items = items.Select(ToActivityView).ToArray(),
            Cursor = nextCursor
        };
        return new ServiceResult(200, "查询成功。", data);
    }

    /// <inheritdoc />
    public async Task<ServiceResult> GetActivityAsync(long homeId, long activityId, CancellationToken cancellationToken = default)
    {
        var activity = await _db.StewardActivities
            .SingleOrDefaultAsync(x => x.Id == activityId && x.HomeId == homeId, cancellationToken);
        return activity is null
            ? new ServiceResult(404, "请求的管家动态不存在。")
            : new ServiceResult(200, "查询成功。", ToActivityView(activity));
    }

    /// <inheritdoc />
    public async Task<ServiceResult> UndoActivityAsync(long homeId, long actorUserId, long activityId, CancellationToken cancellationToken = default)
    {
        var activity = await _db.StewardActivities
            .SingleOrDefaultAsync(x => x.Id == activityId && x.HomeId == homeId, cancellationToken);
        if (activity is null) return new ServiceResult(404, "请求的管家动态不存在。");

        if (activity.Status != StewardActivityStatus.Completed)
            return new ServiceResult(422, "仅可撤销已完成的管家动态。");
        if (!activity.Undoable)
            return new ServiceResult(422, "该管家动态不支持撤销。");
        if (activity.UndoneAt is not null)
            return new ServiceResult(409, "该管家动态已撤销。");

        var before = ToActivityAuditSnapshot(activity);
        var now = DateTime.UtcNow;
        activity.UndoneAt = now;
        activity.Undoable = false;
        activity.UpdatedAt = now;
        activity.RowVersion++;

        await _audit.LogAsync(homeId, actorUserId, FamilyAuditActions.ActivityUndo, FamilyAuditTargetTypes.StewardActivity,
            activity.Id, before, ToActivityAuditSnapshot(activity), null, activity.RunId, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return new ServiceResult(200, "管家动态已撤销。", ToActivityView(activity));
    }

    /// <inheritdoc />
    public async Task<ServiceResult> ListConfirmationsAsync(long homeId, string? riskLevel, string? status, CancellationToken cancellationToken = default)
    {
        if (riskLevel is not null && riskLevel is not (ConfirmationRiskLevel.L1 or ConfirmationRiskLevel.L2 or ConfirmationRiskLevel.L3))
            return new ServiceResult(422, "风险等级过滤参数非法，仅支持 L1/L2/L3。");
        if (status is not null && status is not (ConfirmationItemStatus.Pending or ConfirmationItemStatus.Confirmed or ConfirmationItemStatus.Denied or ConfirmationItemStatus.Expired or ConfirmationItemStatus.Cancelled))
            return new ServiceResult(422, "状态过滤参数非法。");

        var query = _db.ConfirmationItems.Where(x => x.HomeId == homeId);
        if (riskLevel is not null) query = query.Where(x => x.RiskLevel == riskLevel);
        if (status is not null)
        {
            query = query.Where(x => x.Status == status);
            if (status == ConfirmationItemStatus.Pending)
            {
                var now = DateTime.UtcNow;
                query = query.Where(x => x.ExpiresAt == null || x.ExpiresAt > now);
            }
        }

        var items = await query
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);
        return new ServiceResult(200, "查询成功。", items.Select(ToConfirmationView).ToArray());
    }

    /// <inheritdoc />
    public async Task<ServiceResult> ConfirmAsync(long homeId, long actorUserId, long confirmationId, ConfirmationConfirmRequest request, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(request.IdempotencyKey, out _))
            return new ServiceResult(422, "必须提供有效的幂等键（UUID）。");

        var item = await _db.ConfirmationItems
            .SingleOrDefaultAsync(x => x.Id == confirmationId && x.HomeId == homeId, cancellationToken);
        if (item is null) return new ServiceResult(404, "请求的确认项不存在。");

        if (item.Status == ConfirmationItemStatus.Confirmed)
            return new ServiceResult(200, "该确认项已确认。", ToConfirmationView(item));
        if (item.Status != ConfirmationItemStatus.Pending)
            return new ServiceResult(409, "该确认项已处于终态，不能再次确认。");
        if (item.ExpiresAt is not null && item.ExpiresAt <= DateTime.UtcNow)
            return new ServiceResult(409, "该确认项已过期。");

        var now = DateTime.UtcNow;
        var before = ToConfirmationAuditSnapshot(item);
        item.Status = ConfirmationItemStatus.Confirmed;
        item.ConfirmedByUserId = actorUserId;
        item.ConfirmedAt = now;
        item.UpdatedAt = now;
        item.RowVersion++;

        var linkedActivity = item.ActivityId is { } activityId
            ? await _db.StewardActivities.SingleOrDefaultAsync(x => x.Id == activityId && x.HomeId == homeId, cancellationToken)
            : null;
        if (linkedActivity is { Status: StewardActivityStatus.Pending })
        {
            linkedActivity.Status = StewardActivityStatus.Confirmed;
            linkedActivity.UpdatedAt = now;
            linkedActivity.RowVersion++;
        }

        AddStewardActivity(homeId, linkedActivity?.RunId, item.RiskLevel,
            $"已确认：{item.Title}", StewardActivityStatus.Confirmed, now);

        await _audit.LogAsync(homeId, actorUserId, FamilyAuditActions.ConfirmationConfirm, FamilyAuditTargetTypes.ConfirmationItem,
            item.Id, before, ToConfirmationAuditSnapshot(item), null, linkedActivity?.RunId, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return new ServiceResult(200, "确认项已确认。", ToConfirmationView(item));
    }

    /// <inheritdoc />
    public async Task<ServiceResult> DenyAsync(long homeId, long actorUserId, long confirmationId, ConfirmationDenyRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            return new ServiceResult(422, "拒绝时必须提供原因。");

        var item = await _db.ConfirmationItems
            .SingleOrDefaultAsync(x => x.Id == confirmationId && x.HomeId == homeId, cancellationToken);
        if (item is null) return new ServiceResult(404, "请求的确认项不存在。");

        if (item.Status == ConfirmationItemStatus.Denied)
            return new ServiceResult(200, "该确认项已拒绝。", ToConfirmationView(item));
        if (item.Status == ConfirmationItemStatus.Confirmed)
            return new ServiceResult(409, "该确认项已确认，不能拒绝。");
        if (item.Status != ConfirmationItemStatus.Pending)
            return new ServiceResult(409, "该确认项已处于终态，不能拒绝。");
        if (item.ExpiresAt is not null && item.ExpiresAt <= DateTime.UtcNow)
            return new ServiceResult(409, "该确认项已过期。");

        var now = DateTime.UtcNow;
        var before = ToConfirmationAuditSnapshot(item);
        item.Status = ConfirmationItemStatus.Denied;
        item.DeniedByUserId = actorUserId;
        item.DeniedAt = now;
        item.DenialReason = request.Reason.Trim();
        item.UpdatedAt = now;
        item.RowVersion++;

        var linkedActivity = item.ActivityId is { } activityId
            ? await _db.StewardActivities.SingleOrDefaultAsync(x => x.Id == activityId && x.HomeId == homeId, cancellationToken)
            : null;
        if (linkedActivity is { Status: StewardActivityStatus.Pending })
        {
            linkedActivity.Status = StewardActivityStatus.Cancelled;
            linkedActivity.UpdatedAt = now;
            linkedActivity.RowVersion++;
        }

        AddStewardActivity(homeId, linkedActivity?.RunId, item.RiskLevel,
            $"已拒绝：{item.Title}", StewardActivityStatus.Cancelled, now);

        await _audit.LogAsync(homeId, actorUserId, FamilyAuditActions.ConfirmationDeny, FamilyAuditTargetTypes.ConfirmationItem,
            item.Id, before, ToConfirmationAuditSnapshot(item), request.Reason.Trim(), linkedActivity?.RunId, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return new ServiceResult(200, "确认项已拒绝。", ToConfirmationView(item));
    }

    /// <inheritdoc />
    public async Task<ServiceResult> BatchConfirmAsync(long homeId, long actorUserId, ConfirmationBatchConfirmRequest request, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(request.IdempotencyKey, out _))
            return new ServiceResult(422, "必须提供有效的幂等键（UUID）。");
        if (request.ConfirmationIds is not { Length: > 0 })
            return new ServiceResult(422, "批量确认至少需要一条确认项。");
        if (request.ConfirmationIds.Length > MaxBatchSize)
            return new ServiceResult(422, $"批量确认单次最多 {MaxBatchSize} 条。");
        if (request.ConfirmationIds.Distinct().Count() != request.ConfirmationIds.Length)
            return new ServiceResult(422, "批量确认请求包含重复的确认项 ID。");

        var normalizedKey = Guid.Parse(request.IdempotencyKey).ToString();
        var ids = request.ConfirmationIds.OrderBy(x => x).ToArray();

        var record = await _db.ConfirmationBatchRecords
            .SingleOrDefaultAsync(x => x.HomeId == homeId && x.IdempotencyKey == normalizedKey, cancellationToken);
        if (record is not null)
        {
            var storedIds = JsonSerializer.Deserialize<long[]>(record.ConfirmationIdsJson, JsonOptions) ?? Array.Empty<long>();
            return storedIds.SequenceEqual(ids)
                ? ReplayBatch(record)
                : new ServiceResult(409, "该幂等键已用于其他批量确认。");
        }

        var items = await _db.ConfirmationItems
            .Where(x => x.HomeId == homeId && ids.Contains(x.Id))
            .ToListAsync(cancellationToken);
        if (items.Count != ids.Length)
            return new ServiceResult(404, "批量确认中的部分确认项不存在。");

        if (items.Any(x => x.RiskLevel != ConfirmationRiskLevel.L1))
            return new ServiceResult(409, "仅 L1 风险等级允许批量确认。");
        if (items.Any(x => x.Status != ConfirmationItemStatus.Pending))
            return new ServiceResult(409, "批量确认仅接受未处理（pending）的确认项。");
        var now = DateTime.UtcNow;
        if (items.Any(x => x.ExpiresAt is not null && x.ExpiresAt <= now))
            return new ServiceResult(409, "批量确认中包含已过期的确认项。");

        foreach (var item in items)
        {
            item.Status = ConfirmationItemStatus.Confirmed;
            item.ConfirmedByUserId = actorUserId;
            item.ConfirmedAt = now;
            item.UpdatedAt = now;
            item.RowVersion++;
        }

        var linkedActivityIds = items.Where(x => x.ActivityId is not null).Select(x => x.ActivityId!.Value).Distinct().ToArray();
        if (linkedActivityIds.Length > 0)
        {
            var activities = await _db.StewardActivities
                .Where(x => linkedActivityIds.Contains(x.Id) && x.Status == StewardActivityStatus.Pending)
                .ToListAsync(cancellationToken);
            foreach (var activity in activities)
            {
                activity.Status = StewardActivityStatus.Confirmed;
                activity.UpdatedAt = now;
                activity.RowVersion++;
            }
        }

        AddStewardActivity(homeId, null, ConfirmationRiskLevel.L1, $"已批量确认 {items.Count} 项低风险事项",
            StewardActivityStatus.Confirmed, now);

        var result = new ConfirmationBatchResultView(items.Count, items.Select(ToConfirmationView).ToArray());
        _db.ConfirmationBatchRecords.Add(new ConfirmationBatchRecord
        {
            HomeId = homeId,
            IdempotencyKey = normalizedKey,
            ConfirmationIdsJson = JsonSerializer.Serialize(ids, JsonOptions),
            ResultJson = JsonSerializer.Serialize(result, JsonOptions),
            CreatedAt = now
        });

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            var concurrent = await _db.ConfirmationBatchRecords
                .SingleOrDefaultAsync(x => x.HomeId == homeId && x.IdempotencyKey == normalizedKey, cancellationToken);
            if (concurrent is null) throw;
            return ReplayBatch(concurrent);
        }

        await _audit.LogAsync(homeId, actorUserId, FamilyAuditActions.ConfirmationBatch, FamilyAuditTargetTypes.ConfirmationItem,
            null,
            items.Select(x => new { x.Id, Status = "pending" }).ToArray(),
            items.Select(x => new { x.Id, Status = ConfirmationItemStatus.Confirmed, x.ConfirmedAt }).ToArray(),
            $"L1 批量确认 {items.Count} 项", null, cancellationToken);

        return new ServiceResult(200, "批量确认完成。", result);
    }

    /// <summary>构造一条可展示的管家动态（reporting 分类），随本次确认/拒绝/批量确认一并落库。</summary>
    /// <param name="homeId">归属家庭主键。</param>
    /// <param name="runId">可选的关联运行主键。</param>
    /// <param name="riskLevel">风险等级。</param>
    /// <param name="title">活动标题。</param>
    /// <param name="status">活动状态。</param>
    /// <param name="now">当前 UTC 时间。</param>
    private void AddStewardActivity(long homeId, long? runId, string riskLevel, string title, string status, DateTime now)
    {
        _db.StewardActivities.Add(new StewardActivity
        {
            HomeId = homeId,
            RunId = runId,
            Category = StewardActivityCategory.Reporting,
            Title = title,
            RiskLevel = riskLevel,
            Status = status,
            Undoable = false,
            CreatedAt = now,
            UpdatedAt = now
        });
    }

    /// <summary>将幂等记录反序列化为首次确认结果并返回 200 重放响应。</summary>
    /// <param name="record">命中同键同集的幂等记录。</param>
    /// <returns>200 与首次记录的结果视图。</returns>
    private static ServiceResult ReplayBatch(ConfirmationBatchRecord record)
    {
        var result = JsonSerializer.Deserialize<ConfirmationBatchResultView>(record.ResultJson, JsonOptions);
        return new ServiceResult(200, "批量确认已完成（幂等重放）。", result);
    }

    /// <summary>将活动实体映射为视图，不返回内部字段。</summary>
    private static StewardActivityView ToActivityView(StewardActivity a) => new(
        a.Id, a.RunId, a.Category, a.Title, a.Description, a.RiskLevel, a.Status,
        a.ResultSummary, a.Undoable, a.UndoneAt, a.CreatedAt, a.UpdatedAt);

    /// <summary>将确认项实体映射为视图，不返回内部字段。</summary>
    private static ConfirmationItemView ToConfirmationView(ConfirmationItem c) => new(
        c.Id, c.ActivityId, c.RiskLevel, c.Title, c.Description, c.ImpactSummary, c.SuggestedAction,
        c.Status, c.ExpiresAt, c.ConfirmedAt, c.DeniedAt, c.ExpiredAt, c.UpdatedAt);

    /// <summary>构造用于审计 before/after 快照的活动对象。</summary>
    private static object ToActivityAuditSnapshot(StewardActivity a) => new
    {
        a.Id, a.RunId, a.Category, a.Title, a.RiskLevel, a.Status, a.ResultSummary, a.Undoable, a.UndoneAt
    };

    /// <summary>构造用于审计 before/after 快照的确认项对象。</summary>
    private static object ToConfirmationAuditSnapshot(ConfirmationItem c) => new
    {
        c.Id, c.ActivityId, c.RiskLevel, c.Title, c.Status, c.ExpiresAt,
        c.ConfirmedByUserId, c.ConfirmedAt, c.DeniedByUserId, c.DeniedAt, c.DenialReason
    };

    /// <summary>将 (created_at, id) 编码为 base64 游标。</summary>
    private static string EncodeCursor(DateTime createdAt, long id)
    {
        var key = $"{createdAt:O}|{id}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(key));
    }

    /// <summary>将 base64 游标解码为 (created_at, id)；解码失败返回 false。</summary>
    /// <remarks>使用 <see cref="DateTimeStyles.RoundtripKind"/> 保留 UTC 语义，避免带 Z 后缀的时间被本地化换算导致翻页过滤失效。</remarks>
    private static bool TryDecodeCursor(string? cursor, out DateTime createdAt, out long id)
    {
        createdAt = default;
        id = 0;
        if (string.IsNullOrWhiteSpace(cursor)) return false;
        try
        {
            var raw = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            var parts = raw.Split('|');
            return parts.Length == 2
                   && DateTime.TryParse(parts[0], System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out createdAt)
                   && long.TryParse(parts[1], out id);
        }
        catch
        {
            return false;
        }
    }
}
