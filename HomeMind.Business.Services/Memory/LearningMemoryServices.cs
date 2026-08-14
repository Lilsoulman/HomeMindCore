using System.Globalization;
using HomeMind.Business.IServices.Memory;
using HomeMind.Common.Model.Entities.Memory;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.Memory;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;

namespace HomeMind.Business.Services.Memory;

/// <summary>学习记忆库只读查询实现，始终先执行家庭和个人可见性过滤。</summary>
public sealed class LearningMemoryServices : ILearningMemoryServices
{
    private readonly HomeMindDbContext _db;

    /// <summary>构造学习记忆查询服务。</summary>
    public LearningMemoryServices(HomeMindDbContext db) => _db = db;

    /// <inheritdoc />
    public async Task<ServiceResult> ListAsync(long homeId, long actorUserId, string? scope, string? kind, string? status, string? query, int limit, string? cursor, CancellationToken cancellationToken = default)
    {
        if (scope is not null and not ("all" or MemoryVisibility.Personal or MemoryVisibility.Family)) return new ServiceResult(422, "记忆作用域无效。");
        if (status is not null and not (MemoryRecordStatus.Active or MemoryRecordStatus.Archived or MemoryRecordStatus.Expired)) return new ServiceResult(422, "记忆状态无效。");
        if (limit is < 1 or > 50) return new ServiceResult(422, "分页大小必须在 1 到 50 之间。");
        if (!TryDecodeCursor(cursor, out var cursorId)) return new ServiceResult(422, "分页游标无效。");

        var records = _db.LearningMemoryRecords.Where(x => x.HomeId == homeId && (x.Visibility == MemoryVisibility.Family || x.OwnerUserId == actorUserId));
        if (scope == MemoryVisibility.Personal) records = records.Where(x => x.OwnerUserId == actorUserId);
        else if (scope == MemoryVisibility.Family) records = records.Where(x => x.Visibility == MemoryVisibility.Family);
        if (!string.IsNullOrWhiteSpace(kind)) records = records.Where(x => x.Kind == kind.Trim().ToLowerInvariant());
        if (!string.IsNullOrWhiteSpace(status)) records = records.Where(x => x.Status == status);
        if (!string.IsNullOrWhiteSpace(query)) records = records.Where(x => x.DisplaySummary.Contains(query.Trim()));
        if (cursorId is not null) records = records.Where(x => x.Id < cursorId.Value);

        var rows = await records.OrderByDescending(x => x.Id).Take(limit + 1).ToListAsync(cancellationToken);
        var page = rows.Take(limit).Select(ToView).ToArray();
        var next = rows.Count > limit ? page[^1].Id.ToString(CultureInfo.InvariantCulture) : null;
        return new ServiceResult(200, "查询成功。", new LearningMemoryPageView(page, next));
    }

    /// <inheritdoc />
    public async Task<ServiceResult> GetAsync(long homeId, long actorUserId, long memoryId, CancellationToken cancellationToken = default)
    {
        var record = await _db.LearningMemoryRecords.SingleOrDefaultAsync(x => x.Id == memoryId && x.HomeId == homeId && (x.Visibility == MemoryVisibility.Family || x.OwnerUserId == actorUserId), cancellationToken);
        return record is null ? new ServiceResult(404, "请求的学习记忆不存在。") : new ServiceResult(200, "查询成功。", ToView(record));
    }

    private static LearningMemoryView ToView(LearningMemoryRecord record) => new(record.Id, record.DisplaySummary, record.Kind, record.Visibility, record.Stability, record.Status, record.LearnedAt, record.ExpiresAt, record.SourceRunId is long runId ? new[] { new LearningMemorySourceReferenceView("run", runId) } : Array.Empty<LearningMemorySourceReferenceView>(), 0, "候选已审核并写入事实源。");

    private static bool TryDecodeCursor(string? cursor, out long? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(cursor)) return true;
        if (!long.TryParse(cursor, NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id < 1) return false;
        value = id;
        return true;
    }
}
