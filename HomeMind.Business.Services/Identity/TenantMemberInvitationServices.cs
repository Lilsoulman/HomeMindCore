using System.Security.Cryptography;
using System.Text;
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
/// 家庭成员邀请服务：以手机号 SHA-256（E.164 规范化后，无 pepper）匹配 <c>user_identities.subject_hash</c>，
/// 仅允许已验证账户接受；过期按计算语义 <c>expires_at &gt; now</c>，不写回填。
/// </summary>
public sealed class TenantMemberInvitationServices : ITenantMemberInvitationServices
{
    private static readonly TimeSpan DefaultInvitationLifetime = TimeSpan.FromDays(7);
    private static readonly HashSet<string> ValidProposedRoles = new(StringComparer.Ordinal) { "admin", "member", "viewer" };
    private static readonly HashSet<string> ValidStatusFilters = new(StringComparer.Ordinal) { "pending", "accepted", "expired", "revoked" };

    private readonly HomeMindDbContext _db;
    private readonly IFamilyAuditLogger _audit;

    /// <summary>构造家庭成员邀请服务。</summary>
    /// <param name="db">数据库上下文。</param>
    /// <param name="audit">家庭审计日志写入器。</param>
    public TenantMemberInvitationServices(HomeMindDbContext db, IFamilyAuditLogger audit)
    {
        _db = db;
        _audit = audit;
    }

