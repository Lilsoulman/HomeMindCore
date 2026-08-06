using HomeMind.Api.Services;
using HomeMind.Common.Model.ViewModel.Common;
using Microsoft.AspNetCore.Mvc;

namespace HomeMind.Api.Controllers.Base;

/// <summary>
/// 基础控制器，提供已登录用户上下文和统一的常用错误响应。
/// 所有业务 Controller 必须继承自本类以复用用户上下文获取、401/404 等统一响应。
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>从 <see cref="HttpContext.Items"/> 提取访问令牌解析得到的当前用户上下文。</summary>
    /// <param name="user">输出参数，命中时返回当前用户；否则为默认值。</param>
    /// <returns>若 HttpContext 中存在用户上下文则返回 true，否则返回 false。</returns>
    protected bool TryGetUser(out UserContext user)
    {
        user = HttpContext.Items["HomeMind.User"] as UserContext ?? default!;
        return user is not null;
    }

    /// <summary>生成统一的 401 响应：访问令牌缺失或已过期。</summary>
    /// <typeparam name="T">响应负载类型。</typeparam>
    /// <returns>HTTP 401 与业务错误码 20001 的统一响应体。</returns>
    protected ActionResult<ApiResponse<T>> UnauthorizedResult<T>() => Unauthorized(ApiResponse<T>.Fail(ApiErrorCodes.AccessTokenInvalid, "未提供访问令牌，或访问令牌已过期。"));

    /// <summary>生成统一的 404 响应：请求资源不存在。</summary>
    /// <typeparam name="T">响应负载类型。</typeparam>
    /// <returns>HTTP 404 与业务错误码 30000 的统一响应体。</returns>
    protected ActionResult<ApiResponse<T>> NotFoundResult<T>() => NotFound(ApiResponse<T>.Fail(ApiErrorCodes.ResourceNotFound, "请求的资源不存在。"));
}
