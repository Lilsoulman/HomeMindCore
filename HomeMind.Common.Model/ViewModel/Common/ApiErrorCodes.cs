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
    /// <summary>业务前置条件未满足（HTTP 422），例如 AI 配置已禁用。</summary>
    public const int PreconditionFailed = 42200;

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

    /// <summary>V2.4 B19：家庭 owner 转让目标处于 suspended/away（HTTP 422）。</summary>
    public const int OwnerTransferInvalidReceiver = 42201;
    /// <summary>V2.4 B19：家庭成员角色变更直接置 owner 已被拒（HTTP 422），请使用 owner-transfer。</summary>
    public const int TenantRoleOwnerDirectForbidden = 42202;
    /// <summary>V2.4 B19：家庭租户级乐观锁冲突（HTTP 409）。</summary>
    public const int TenantOptimisticLockConflict = 40901;
    /// <summary>V2.4 B19：家庭成员邀请的受邀标识在当前家庭已存在未结邀请（HTTP 409）。</summary>
    public const int TenantInvitationConflict = 40902;
    /// <summary>V2.4 B19：家庭成员邀请的受邀标识在当前家庭不匹配已验证账户（HTTP 404）。</summary>
    public const int TenantInvitationIdentityNotMatched = 30001;
    /// <summary>V2.4 B19：Web 导航偏好提交了未发布 route_key（HTTP 422）。</summary>
    public const int WebNavigationRouteKeyNotPublished = 42203;

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
