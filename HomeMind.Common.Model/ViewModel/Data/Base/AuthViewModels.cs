namespace HomeMind.Common.Model.ViewModel.Data.Base;

/// <summary>注册个人账户的请求参数。</summary>
public sealed record RegisterRequest(string Phone, string Password, string? DisplayName, string? InstallationId, string? Platform);

/// <summary>使用手机号和密码登录的请求参数。</summary>
public sealed record LoginRequest(string Phone, string Password, string? InstallationId, string? Platform);

/// <summary>刷新访问令牌的请求参数。</summary>
public sealed record RefreshRequest(string RefreshToken);

/// <summary>登录、注册或刷新令牌后返回的会话信息。</summary>
public sealed record AuthSessionViewModel(string AccessToken, string RefreshToken, long UserId, long TenantId);

/// <summary>当前登录用户的基础资料。</summary>
public sealed record BaseUserViewModel(long Id, string DisplayName, string? AvatarUrl, string Status, string Timezone, string Locale, DateTime CreatedAt);

/// <summary>业务层返回的认证处理结果。</summary>
public sealed record AuthenticationResult(int StatusCode, string Message, AuthSessionViewModel? Session)
{
    public bool Succeeded => StatusCode == 200;
}
