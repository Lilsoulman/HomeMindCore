using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace HomeMind.Common.Model.ViewModel.Data.Connectors;

/// <summary>发起个人连接器 OAuth 授权会话的请求体。</summary>
public sealed class StartAuthorizationRequest
{
    /// <summary>授权完成后的回调跳转地址，必须命中 Provider 预注册白名单；请求、响应与日志均不含授权 code 或令牌。</summary>
    [Required, StringLength(512), Description("授权完成后的回调跳转地址，必须命中 Provider 预注册白名单。")]
    public string? RedirectUri { get; init; }
}

/// <summary>连接器授权会话脱敏视图；不返回授权 code、访问令牌、刷新令牌或凭据引用。</summary>
/// <param name="SessionId">会话主键。</param>
/// <param name="ProviderCode">连接器提供方编码。</param>
/// <param name="ProviderName">连接器提供方展示名。</param>
/// <param name="Status">会话状态：pending / used / expired / revoked / completed / failed。</param>
/// <param name="ExpiresAt">会话过期时间（UTC），过期后回调拒绝。</param>
/// <param name="AuthorizationUrl">浏览器跳转的授权地址，仅创建响应时返回。</param>
/// <param name="RedirectUri">会话回调跳转地址；回调完成或查询时返回，供客户端回跳。</param>
/// <param name="QrContent">扫码登录类 Provider（如 xhs）的二维码内容或登录链接；仅创建响应时返回，其余为 null。</param>
public sealed record AuthorizationSessionView(
    long SessionId,
    string ProviderCode,
    string ProviderName,
    string Status,
    DateTime ExpiresAt,
    string? AuthorizationUrl = null,
    string? RedirectUri = null,
    string? QrContent = null);
