using HomeMind.Business.IServices.Family;
using HomeMind.Common.Model.Entities.Family;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.Family;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;

namespace HomeMind.Business.Services.Family;

/// <summary>
/// 家庭成员服务实现。职责：
/// - 列表：按 homeId 返回未删除成员。
/// - 创建：默认状态 <c>active</c>。
/// - 更新：仅允许 active ↔ away 双向切换。
/// - 纠偏：任何进入/退出终态的操作在同一事务内写入审计及终端三字段。
/// </summary>
public sealed class FamilyMemberServices : IFamilyMemberServices
{
    private readonly HomeMindDbContext _db;
    private readonly IFamilyAuditLogger _audit;

    /// <summary>构造家庭成员服务。</summary>
    /// <param name="db">数据库上下文。</param>
    /// <param name="audit">家庭审计日志写入器。</param>
    public FamilyMemberServices(HomeMindDbContext db, IFamilyAuditLogger audit)
    {
        _db = db;
        _audit = audit;
    }

    /// <inheritdoc />
    public async Task<ServiceResult> ListAsync(long homeId, CancellationToken cancellationToken = default)
    {
        var members = await _db.FamilyMembers
            .Where(x => x.HomeId == homeId && x.DeletedAt == null)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);
        return new ServiceResult(200, "查询成功。", members.Select(ToView).ToArray());
    }

    /// <inheritdoc />
    public async Task<ServiceResult> CreateAsync(long homeId, long actorUserId, FamilyMemberCreateRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Relation))
            return new ServiceResult(422, "成员名称和关系为必填项。");

        var memberStatus = request.MemberStatus ?? FamilyMemberStatus.Active;
        if (memberStatus is not (FamilyMemberStatus.Active or FamilyMemberStatus.Away))
            return new ServiceResult(422, "仅允许创建状态为 active 或 away 的成员。");

        var now = DateTime.UtcNow;
        var member = new FamilyMember
        {
            HomeId = homeId,
            Name = request.Name.Trim(),
            Relation = request.Relation.Trim(),
            Birthday = request.Birthday,
            IsElderly = request.IsElderly,
            IsChild = request.IsChild,
            IsPrimary = request.IsPrimary,
            MemberStatus = memberStatus,
            Preferences = request.Preferences,
            CreatedByUserId = actorUserId,
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.FamilyMembers.Add(member);
        await _db.SaveChangesAsync(cancellationToken);

        return new ServiceResult(201, "家庭成员已创建。", ToView(member));
    }

    /// <inheritdoc />
    public async Task<ServiceResult> UpdateAsync(long homeId, long actorUserId, long memberId, FamilyMemberUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var member = await _db.FamilyMembers
            .SingleOrDefaultAsync(x => x.Id == memberId && x.HomeId == homeId && x.DeletedAt == null, cancellationToken);
        if (member is null) return new ServiceResult(404, "请求的家庭成员不存在。");

        if (request.MemberStatus is { } targetStatus)
        {
            if (member.MemberStatus is not (FamilyMemberStatus.Active or FamilyMemberStatus.Away))
                return new ServiceResult(422, "终态成员只能通过更正接口修改。");
            if (targetStatus is not (FamilyMemberStatus.Active or FamilyMemberStatus.Away))
                return new ServiceResult(422, "仅允许在 active 与 away 之间切换；终态变更需使用更正接口。");
            if (targetStatus == member.MemberStatus)
                return new ServiceResult(422, "目标状态与当前状态相同，无需更新。");
            member.MemberStatus = targetStatus;
        }

        if (request.Name is { } name) member.Name = name.Trim();
        if (request.Relation is { } relation) member.Relation = relation.Trim();
        if (request.Birthday is { } birthday) member.Birthday = birthday;
        if (request.IsElderly is { } isElderly) member.IsElderly = isElderly;
        if (request.IsChild is { } isChild) member.IsChild = isChild;
        if (request.IsPrimary is { } isPrimary) member.IsPrimary = isPrimary;
        if (request.Preferences is { } preferences) member.Preferences = preferences;

        member.UpdatedAt = DateTime.UtcNow;
        member.RowVersion++;
        await _db.SaveChangesAsync(cancellationToken);

        return new ServiceResult(200, "家庭成员信息已更新。", ToView(member));
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">当目标状态非法时抛出。</exception>
    public async Task<ServiceResult> CorrectAsync(long homeId, long actorUserId, long memberId, FamilyMemberCorrectionRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.MemberStatus))
            return new ServiceResult(422, "更正操作必须提供目标状态。");

        var member = await _db.FamilyMembers
            .SingleOrDefaultAsync(x => x.Id == memberId && x.HomeId == homeId && x.DeletedAt == null, cancellationToken);
        if (member is null) return new ServiceResult(404, "请求的家庭成员不存在。");

        var isTerminal = request.MemberStatus is FamilyMemberStatus.PermanentlyLeft or FamilyMemberStatus.Deceased;
        var isRestoring = member.MemberStatus is FamilyMemberStatus.PermanentlyLeft or FamilyMemberStatus.Deceased
                          && request.MemberStatus is FamilyMemberStatus.Active or FamilyMemberStatus.Away;

        if (!isTerminal && !isRestoring)
            return new ServiceResult(422, "更正接口只能用于进入终态或从终态恢复；普通状态变更请使用 PUT。");

        if (isTerminal && string.IsNullOrWhiteSpace(request.Reason))
            return new ServiceResult(422, "进入终态时必须提供原因说明。");

        var before = ToAuditSnapshot(member);
        var now = DateTime.UtcNow;
        member.MemberStatus = request.MemberStatus;
        member.TerminalCorrectedByUserId = actorUserId;
        member.TerminalCorrectionReason = request.Reason?.Trim();
        member.TerminalCorrectedAt = now;
        member.UpdatedAt = now;
        member.RowVersion++;

        var auditAction = isRestoring ? FamilyAuditActions.MemberTerminalRestore : FamilyAuditActions.MemberCorrection;
        await _audit.LogAsync(homeId, actorUserId, auditAction, FamilyAuditTargetTypes.FamilyMember, member.Id, before, ToAuditSnapshot(member), request.Reason?.Trim(), null, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return new ServiceResult(200, "家庭成员状态已更正。", ToView(member));
    }

    /// <summary>将成员实体映射为视图，不返回内部字段。</summary>
    private static FamilyMemberView ToView(FamilyMember m) => new(
        m.Id, m.Name, m.Relation, m.Birthday, m.IsElderly, m.IsChild, m.IsPrimary,
        m.MemberStatus, m.Preferences, m.CreatedAt, m.UpdatedAt);

    /// <summary>构造用于审计 before/after 快照的匿名对象。</summary>
    private static object ToAuditSnapshot(FamilyMember m) => new
    {
        m.Id, m.Name, m.Relation, m.Birthday, m.IsElderly, m.IsChild, m.IsPrimary,
        m.MemberStatus, m.Preferences,
        m.TerminalCorrectedByUserId, m.TerminalCorrectionReason, m.TerminalCorrectedAt
    };
}
