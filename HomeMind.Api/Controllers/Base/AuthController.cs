using System.Threading.Tasks;
using HomeMind.Api.Services;
using HomeMind.Business.IServices.Base;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.Base;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HomeMind.Api.Controllers.Base;

/// <summary>身份认证模块，负责账户注册、登录、令牌续期、当前用户信息查询和注销。</summary>
/// <remarks>所有受保护资源都从 JWT 派生用户与租户；本控制器不会接受客户端覆盖 <c>userId</c> 或 <c>tenantId</c>。</remarks>
[Route("api/v1/auth")]
public sealed class AuthController : ApiControllerBase
{
    private readonly IBaseUserServices _baseUserServices;
    private readonly AccessTokenValidator _accessTokens;

    /// <summary>构造认证控制器。</summary>
    /// <param name="baseUserServices">用户与认证业务服务。</param>
    /// <param name="accessTokens">访问令牌校验与撤销服务。</param>
    public AuthController(IBaseUserServices baseUserServices, AccessTokenValidator accessTokens)
    {
        _baseUserServices = baseUserServices;
        _accessTokens = accessTokens;
    }

    /// <summary>使用手机号和密码注册个人账户，并返回访问令牌与刷新令牌。</summary>
    /// <remarks>权限：匿名。成功返回 200，参数无效返回 422，手机号重复返回 409。</remarks>
    /// <param name="request">注册请求体，必须包含手机号、密码、可选的展示名、安装 ID 和平台。</param>
    /// <returns>统一响应包装的会话信息，包含 AccessToken、RefreshToken、UserId 和 TenantId。</returns>
    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<ActionResult<ApiResponse<AuthSessionViewModel>>> Register(RegisterRequest request)
        => ToAuthenticationResponse(await _baseUserServices.RegisterAsync(request, HttpContext.RequestAborted));

    /// <summary>使用手机号和密码登录，创建或更新当前设备会话。</summary>
    /// <remarks>权限：匿名。成功返回 200；手机号或密码错误返回 400 + 20000；失败过多将锁定账户。</remarks>
    /// <param name="request">登录请求体，包含手机号、密码与可选的安装 ID 与平台。</param>
    /// <returns>统一响应包装的会话信息。</returns>
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<AuthSessionViewModel>>> Login(LoginRequest request)
        => ToAuthenticationResponse(await _baseUserServices.LoginAsync(request, HttpContext.RequestAborted));

    /// <summary>使用有效的刷新令牌换取新的访问令牌和刷新令牌。</summary>
    /// <remarks>权限：匿名。返回 200 续期成功；令牌无效或被吊销返回 401 + 20002；家族级重放将整族撤销。</remarks>
    /// <param name="request">刷新请求体，包含刷新令牌明文。</param>
    /// <returns>统一响应包装的会话信息。</returns>
    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<ActionResult<ApiResponse<AuthSessionViewModel>>> Refresh(RefreshRequest request)
        => ToAuthenticationResponse(await _baseUserServices.RefreshAsync(request, HttpContext.RequestAborted));

    /// <summary>获取当前访问令牌对应用户的基础资料。</summary>
    /// <remarks>权限：<c>identity.read</c>。未携带有效令牌返回 401；用户不存在返回 404。</remarks>
    /// <returns>当前用户的基础资料视图。</returns>
    [Authorize(Policy = PermissionNames.IdentityRead)]
    [HttpGet("me")]
    public async Task<ActionResult<ApiResponse<BaseUserViewModel>>> Me()
    {
        if (!TryGetUser(out var user)) return UnauthorizedResult<BaseUserViewModel>();
        var result = await _baseUserServices.GetCurrentUserAsync(user.UserId, HttpContext.RequestAborted);
        return result is null ? NotFoundResult<BaseUserViewModel>() : Ok(ApiResponse<BaseUserViewModel>.Ok(result with { Role = user.Role }));
    }

    /// <summary>注销当前设备会话，并立即撤销当前访问令牌和该设备的刷新令牌。</summary>
    /// <remarks>权限：<c>identity.read</c>。调用成功后当前访问令牌立即失效；同一设备后续请求必须重新登录。</remarks>
    /// <returns>包含 <c>loggedOut=true</c> 的统一响应。</returns>
    [Authorize(Policy = PermissionNames.IdentityRead)]
    [HttpPost("logout")]
    public async Task<ActionResult<ApiResponse<object>>> Logout()
    {
        if (!TryGetUser(out var user)) return UnauthorizedResult<object>();
        await _accessTokens.RevokeAsync(user);
        return Ok(ApiResponse<object>.Ok(new { loggedOut = true }));
    }

    /// <summary>交换微信授权码；在微信应用配置完成前该接口返回未实现状态。</summary>
    /// <remarks>权限：匿名。在尚未提供 AppId、密钥与回调地址时返回 501，绝不伪造微信身份。</remarks>
    /// <returns>固定 501 + 业务错误码 50000 的统一响应。</returns>
    [AllowAnonymous]
    [HttpPost("wechat/exchange")]
    public ActionResult<ApiResponse<object>> WeChatExchange() => StatusCode(501, ApiResponse<object>.Fail(ApiErrorCodes.NotImplemented, "请先配置微信 AppId、密钥和回调地址，才能启用授权码换取功能。"));

    /// <summary>将业务层认证结果转换为统一 HTTP 响应。</summary>
    /// <param name="result">业务层返回的认证结果。</param>
    /// <returns>HTTP 状态码与统一响应体。</returns>
    private ActionResult<ApiResponse<AuthSessionViewModel>> ToAuthenticationResponse(AuthenticationResult result)
    {
        if (result.Succeeded && result.Session is not null)
            return Ok(new ApiResponse<AuthSessionViewModel>(0, result.Message, result.Session));
        return StatusCode(result.StatusCode, ApiResponse<AuthSessionViewModel>.Fail(result.Code, result.Message));
    }
}
