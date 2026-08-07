using System;
using System.Threading;
using System.Threading.Tasks;
using HomeMind.Api.Controllers.Base;
using HomeMind.Api.Services;
using HomeMind.Business.IServices.Identity;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HomeMind.Api.Controllers.Web;

/// <summary>
/// Web 导航偏好控制器（V2.4 B19）：返回当前家庭当前角色可见的导航，owner/admin 可写入。
/// 租户与角色均由 JWT 推导，路径不含 homeId，前端菜单随服务端返回显隐/排序。
/// </summary>
[Authorize]
[Route("api/v1/web/navigation")]
public sealed class WebNavigationController : ApiControllerBase
{
    private readonly IWebNavigationPreferencesServices _navigation;

    /// <summary>构造 Web 导航偏好控制器。</summary>
    /// <param name="navigation">Web 导航偏好服务。</param>
    public WebNavigationController(IWebNavigationPreferencesServices navigation) => _navigation = navigation;

    /// <summary>返回当前家庭当前角色的导航偏好（白名单 + 持久化偏好合并）。</summary>
    /// <remarks>权限：<c>tenant.read</c>。无偏好时全部 route_key 默认 enabled=true 并按默认 sort_order。</remarks>
    /// <returns>导航偏好视图统一响应。</returns>
    [Authorize(Policy = PermissionNames.TenantRead)]
    [HttpGet]
    public async Task<ActionResult<ApiResponse<object>>> Get() =>
        ToResponse(await WithUserAsync((user, token) => _navigation.GetForCurrentAsync(user.TenantId, user.Role, token)));

    /// <summary>写入当前家庭某角色的导航偏好。</summary>
    /// <remarks>权限：<c>tenant.member.manage</c>（owner/admin）。route_key 必须命中后端静态白名单。</remarks>
    /// <param name="request">偏好更新请求体。</param>
    /// <returns>更新后导航偏好视图统一响应。</returns>
    [Authorize(Policy = PermissionNames.TenantMemberManage)]
    [HttpPut]
    public async Task<ActionResult<ApiResponse<object>>> Update(WebNavigationPreferencesUpdateRequest request) =>
        ToResponse(await WithUserAsync((user, token) => _navigation.UpdateForRoleAsync(user.TenantId, user.UserId, request, token)));

    /// <summary>在用户上下文就绪时执行给定的业务回调，否则返回 401。</summary>
    /// <param name="action">执行业务逻辑的回调。</param>
    /// <returns>业务执行结果 <see cref="ServiceResult"/>。</returns>
    private async Task<ServiceResult> WithUserAsync(Func<UserContext, CancellationToken, Task<ServiceResult>> action) => TryGetUser(out var user)
        ? await action(user, HttpContext.RequestAborted)
        : new ServiceResult(401, "未提供访问令牌，或访问令牌已过期。");

    /// <summary>将 <see cref="ServiceResult"/> 转换为统一 HTTP 响应。</summary>
    /// <param name="result">业务执行结果。</param>
    /// <returns>统一响应体与对应状态码。</returns>
    private static ActionResult<ApiResponse<object>> ToResponse(ServiceResult result) => result.Succeeded
        ? new ObjectResult(new ApiResponse<object>(0, result.Message, result.Data)) { StatusCode = result.StatusCode }
        : new ObjectResult(ApiResponse<object>.Fail(result.Code, result.Message)) { StatusCode = result.StatusCode };
}
