using HomeMind.Common.Model.ViewModel.Common;

namespace HomeMind.Common.Model.ViewModel.Data.Base;

/// <summary>注册个人账户的请求参数。</summary>
/// <param name="Phone">手机号，E.164 国际格式。</param>
/// <param name="Password">明文密码，由服务端按 PBKDF2 哈希后存储。</param>
/// <param name="DisplayName">可选的显示名。</param>
/// <param name="InstallationId">客户端安装标识，用于多设备令牌家族。</param>
/// <param name="Platform">客户端平台，如"ios""android""h5"。</param>
public sealed record RegisterRequest(string Phone, string Password, string? DisplayName, string? InstallationId, string? Platform);

/// <summary>使用手机号和密码登录的请求参数。</summary>
/// <param name="Phone">手机号，E.164 国际格式。</param>
/// <param name="Password">明文密码。</param>
/// <param name="InstallationId">客户端安装标识，可为空。</param>
/// <param name="Platform">客户端平台，可为空。</param>
public sealed record LoginRequest(string Phone, string Password, string? InstallationId, string? Platform);

/// <summary>刷新访问令牌的请求参数。</summary>
/// <param name="RefreshToken">刷新令牌明文，服务端仅保存哈希。</param>
public sealed record RefreshRequest(string RefreshToken);

/// <summary>登录、注册或刷新令牌后返回的会话信息。</summary>
/// <param name="AccessToken">短期访问令牌 JWT。</param>
/// <param name="RefreshToken">刷新令牌明文，客户端需安全保存。</param>
/// <param name="UserId">用户主键。</param>
/// <param name="TenantId">租户主键，由服务端分配。</param>
public sealed record AuthSessionViewModel(string AccessToken, string RefreshToken, long UserId, long TenantId);

/// <summary>当前登录用户的基础资料。</summary>
/// <param name="Id">用户主键。</param>
/// <param name="DisplayName">显示名。</param>
/// <param name="AvatarUrl">头像 URL，可为空。</param>
/// <param name="Status">账户状态。</param>
/// <param name="Timezone">默认时区，IANA 标识。</param>
/// <param name="Locale">语言标签，BCP 47。</param>
/// <param name="CreatedAt">账户创建时间（UTC）。</param>
/// <param name="Role">当前租户中的角色：owner/admin/member/viewer。</param>
public sealed record BaseUserViewModel(long Id, string DisplayName, string? AvatarUrl, string Status, string Timezone, string Locale, DateTime CreatedAt, string Role = "");

/// <summary>业务层返回的认证处理结果。</summary>
/// <param name="StatusCode">HTTP 状态码。</param>
/// <param name="Message">人类可读的结果消息。</param>
/// <param name="Session">认证成功时返回的会话信息，失败时为 null。</param>
/// <param name="ErrorCode">应用层业务错误码，可缺省。</param>
public sealed record AuthenticationResult(int StatusCode, string Message, AuthSessionViewModel? Session, int? ErrorCode = null)
{
    /// <summary>认证是否成功（HTTP 200）。</summary>
    public bool Succeeded => StatusCode == 200;
    /// <summary>对外的业务错误码。</summary>
    public int Code => Succeeded ? ApiErrorCodes.Success : ErrorCode ?? ApiErrorCodes.FromHttpStatus(StatusCode);
}
