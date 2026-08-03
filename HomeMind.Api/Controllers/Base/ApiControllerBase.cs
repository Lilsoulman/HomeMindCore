using HomeMind.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace HomeMind.Api.Controllers.Base;

/// <summary>
/// 基础控制器，提供已登录用户上下文和统一的常用错误响应。
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
public abstract class ApiControllerBase : ControllerBase
{
    protected bool TryGetUser(out UserContext user)
    {
        user = HttpContext.Items["HomeMind.User"] as UserContext ?? default!;
        return user is not null;
    }

    protected ActionResult<ApiResponse<T>> UnauthorizedResult<T>() => Unauthorized(ApiResponse<T>.Fail(401, "Bearer access token is required or expired."));
    protected ActionResult<ApiResponse<T>> NotFoundResult<T>() => NotFound(ApiResponse<T>.Fail(404, "Resource not found."));
}
