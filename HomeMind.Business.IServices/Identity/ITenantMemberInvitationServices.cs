using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.Identity;

namespace HomeMind.Business.IServices.Identity;

/// <summary>家庭成员邀请服务：以手机号 SHA-256 匹配 <c>user_identities</c>，仅允许已验证账户接受。</summary>
public interface ITenantMemberInvitationServices
{
    /// <summary>创建一条邀请；同 (tenant_id, subject_hash) 仅允许一条 pending 邀请。</summary>
    /// <param name="tenantId">当前家庭（租户）主键。</param>
    /// <param name="actorUserId">当前用户主键（owner/admin 发起人）。</param>
    /// <param name="request">邀请请求体，含手机号与 proposed_role。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>成功返回新邀请视图；同标识已 pending 返回 409。</returns>
    Task<ServiceResult> CreateAsync(long tenantId, long actorUserId, TenantMemberInvitationCreateRequest request, CancellationToken cancellationToken = default);

    /// <summary>列出当前家庭邀请；按状态过滤，已过期项按计算语义视为 expired 但不写回填。</summary>
    /// <param name="tenantId">当前家庭（租户）主键。</param>
    /// <param name="actorUserId">当前用户主键。</param>
    /// <param name="statusFilter">可选状态过滤：pending/accepted/expired/revoked。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>邀请列表分页视图（按过期时间升序）。</returns>
    Task<ServiceResult> ListAsync(long tenantId, long actorUserId, string? statusFilter, CancellationToken cancellationToken = default);

    /// <summary>撤销一条 pending 邀请；非 pending 状态返回 409。</summary>
    /// <param name="tenantId">当前家庭（租户）主键。</param>
    /// <param name="actorUserId">当前用户主键。</param>
    /// <param name="invitationId">邀请主键。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>成功返回撤销后邀请视图；跨家庭返回 404。</returns>
    Task<ServiceResult> RevokeAsync(long tenantId, long actorUserId, long invitationId, CancellationToken cancellationToken = default);

    /// <summary>当前用户接受邀请；服务端重新计算手机号 SHA-256 并与 user_identities 已验证标识匹配。</summary>
    /// <param name="actorUserId">当前用户主键。</param>
    /// <param name="request">接受请求体，含邀请主键与待验证手机号原文。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>成功返回新加入的成员摘要；邀请不存在 / 哈希不匹配 / 未验证返回 404。</returns>
    Task<ServiceResult> AcceptAsync(long actorUserId, TenantMemberInvitationAcceptRequest request, CancellationToken cancellationToken = default);
}
