using System.Threading.Tasks;
using HomeMind.Api.Services;
using HomeMind.Business.IServices.Base;
using HomeMind.Common.Model.ViewModel.Data.Base;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HomeMind.Api.Controllers.Base;

/// <summary>身份认证模块，负责账户注册、登录、令牌续期和当前用户信息查询。</summary>
[Route("api/v1/auth")]
public sealed class AuthController : ApiControllerBase
{
    private readonly IBaseUserServices _baseUserServices;
    private readonly AccessTokenValidator _accessTokens;

    public AuthController(IBaseUserServices baseUserServices, AccessTokenValidator accessTokens)
    {
        _baseUserServices = baseUserServices;
        _accessTokens = accessTokens;
    }

    /// <summary>使用手机号和密码注册个人账户，并返回访问令牌与刷新令牌。</summary>
    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<ActionResult<ApiResponse<AuthSessionViewModel>>> Register(RegisterRequest request)
        => ToAuthenticationResponse(await _baseUserServices.RegisterAsync(request, HttpContext.RequestAborted));

    /// <summary>使用手机号和密码登录，创建或更新当前设备会话。</summary>
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<AuthSessionViewModel>>> Login(LoginRequest request)
        => ToAuthenticationResponse(await _baseUserServices.LoginAsync(request, HttpContext.RequestAborted));

    /// <summary>使用有效的刷新令牌换取新的访问令牌和刷新令牌。</summary>
    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<ActionResult<ApiResponse<AuthSessionViewModel>>> Refresh(RefreshRequest request)
        => ToAuthenticationResponse(await _baseUserServices.RefreshAsync(request, HttpContext.RequestAborted));

    /// <summary>获取当前访问令牌对应用户的基础资料。</summary>
    [Authorize(Policy = PermissionNames.IdentityRead)]
    [HttpGet("me")]
    public async Task<ActionResult<ApiResponse<BaseUserViewModel>>> Me()
    {
        if (!TryGetUser(out var user)) return UnauthorizedResult<BaseUserViewModel>();
        var result = await _baseUserServices.GetCurrentUserAsync(user.UserId, HttpContext.RequestAborted);
        return result is null ? NotFoundResult<BaseUserViewModel>() : Ok(ApiResponse<BaseUserViewModel>.Ok(result));
    }

    /// <summary>注销当前设备会话，并立即撤销当前访问令牌和该设备的刷新令牌。</summary>
    [Authorize(Policy = PermissionNames.IdentityRead)]
    [HttpPost("logout")]
    public async Task<ActionResult<ApiResponse<object>>> Logout()
    {
        if (!TryGetUser(out var user)) return UnauthorizedResult<object>();
        await _accessTokens.RevokeAsync(user);
        return Ok(ApiResponse<object>.Ok(new { loggedOut = true }));
    }

    /// <summary>交换微信授权码；在微信应用配置完成前该接口返回未实现状态。</summary>
    [AllowAnonymous]
    [HttpPost("wechat/exchange")]
    public ActionResult<ApiResponse<object>> WeChatExchange() => StatusCode(501, ApiResponse<object>.Fail(501, "请先配置微信 AppId、密钥和回调地址，才能启用授权码换取功能。"));

    private ActionResult<ApiResponse<AuthSessionViewModel>> ToAuthenticationResponse(AuthenticationResult result)
    {
        if (result.Succeeded && result.Session is not null)
            return Ok(new ApiResponse<AuthSessionViewModel>(0, result.Message, result.Session));
        return StatusCode(result.StatusCode, ApiResponse<AuthSessionViewModel>.Fail(result.StatusCode, result.Message));
    }
}
