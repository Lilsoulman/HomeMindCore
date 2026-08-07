using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.Identity;

namespace HomeMind.Business.IServices.Identity;

/// <summary>家庭成员受控管理服务：角色变更、状态停启、owner 转让；只 owner/admin 可调。</summary>
public interface ITenantMemberServices
{
    /// <summary>列出当前家庭所有成员（含用户资料与角色/状态/行版本）。</summary>
    /// <param name="tenantId">当前家庭（租户）主键，由 JWT 推导。</param>
    /// <param name="actorUserId">当前用户主键（用于识别 owner 标记）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>成员摘要列表；跨家庭由 <c>RequireHomeOwner</c> 兜底。</returns>
    Task<ServiceResult> ListMembersAsync(long tenantId, long actorUserId, CancellationToken cancellationToken = default);

    /// <summary>变更目标成员角色；<c>owner</c> 必须走 owner-transfer。</summary>
    /// <param name="tenantId">当前家庭（租户）主键。</param>
    /// <param name="actorUserId">当前用户主键。</param>
    /// <param name="targetUserId">目标成员用户主键。</param>
    /// <param name="request">角色变更请求体。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>成功返回更新后成员视图；新角色为 owner / 跨家庭 / 乐观锁冲突返回相应错误。</returns>
    Task<ServiceResult> ChangeRoleAsync(long tenantId, long actorUserId, long targetUserId, TenantMemberRoleUpdateRequest request, CancellationToken cancellationToken = default);

    /// <summary>变更目标成员启用/停用状态；不能停用最后一个 active owner。</summary>
    /// <param name="tenantId">当前家庭（租户）主键。</param>
    /// <param name="actorUserId">当前用户主键。</param>
    /// <param name="targetUserId">目标成员用户主键。</param>
    /// <param name="request">状态变更请求体。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>成功返回更新后成员视图；停用最后一个 active owner 或乐观锁冲突返回相应错误。</returns>
    Task<ServiceResult> ChangeStatusAsync(long tenantId, long actorUserId, long targetUserId, TenantMemberStatusUpdateRequest request, CancellationToken cancellationToken = default);

    /// <summary>owner 转让：同事务更新 <c>tenants.owner_user_id</c> 与双方成员角色。</summary>
    /// <param name="tenantId">当前家庭（租户）主键。</param>
    /// <param name="actorUserId">当前用户主键；必须为当前 active owner。</param>
    /// <param name="request">转让请求体，含新 owner 用户主键与行版本。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>成功返回新 owner 视图；非 owner 发起 / 受让方 suspended / 乐观锁冲突返回相应错误。</returns>
    Task<ServiceResult> TransferOwnerAsync(long tenantId, long actorUserId, TenantOwnerTransferRequest request, CancellationToken cancellationToken = default);
}
