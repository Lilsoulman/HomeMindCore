using HomeMind.Business.IServices.Family;
using HomeMind.Business.IServices.Identity;
using HomeMind.Common.Model.Entities;
using HomeMind.Common.Model.Entities.Family;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.Identity;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;

namespace HomeMind.Business.Services.Identity;

/// <summary>
/// 家庭成员受控管理服务：实现角色变更、状态停启、owner 转让，遵守"最后一名 active owner"守恒。
/// 所有写操作均写 <c>family_audit_logs</c>，与管家动态、运行事件分离。
/// </summary>
public sealed class TenantMemberServices : ITenantMemberServices
{
    private const string Owner = "owner";
    private const string Admin = "admin";
    private const string Active = "active";
    private const string Suspended = "suspended";

    private static readonly HashSet<string> ValidRoles = new(StringComparer.Ordinal) { Owner, Admin, "member", "viewer" };
    private static readonly HashSet<string> ValidStatuses = new(StringComparer.Ordinal) { Active, Suspended };

    private readonly HomeMindDbContext _db;
    private readonly IFamilyAuditLogger _audit;

    /// <summary>构造家庭成员受控管理服务。</summary>
    /// <param name="db">数据库上下文。</param>
    /// <param name="audit">家庭审计日志写入器。</param>
    public TenantMemberServices(HomeMindDbContext db, IFamilyAuditLogger audit)
    {
        _db = db;
        _audit = audit;
    }

    /// <inheritdoc />
    public async Task<ServiceResult> ListMembersAsync(long tenantId, long actorUserId, CancellationToken cancellationToken = default)
    {
        var rows = await (from tm in _db.TenantMembers
                          join u in _db.Users on tm.UserId equals u.Id
                          where tm.TenantId == tenantId && u.DeletedAt == null
                          orderby tm.JoinedAt
                          select new
                          {
                              tm.UserId,
                              u.DisplayName,
                              u.AvatarUrl,
                              tm.Role,
                              tm.Status,
                              tm.JoinedAt,
                              u.Timezone,
                              u.Locale,
                              tm.RowVersion
                          }).ToListAsync(cancellationToken);
        var items = rows.Select(m => new TenantMemberSummaryView(
            m.UserId, m.DisplayName, m.AvatarUrl, m.Role, m.Status, m.JoinedAt, m.Timezone, m.Locale,
            IsCurrentUserOwner: m.UserId == actorUserId && m.Role == Owner,
            HasPendingInvitation: false,
            RowVersion: m.RowVersion)).ToList();
        return new ServiceResult(200, "查询成功。", items);
    }

