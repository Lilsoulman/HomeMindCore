using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.Identity;

namespace HomeMind.Business.IServices.Identity;

/// <summary>Web 导航偏好服务：合并后端静态白名单 <c>NexusWebNavigationKeys.All</c> 与当前家庭角色偏好。</summary>
public interface IWebNavigationPreferencesServices
{
    /// <summary>返回当前家庭当前角色的可见导航；偏好缺失时使用默认 sort_order 与 enabled=true。</summary>
    /// <param name="tenantId">当前家庭（租户）主键。</param>
    /// <param name="role">当前用户角色：owner/admin/member/viewer。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>导航偏好视图。</returns>
    Task<ServiceResult> GetForCurrentAsync(long tenantId, string role, CancellationToken cancellationToken = default);

    /// <summary>写入当前家庭某角色的导航偏好；只 owner/admin 可调；route_key 必须命中白名单。</summary>
    /// <param name="tenantId">当前家庭（租户）主键。</param>
    /// <param name="actorUserId">当前用户主键（owner/admin）。</param>
    /// <param name="request">偏好更新请求体，含目标角色与偏好项列表。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>成功返回更新后导航视图；route_key 未发布返回 422。</returns>
    Task<ServiceResult> UpdateForRoleAsync(long tenantId, long actorUserId, WebNavigationPreferencesUpdateRequest request, CancellationToken cancellationToken = default);
}
