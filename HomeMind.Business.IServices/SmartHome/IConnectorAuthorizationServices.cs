using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.Connectors;

namespace HomeMind.Business.IServices.SmartHome;

/// <summary>
/// 连接器个人授权服务契约：承载 OAuth 授权会话的发起、服务端回调、状态查询与撤销。
/// 所有敏感处理（state 哈希、PKCE 校验器、Token 交换、凭据引用）均在服务端完成，
/// 任何返回与日志不得出现授权 code、访问令牌、刷新令牌或明文凭据。
/// </summary>
public interface IConnectorAuthorizationServices
{
    /// <summary>发起一次个人连接器 OAuth 授权会话并返回浏览器跳转地址。</summary>
    /// <param name="userId">当前用户主键（会话发起人）。</param>
    /// <param name="tenantId">当前租户标识，来自 JWT，禁止客户端覆盖。</param>
    /// <param name="providerCode">连接器提供方编码。</param>
    /// <param name="request">授权请求体，含回调跳转白名单地址。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>成功返回会话脱敏视图（含授权地址）；提供方不存在返回 404，Vault 不可用返回 503。</returns>
    Task<ServiceResult> StartAuthorizationAsync(long userId, long tenantId, string providerCode, StartAuthorizationRequest request, CancellationToken cancellationToken = default);

    /// <summary>处理 Provider 的服务端回调：校验一次性 state、会话未过期且未使用，模拟 Token 交换并写入凭据引用。</summary>
    /// <param name="providerCode">连接器提供方编码。</param>
    /// <param name="state">回调携带的一次性 state，服务端按哈希匹配会话。</param>
    /// <param name="code">回调携带的授权 code，仅服务端消费，不落库。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>成功返回 302 与会话回跳地址；state 无效、会话已使用或已过期返回 400/409，不泄漏校验细节。</returns>
    Task<ServiceResult> HandleCallbackAsync(string providerCode, string state, string code, CancellationToken cancellationToken = default);

    /// <summary>查询本人授权会话的脱敏状态。</summary>
    /// <param name="userId">当前用户主键。</param>
    /// <param name="tenantId">当前租户标识。</param>
    /// <param name="sessionId">会话主键。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>成功返回脱敏会话视图；非本人或跨租户统一返回 404。</returns>
    Task<ServiceResult> GetAuthorizationStatusAsync(long userId, long tenantId, long sessionId, CancellationToken cancellationToken = default);

    /// <summary>撤销本人个人连接器授权：撤销实例凭据可用性、终止会话并写审计；重复撤销幂等返回既有结果。</summary>
    /// <param name="userId">当前用户主键。</param>
    /// <param name="tenantId">当前租户标识。</param>
    /// <param name="sessionId">会话主键。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>成功返回撤销后会话视图；非本人或跨租户统一返回 404。</returns>
    Task<ServiceResult> RevokeAuthorizationAsync(long userId, long tenantId, long sessionId, CancellationToken cancellationToken = default);
}
