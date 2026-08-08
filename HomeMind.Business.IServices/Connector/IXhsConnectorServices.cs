using HomeMind.Common.Model.ViewModel.Common;

namespace HomeMind.Business.IServices.Connector;

/// <summary>
/// 小红书（xhs）个人级 Connector 工具执行服务契约：执行前校验连接器归属
/// （当前租户 + personal 作用域 + 本人 owner + 已授权），未授权统一返回 404；
/// 搜索/详情为只读 L1 操作，响应不含登录态、凭据引用或 MCP 内部路径。
/// </summary>
public interface IXhsConnectorServices
{
    /// <summary>按关键词搜索小红书笔记（只读 L1）。</summary>
    /// <param name="userId">当前用户主键。</param>
    /// <param name="tenantId">当前租户标识，来自 JWT。</param>
    /// <param name="query">搜索关键词，必填。</param>
    /// <param name="limit">返回条数上限（1-50），默认 10。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>成功返回笔记摘要列表；连接器未授权返回 404，参数非法返回 422。</returns>
    Task<ServiceResult> SearchNotesAsync(long userId, long tenantId, string query, int limit, CancellationToken cancellationToken = default);

    /// <summary>获取小红书笔记详情（只读 L1）。</summary>
    /// <param name="userId">当前用户主键。</param>
    /// <param name="tenantId">当前租户标识，来自 JWT。</param>
    /// <param name="url">笔记链接，必填。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>成功返回笔记详情；连接器未授权返回 404，参数非法返回 422。</returns>
    Task<ServiceResult> GetNoteDetailAsync(long userId, long tenantId, string url, CancellationToken cancellationToken = default);

    /// <summary>查询本人小红书连接器的登录状态。</summary>
    /// <param name="userId">当前用户主键。</param>
    /// <param name="tenantId">当前租户标识，来自 JWT。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>成功返回登录状态视图；连接器未授权返回 404。</returns>
    Task<ServiceResult> GetAuthStatusAsync(long userId, long tenantId, CancellationToken cancellationToken = default);
}
