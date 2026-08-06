using System;
using HomeMind.Api.Services;
using HomeMind.Common.Model.ViewModel.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace HomeMind.Api.Authorization;

/// <summary>
/// 操作过滤器：校验路由参数中的 <c>homeId</c> 与 JWT 推导的 <c>TenantId</c> 相等。
/// 不相等时短路返回 403，禁止客户端覆盖家庭归属。
/// </summary>
/// <remarks>
/// 用法：在 Action 上标注 <c>[ServiceFilter(typeof(RequireHomeOwnerAttribute))]</c>，
/// 并确保路由模板包含 <c>{homeId:long}</c> 参数。
/// </remarks>
[AttributeUsage(AttributeTargets.Method)]
public sealed class RequireHomeOwnerAttribute : ActionFilterAttribute
{
    /// <summary>在执行 Action 前校验 homeId 与租户归属。</summary>
    /// <param name="context">操作执行上下文。</param>
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var user = context.HttpContext.Items["HomeMind.User"] as UserContext;
        if (user is null)
        {
            context.Result = new ObjectResult(ApiResponse<object>.Fail(ApiErrorCodes.AccessTokenInvalid, "未提供访问令牌，或访问令牌已过期。"))
            {
                StatusCode = 401
            };
            return;
        }

        if (!context.RouteData.Values.TryGetValue("homeId", out var raw) || !long.TryParse(raw?.ToString(), out var routeHomeId))
        {
            context.Result = new ObjectResult(ApiResponse<object>.Fail(ApiErrorCodes.ResourceNotFound, "请求的 homeId 格式无效。"))
            {
                StatusCode = 400
            };
            return;
        }

        if (routeHomeId != user.TenantId)
        {
            context.Result = new ObjectResult(ApiResponse<object>.Fail(ApiErrorCodes.AccessDenied, "当前访问令牌无权操作该家庭的数据。"))
            {
                StatusCode = 403
            };
        }
    }
}