    /// <inheritdoc />
    public async Task<ServiceResult> ChangeRoleAsync(long tenantId, long actorUserId, long targetUserId, TenantMemberRoleUpdateRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.NewRole) || !ValidRoles.Contains(request.NewRole))
            return new ServiceResult(422, "目标角色必须是 owner/admin/member/viewer 之一。", ErrorCode: ApiErrorCodes.ValidationFailed);
        if (string.Equals(request.NewRole, Owner, StringComparison.Ordinal))
            return new ServiceResult(422, "角色变更不能直接置 owner，请使用 owner-transfer 接口。", ErrorCode: ApiErrorCodes.TenantRoleOwnerDirectForbidden);

        var target = await _db.TenantMembers.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.UserId == targetUserId, cancellationToken);
        if (target is null) return new ServiceResult(404, "目标成员不属于当前家庭。", ErrorCode: ApiErrorCodes.ResourceNotFound);
        if (target.RowVersion != request.RowVersion)
            return new ServiceResult(409, "成员信息已被他人更新，请刷新后重试。", ErrorCode: ApiErrorCodes.TenantOptimisticLockConflict);

        var beforeRole = target.Role;
        target.Role = request.NewRole;
        target.RowVersion += 1;
        target.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync(tenantId, actorUserId, FamilyAuditActions.TenantMemberRoleChanged, FamilyAuditTargetTypes.TenantMember, targetUserId,
            before: new { role = beforeRole }, after: new { role = target.Role }, reason: $"角色从 {beforeRole} 变更为 {target.Role}。", relatedRunId: null, cancellationToken);

        return new ServiceResult(200, "角色已更新。", ToSummary(target));
    }

    /// <inheritdoc />
    public async Task<ServiceResult> ChangeStatusAsync(long tenantId, long actorUserId, long targetUserId, TenantMemberStatusUpdateRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.NewStatus) || !ValidStatuses.Contains(request.NewStatus))
            return new ServiceResult(422, "目标状态必须是 active/suspended 之一。", ErrorCode: ApiErrorCodes.ValidationFailed);
        if (string.Equals(request.NewStatus, Suspended, StringComparison.Ordinal) && string.IsNullOrWhiteSpace(request.Reason))
            return new ServiceResult(422, "停用时必须填写原因。", ErrorCode: ApiErrorCodes.ValidationFailed);

        var target = await _db.TenantMembers.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.UserId == targetUserId, cancellationToken);
        if (target is null) return new ServiceResult(404, "目标成员不属于当前家庭。", ErrorCode: ApiErrorCodes.ResourceNotFound);
        if (target.RowVersion != request.RowVersion)
            return new ServiceResult(409, "成员信息已被他人更新，请刷新后重试。", ErrorCode: ApiErrorCodes.TenantOptimisticLockConflict);

        if (string.Equals(request.NewStatus, Suspended, StringComparison.Ordinal) && string.Equals(target.Role, Owner, StringComparison.Ordinal))
        {
            var otherActiveOwnerCount = await _db.TenantMembers.CountAsync(
                x => x.TenantId == tenantId && x.Role == Owner && x.Status == Active && x.UserId != targetUserId, cancellationToken);
            if (otherActiveOwnerCount < 1)
                return new ServiceResult(422, "不能停用最后一名 active owner。", ErrorCode: ApiErrorCodes.ValidationFailed);
        }

        var beforeStatus = target.Status;
        target.Status = request.NewStatus;
        target.RowVersion += 1;
        target.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync(tenantId, actorUserId, FamilyAuditActions.TenantMemberStatusChanged, FamilyAuditTargetTypes.TenantMember, targetUserId,
            before: new { status = beforeStatus }, after: new { status = target.Status }, reason: request.Reason ?? "状态变更。", relatedRunId: null, cancellationToken);

        return new ServiceResult(200, "成员状态已更新。", ToSummary(target));
    }

    /// <inheritdoc />
    public async Task<ServiceResult> TransferOwnerAsync(long tenantId, long actorUserId, TenantOwnerTransferRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null || request.NewOwnerUserId <= 0)
            return new ServiceResult(422, "新 owner 用户主键无效。", ErrorCode: ApiErrorCodes.ValidationFailed);

        var tenant = await _db.Tenants.SingleOrDefaultAsync(x => x.Id == tenantId, cancellationToken);
        if (tenant is null) return new ServiceResult(404, "家庭不存在。", ErrorCode: ApiErrorCodes.ResourceNotFound);
        if (tenant.OwnerUserId != actorUserId)
            return new ServiceResult(403, "只有当前 active owner 才能发起 owner 转让。", ErrorCode: ApiErrorCodes.AccessDenied);

        var actor = await _db.TenantMembers.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.UserId == actorUserId, cancellationToken);
        if (actor is null || actor.Status != Active || actor.Role != Owner)
            return new ServiceResult(403, "当前账号不是 active owner。", ErrorCode: ApiErrorCodes.AccessDenied);

        var newOwner = await _db.TenantMembers.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.UserId == request.NewOwnerUserId, cancellationToken);
        if (newOwner is null) return new ServiceResult(404, "新 owner 不属于当前家庭。", ErrorCode: ApiErrorCodes.ResourceNotFound);
        if (newOwner.Status != Active)
            return new ServiceResult(422, "新 owner 必须为 active 状态。", ErrorCode: ApiErrorCodes.OwnerTransferInvalidReceiver);
        if (actor.RowVersion != request.RowVersion)
            return new ServiceResult(409, "家庭信息已被他人更新，请刷新后重试。", ErrorCode: ApiErrorCodes.TenantOptimisticLockConflict);

        var now = DateTime.UtcNow;
        actor.Role = Admin;
        actor.RowVersion += 1;
        actor.UpdatedAt = now;
        newOwner.Role = Owner;
        newOwner.RowVersion += 1;
        newOwner.UpdatedAt = now;
        tenant.OwnerUserId = newOwner.UserId;
        tenant.UpdatedAt = now;
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync(tenantId, actorUserId, FamilyAuditActions.TenantOwnerTransferred, FamilyAuditTargetTypes.TenantMember, newOwner.UserId,
            before: new { ownerUserId = actorUserId, oldOwnerRole = Owner, newOwnerRole = newOwner.Role },
            after: new { ownerUserId = newOwner.UserId, oldOwnerRole = Admin, newOwnerRole = Owner },
            reason: $"owner 由 {actorUserId} 转让给 {newOwner.UserId}。", relatedRunId: null, cancellationToken);

        return new ServiceResult(200, "owner 已转让。", ToSummary(newOwner));
    }

    private static TenantMemberSummaryView ToSummary(TenantMember tm) => new(
        tm.UserId, "HomeMind 用户", null, tm.Role, tm.Status, tm.JoinedAt, "Asia/Shanghai", "zh-CN",
        IsCurrentUserOwner: tm.Role == Owner, HasPendingInvitation: false, RowVersion: tm.RowVersion);
}
