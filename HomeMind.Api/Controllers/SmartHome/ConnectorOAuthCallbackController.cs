using System;
using System.Threading;
using System.Threading.Tasks;
using HomeMind.Business.IServices.SmartHome;
using HomeMind.Common.Model.ViewModel.Data.Connectors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HomeMind.Api.Controllers.SmartHome;

/// <summary>
/// 连接器 OAuth Provider 交互端点：Mock 授权页模拟 Provider 同意跳转，服务端回调承载
/// state 校验与 Token 交换。两个端点均匿名访问（由 Provider 或浏览器重定向），
/// 响应不包含授权 code、访问令牌、刷新令牌或凭据引用，失败信息不泄漏校验细节。
/// </summary>
[AllowAnonymous]
[Route("api/v1")]
public sealed class ConnectorOAuthCallbackController : ControllerBase
{
    private readonly IConnectorAuthorizationServices _authorizations;

    /// <summary>构造 OAuth 回调控制器。</summary>
    /// <param name="authorizations">个人连接器授权服务。</param>
    public ConnectorOAuthCallbackController(IConnectorAuthorizationServices authorizations) => _authorizations = authorizations;

    /// <summary>Mock Provider 授权页：模拟用户同意并跳转到服务端回调（携带一次性 code 与 state）。</summary>
    /// <remarks>匿名访问。state 有效性由服务端回调统一校验；仅用于开发与测试环境的确定性链路。</remarks>
    /// <param name="providerCode">连接器提供方编码。</param>
    /// <param name="state">授权会话一次性 state。</param>
    /// <returns>302 到服务端回调地址。</returns>
    [HttpGet("connector-providers/{providerCode}/authorize")]
    public IActionResult AuthorizeMock(string providerCode, [FromQuery] string state)
    {
        var code = Guid.NewGuid().ToString("N");
        return Redirect($"/api/v1/connector-providers/{Uri.EscapeDataString(providerCode)}/callback?state={Uri.EscapeDataString(state ?? string.Empty)}&code={code}");
    }

    /// <summary>服务端 OAuth 回调：校验 state 单次使用与过期，完成 Token 交换并落库凭据引用。</summary>
    /// <remarks>匿名访问（Provider 回跳）。成功 302 到会话回调跳转地址，失败返回 400 且不泄漏校验细节。</remarks>
    /// <param name="providerCode">连接器提供方编码。</param>
    /// <param name="state">回调携带的一次性 state。</param>
    /// <param name="code">回调携带的授权 code，仅服务端消费。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>302 到会话回调跳转地址或 400 错误文本。</returns>
    [HttpGet("connector-providers/{providerCode}/callback")]
    public async Task<IActionResult> Callback(string providerCode, [FromQuery] string state, [FromQuery] string code, CancellationToken cancellationToken)
    {
        var result = await _authorizations.HandleCallbackAsync(providerCode, state, code, cancellationToken);
        if (result.StatusCode is 200 or 302 && result.Data is AuthorizationSessionView view && !string.IsNullOrWhiteSpace(view.RedirectUri))
            return Redirect(view.RedirectUri);
        return StatusCode(400, result.Message);
    }
}
