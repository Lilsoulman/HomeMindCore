namespace HomeMind.Business.IServices.Connector;

/// <summary>
/// 小红书（xhs）MCP 客户端契约：经本地 stdio MCP（xhs-mcp）调用小红书能力——登录状态、
/// 笔记搜索（只读 L1）、笔记详情（只读 L1）与图文/视频笔记发布（对外动作 L2）。
/// 登录为扫码登录，凭据由 MCP 进程本机管理；实现不得返回 cookie、登录态明文或 MCP 内部路径。
/// </summary>
public interface IXhsMcpClient
{
    /// <summary>查询当前小红书登录状态。</summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>登录状态摘要。</returns>
    /// <exception cref="McpClientException">本地 MCP 进程不可用或调用失败时抛出。</exception>
    Task<XhsAuthStatus> GetAuthStatusAsync(CancellationToken cancellationToken = default);

    Task<XhsAuthStatus> GetAuthStatusAsync(string credentialRef, CancellationToken cancellationToken = default) =>
        GetAuthStatusAsync(cancellationToken);

    /// <summary>触发扫码登录：请求 MCP 生成登录二维码；登录结果经 <see cref="GetAuthStatusAsync"/> 轮询。</summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>登录提示信息（二维码展示方式随 MCP 实现而定）。</returns>
    /// <exception cref="McpClientException">本地 MCP 进程不可用或调用失败时抛出。</exception>
    Task<XhsLoginHint> TriggerLoginAsync(CancellationToken cancellationToken = default);

    Task<XhsLoginHint> TriggerLoginAsync(string credentialRef, CancellationToken cancellationToken = default) =>
        TriggerLoginAsync(cancellationToken);

    /// <summary>登出小红书账号并清理本机登录会话；撤销授权时调用，失败不阻塞授权状态流转。</summary>
    /// <param name="cancellationToken">取消令牌。</param>
    Task LogoutAsync(CancellationToken cancellationToken = default);

    Task LogoutAsync(string credentialRef, CancellationToken cancellationToken = default) =>
        LogoutAsync(cancellationToken);

    /// <summary>按关键词搜索小红书笔记（只读）。</summary>
    /// <param name="query">搜索关键词。</param>
    /// <param name="limit">返回条数上限（1-50）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>笔记摘要列表。</returns>
    /// <exception cref="McpClientException">本地 MCP 进程不可用或调用失败时抛出。</exception>
    Task<XhsSearchResult> SearchNotesAsync(string query, int limit, CancellationToken cancellationToken = default);

    Task<XhsSearchResult> SearchNotesAsync(string query, int limit, string credentialRef, CancellationToken cancellationToken = default) =>
        SearchNotesAsync(query, limit, cancellationToken);

    /// <summary>获取笔记详情（只读）。</summary>
    /// <param name="url">笔记链接。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>笔记详情。</returns>
    /// <exception cref="McpClientException">本地 MCP 进程不可用或调用失败时抛出。</exception>
    Task<XhsNoteDetail> GetNoteDetailAsync(string url, CancellationToken cancellationToken = default);

    Task<XhsNoteDetail> GetNoteDetailAsync(string url, string credentialRef, CancellationToken cancellationToken = default) =>
        GetNoteDetailAsync(url, cancellationToken);

    /// <summary>发布图文或视频笔记（对外动作，调用方负责 L2 确认与审计）。</summary>
    /// <param name="input">发布参数（标题/正文/媒体/标签）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>发布结果。</returns>
    /// <exception cref="McpClientException">本地 MCP 进程不可用或调用失败时抛出。</exception>
    Task<XhsPublishResult> PublishAsync(XhsPublishInput input, CancellationToken cancellationToken = default);

    Task<XhsPublishResult> PublishAsync(XhsPublishInput input, string credentialRef, CancellationToken cancellationToken = default) =>
        PublishAsync(input, cancellationToken);
}
