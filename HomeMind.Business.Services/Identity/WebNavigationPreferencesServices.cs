using HomeMind.Business.IServices.Family;
using HomeMind.Business.IServices.Identity;
using HomeMind.Common.Infrastructure.Constants;
using HomeMind.Common.Model.Entities;
using HomeMind.Common.Model.Entities.Family;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.Identity;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;

namespace HomeMind.Business.Services.Identity;

/// <summary>
/// Web 导航偏好服务：以后端静态白名单 <c>NexusWebNavigationKeys.All</c> 为唯一真相源，
/// 与当前家庭角色的持久化偏好合并；未持久化的 route_key 使用默认 enabled=true + 默认 sort_order。
/// </summary>
public sealed class WebNavigationPreferencesServices : IWebNavigationPreferencesServices
{
    private static readonly HashSet<string> ValidRoles = new(StringComparer.Ordinal) { "owner", "admin", "member", "viewer" };

    private readonly HomeMindDbContext _db;
    private readonly IFamilyAuditLogger _audit;

    /// <summary>构造 Web 导航偏好服务。</summary>
    /// <param name="db">数据库上下文。</param>
    /// <param name="audit">家庭审计日志写入器。</param>
    public WebNavigationPreferencesServices(HomeMindDbContext db, IFamilyAuditLogger audit)
    {
        _db = db;
        _audit = audit;
    }

    /// <inheritdoc />
    public async Task<ServiceResult> GetForCurrentAsync(long tenantId, string role, CancellationToken cancellationToken = default)
    {
        var persisted = await _db.WebNavigationPreferences
            .Where(x => x.TenantId == tenantId && x.Role == role)
            .ToDictionaryAsync(x => x.RouteKey, StringComparer.Ordinal, cancellationToken);

        var routes = NexusWebNavigationKeys.All
            .Select(k => new WebNavigationRouteView(
                k.RouteKey,
                persisted.TryGetValue(k.RouteKey, out var p) ? p.Enabled : true,
                persisted.TryGetValue(k.RouteKey, out var p2) ? p2.SortOrder : k.SortOrder,
                IsCustomized: persisted.ContainsKey(k.RouteKey)))
            .ToList();

        var latest = persisted.Values.OrderByDescending(x => x.UpdatedAt).Select(x => (DateTime?)x.UpdatedAt).FirstOrDefault();
        return new ServiceResult(200, "查询成功。", new WebNavigationPreferencesView(role, routes, latest));
    }

    /// <inheritdoc />
    public async Task<ServiceResult> UpdateForRoleAsync(long tenantId, long actorUserId, WebNavigationPreferencesUpdateRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.TargetRole) || !ValidRoles.Contains(request.TargetRole))
            return new ServiceResult(422, "目标角色必须是 owner/admin/member/viewer 之一。", ErrorCode: ApiErrorCodes.ValidationFailed);
        if (request.Items is null || request.Items.Count == 0)
            return new ServiceResult(422, "至少提交一个偏好项。", ErrorCode: ApiErrorCodes.ValidationFailed);

        var submitted = request.Items.DistinctBy(x => x.RouteKey, StringComparer.Ordinal).ToList();
        foreach (var item in submitted)
        {
            if (!NexusWebNavigationKeys.IsKnownRouteKey(item.RouteKey))
                return new ServiceResult(422, $"route_key 未发布：{item.RouteKey}", ErrorCode: ApiErrorCodes.WebNavigationRouteKeyNotPublished);
        }

        var now = DateTime.UtcNow;
        var existing = await _db.WebNavigationPreferences
            .Where(x => x.TenantId == tenantId && x.Role == request.TargetRole)
            .ToListAsync(cancellationToken);
        var existingByKey = existing.ToDictionary(x => x.RouteKey, StringComparer.Ordinal);

        foreach (var item in submitted)
        {
            if (existingByKey.TryGetValue(item.RouteKey, out var row))
            {
                row.Enabled = item.Enabled;
                row.SortOrder = item.SortOrder;
                row.UpdatedByUserId = actorUserId;
                row.UpdatedAt = now;
            }
            else
            {
                _db.WebNavigationPreferences.Add(new WebNavigationPreference
                {
                    TenantId = tenantId,
                    Role = request.TargetRole,
                    RouteKey = item.RouteKey,
                    Enabled = item.Enabled,
                    SortOrder = item.SortOrder,
                    UpdatedByUserId = actorUserId,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }
        }
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync(tenantId, actorUserId, FamilyAuditActions.WebNavigationPreferenceUpdated, FamilyAuditTargetTypes.WebNavigationPreference, null,
            before: null, after: new { role = request.TargetRole, items = submitted.Select(x => new { x.RouteKey, x.Enabled, x.SortOrder }) },
            reason: $"更新角色 {request.TargetRole} 的 Web 导航偏好。", relatedRunId: null, cancellationToken);

        return await GetForCurrentAsync(tenantId, request.TargetRole, cancellationToken);
    }
}
