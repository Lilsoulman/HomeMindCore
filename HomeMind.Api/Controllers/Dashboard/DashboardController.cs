using System;
using System.Threading;
using System.Threading.Tasks;
using HomeMind.Api.Controllers.Base;
using HomeMind.Api.Services;
using HomeMind.Business.IServices.Dashboard;
using HomeMind.Common.Model.ViewModel.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HomeMind.Api.Controllers.Dashboard;

/// <summary>仪表板聚合接口，模块之间可独立降级，单模块失败不影响其他模块返回。</summary>
/// <remarks>需要 <c>smart_home.read</c> 权限；用户与租户从 JWT 派生。</remarks>
[Authorize]
[Route("api/v1/dashboard")]
public sealed class DashboardController : ApiControllerBase
{
    private readonly IDashboardServices _dashboard;

    /// <summary>构造仪表板控制器。</summary>
    /// <param name="dashboard">仪表板聚合服务。</param>
    public DashboardController(IDashboardServices dashboard) => _dashboard = dashboard;

    /// <summary>获取仪表板聚合视图，包含 Home、Scenes、Todos、Calendar、Suggestion 模块。</summary>
    /// <remarks>权限：<c>smart_home.read</c>。每个模块独立声明 <c>available</c> 或 <c>unavailable</c> 状态与可读消息。</remarks>
    /// <returns>仪表板视图的统一响应。</returns>
    [Authorize(Policy = PermissionNames.SmartHomeRead)]
    [HttpGet]
    public async Task<ActionResult<ApiResponse<object>>> Get() =>
        ToResponse(await WithUserAsync((user, token) => _dashboard.GetAsync(user.UserId, user.TenantId, token)));

    /// <summary>在用户上下文就绪时执行给定的业务回调，否则返回 401。</summary>
    /// <param name="action">执行业务逻辑的回调。</param>
    /// <returns>业务执行结果 <see cref="ServiceResult"/>。</returns>
    private async Task<ServiceResult> WithUserAsync(Func<UserContext, CancellationToken, Task<ServiceResult>> action) =>
        TryGetUser(out var user)
            ? await action(user, HttpContext.RequestAborted)
            : new ServiceResult(401, "未提供访问令牌，或访问令牌已过期。");

    /// <summary>将 <see cref="ServiceResult"/> 转换为统一 HTTP 响应。</summary>
    /// <param name="result">业务执行结果。</param>
    /// <returns>统一响应体与对应状态码。</returns>
    private static ActionResult<ApiResponse<object>> ToResponse(ServiceResult result) => result.Succeeded
        ? new ObjectResult(new ApiResponse<object>(0, result.Message, result.Data)) { StatusCode = result.StatusCode }
        : new ObjectResult(ApiResponse<object>.Fail(result.Code, result.Message)) { StatusCode = result.StatusCode };
}
