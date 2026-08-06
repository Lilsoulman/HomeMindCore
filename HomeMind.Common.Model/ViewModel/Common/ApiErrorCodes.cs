namespace HomeMind.Common.Model.ViewModel.Common;

/// <summary>面向客户端的业务错误码集合，含义独立于 HTTP 状态码，控制器必须把 ServiceResult.Code 写入响应体。</summary>
public static class ApiErrorCodes
{
    /// <summary>成功。</summary>
    public const int Success = 0;

    /// <summary>请求无效（HTTP 400/405）。</summary>
    public const int RequestInvalid = 10000;
    /// <summary>请求参数验证失败（HTTP 422）。</summary>
    public const int ValidationFailed = 10001;

    /// <summary>登录凭据无效（HTTP 400）。</summary>
    public const int AuthenticationFailed = 20000;
    /// <summary>访问令牌缺失、无效、过期或已被吊销（HTTP 401）。</summary>
    public const int AccessTokenInvalid = 20001;
    /// <summary>刷新令牌无效、过期或已被吊销（HTTP 401）。</summary>
    public const int RefreshTokenInvalid = 20002;
    /// <summary>已认证调用方缺少权限（HTTP 403）。</summary>
    public const int AccessDenied = 20003;

    /// <summary>资源对调用方不可用或不存在（HTTP 404）。</summary>
    public const int ResourceNotFound = 30000;
    /// <summary>请求与当前资源状态冲突（HTTP 409）。</summary>
    public const int Conflict = 40000;

    /// <summary>端点被刻意留为未实现（HTTP 501）。</summary>
    public const int NotImplemented = 50000;
    /// <summary>所需依赖不可用（HTTP 503）。</summary>
    public const int DependencyUnavailable = 50001;
    /// <summary>上游依赖失败或超时（HTTP 502/504）。</summary>
    public const int DependencyFailed = 50002;
    /// <summary>未预期的服务器错误（HTTP 500）。</summary>
    public const int InternalError = 90000;

    /// <summary>将 HTTP 状态码映射为应用层业务错误码。</summary>
    /// <param name="statusCode">HTTP 状态码。</param>
    /// <returns>对应的应用层业务错误码，未匹配时返回 <see cref="InternalError"/>。</returns>
    public static int FromHttpStatus(int statusCode) => statusCode switch
    {
        400 or 405 => RequestInvalid,
        401 => AccessTokenInvalid,
        403 => AccessDenied,
        404 => ResourceNotFound,
        409 => Conflict,
        422 => ValidationFailed,
        501 => NotImplemented,
        502 or 504 => DependencyFailed,
        503 => DependencyUnavailable,
        _ => InternalError
    };
}