    /// <inheritdoc />
    public async Task<ServiceResult> CreateAsync(long tenantId, long actorUserId, TenantMemberInvitationCreateRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Phone) || !TryNormalizePhone(request.Phone, out var phone))
            return new ServiceResult(422, "手机号格式无效，请使用 E.164 格式。", ErrorCode: ApiErrorCodes.ValidationFailed);
        if (string.IsNullOrWhiteSpace(request.ProposedRole) || !ValidProposedRoles.Contains(request.ProposedRole))
            return new ServiceResult(422, "proposed_role 必须是 admin/member/viewer 之一。", ErrorCode: ApiErrorCodes.ValidationFailed);

        var subjectHash = Sha256(phone);
        var existing = await _db.TenantMemberInvitations.SingleOrDefaultAsync(
            x => x.TenantId == tenantId && x.SubjectHash == subjectHash && x.Status == "pending" && x.ExpiresAt > DateTime.UtcNow,
            cancellationToken);
        if (existing is not null)
            return new ServiceResult(409, "该手机号在当前家庭已存在未结邀请。", ErrorCode: ApiErrorCodes.TenantInvitationConflict);

        var now = DateTime.UtcNow;
        var invitation = new TenantMemberInvitation
        {
            TenantId = tenantId,
            InvitedByUserId = actorUserId,
            SubjectKind = "phone",
            SubjectHash = subjectHash,
            ProposedRole = request.ProposedRole,
            Status = "pending",
            ExpiresAt = now.Add(DefaultInvitationLifetime),
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.TenantMemberInvitations.Add(invitation);
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync(tenantId, actorUserId, FamilyAuditActions.TenantInvitationCreated, FamilyAuditTargetTypes.TenantInvitation, invitation.Id,
            before: null, after: new { phone, proposedRole = invitation.ProposedRole, expiresAt = invitation.ExpiresAt },
            reason: $"邀请 {phone} 加入家庭，角色 {invitation.ProposedRole}。", relatedRunId: null, cancellationToken);

        return new ServiceResult(201, "邀请已创建。", ToView(invitation));
    }

    /// <inheritdoc />
    public async Task<ServiceResult> ListAsync(long tenantId, long actorUserId, string? statusFilter, CancellationToken cancellationToken = default)
    {
        if (statusFilter is not null && !ValidStatusFilters.Contains(statusFilter))
            return new ServiceResult(422, "状态过滤仅支持 pending/accepted/expired/revoked。", ErrorCode: ApiErrorCodes.ValidationFailed);

        var now = DateTime.UtcNow;
        var items = await _db.TenantMemberInvitations
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.ExpiresAt)
            .ToListAsync(cancellationToken);

        var views = items.Select(ToView).ToList();
        if (statusFilter is "pending")
            views = views.Where(x => x.Status == "pending" && x.ExpiresAt > now).ToList();
        else if (statusFilter is not null)
            views = views.Where(x => x.Status == statusFilter).ToList();

        return new ServiceResult(200, "查询成功。", new TenantMemberInvitationListView(views, Cursor: null));
    }

    /// <inheritdoc />
    public async Task<ServiceResult> RevokeAsync(long tenantId, long actorUserId, long invitationId, CancellationToken cancellationToken = default)
    {
        var invitation = await _db.TenantMemberInvitations.SingleOrDefaultAsync(
            x => x.Id == invitationId && x.TenantId == tenantId, cancellationToken);
        if (invitation is null) return new ServiceResult(404, "邀请不存在或不属于当前家庭。", ErrorCode: ApiErrorCodes.ResourceNotFound);
        if (invitation.Status != "pending")
            return new ServiceResult(409, "该邀请已处于终态，无法撤销。", ErrorCode: ApiErrorCodes.Conflict);

        var now = DateTime.UtcNow;
        invitation.Status = "revoked";
        invitation.RevokedAt = now;
        invitation.UpdatedAt = now;
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync(tenantId, actorUserId, FamilyAuditActions.TenantInvitationRevoked, FamilyAuditTargetTypes.TenantInvitation, invitation.Id,
            before: new { status = "pending" }, after: new { status = "revoked" }, reason: "邀请被撤销。", relatedRunId: null, cancellationToken);

        return new ServiceResult(200, "邀请已撤销。", ToView(invitation));
    }

    /// <inheritdoc />
    public async Task<ServiceResult> AcceptAsync(long actorUserId, TenantMemberInvitationAcceptRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null || request.InvitationId <= 0 || string.IsNullOrWhiteSpace(request.Phone) || !TryNormalizePhone(request.Phone, out var phone))
            return new ServiceResult(422, "邀请主键或手机号无效。", ErrorCode: ApiErrorCodes.ValidationFailed);

        var invitation = await _db.TenantMemberInvitations.SingleOrDefaultAsync(x => x.Id == request.InvitationId, cancellationToken);
        if (invitation is null) return new ServiceResult(404, "邀请不存在。", ErrorCode: ApiErrorCodes.ResourceNotFound);
        if (invitation.Status != "pending" || invitation.ExpiresAt <= DateTime.UtcNow)
            return new ServiceResult(409, "邀请已过期或已失效。", ErrorCode: ApiErrorCodes.Conflict);

        var subjectHash = Sha256(phone);
        if (!invitation.SubjectHash.SequenceEqual(subjectHash))
            return new ServiceResult(404, "邀请与当前账户的手机号不匹配。", ErrorCode: ApiErrorCodes.TenantInvitationIdentityNotMatched);

        var identity = await _db.UserIdentities.SingleOrDefaultAsync(
            x => x.UserId == actorUserId && x.Provider == "phone" && x.SubjectKind == "phone_number" && x.SubjectHash == subjectHash,
            cancellationToken);
        if (identity is null || identity.RevokedAt is not null)
            return new ServiceResult(404, "当前账户未通过该手机号的验证。", ErrorCode: ApiErrorCodes.TenantInvitationIdentityNotMatched);

        var now = DateTime.UtcNow;
        var member = new TenantMember
        {
            TenantId = invitation.TenantId,
            UserId = actorUserId,
            Role = invitation.ProposedRole,
            Status = "active",
            JoinedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.TenantMembers.Add(member);

        invitation.Status = "accepted";
        invitation.AcceptedUserId = actorUserId;
        invitation.AcceptedAt = now;
        invitation.UpdatedAt = now;
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync(invitation.TenantId, actorUserId, FamilyAuditActions.TenantInvitationAccepted, FamilyAuditTargetTypes.TenantInvitation, invitation.Id,
            before: new { status = "pending" }, after: new { status = "accepted", userId = actorUserId, role = member.Role },
            reason: "受邀人接受邀请并加入家庭。", relatedRunId: null, cancellationToken);

        return new ServiceResult(200, "已接受邀请并加入家庭。", new { TenantId = invitation.TenantId, Role = member.Role });
    }

    /// <summary>规范化手机号并返回是否有效（E.164：+ 号开头，8-15 位数字）。</summary>
    private static bool TryNormalizePhone(string raw, out string phone)
    {
        phone = raw.Trim();
        return phone.Length is >= 8 and <= 15 && phone[0] == '+' && phone[1..].All(char.IsAsciiDigit);
    }

    /// <summary>计算手机号规范字符串的 SHA-256 摘要（与 <c>user_identities.subject_hash</c> 同口径）。</summary>
    private static byte[] Sha256(string value) => SHA256.HashData(Encoding.UTF8.GetBytes(value));

    /// <summary>将邀请实体转换为脱敏视图。</summary>
    private static TenantMemberInvitationView ToView(TenantMemberInvitation i) => new(
        i.Id, i.InvitedByUserId, i.SubjectKind, Convert.ToHexString(i.SubjectHash), i.ProposedRole, i.Status,
        i.ExpiresAt, i.AcceptedUserId, i.AcceptedAt, i.RevokedAt, i.CreatedAt, i.RowVersion);
}
