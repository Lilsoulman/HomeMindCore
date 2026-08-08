using HomeMind.Business.IServices.Connector;

namespace HomeMind.Business.Services.Connectors.Mcp;

/// <summary>
/// 小红书（xhs）MCP 的确定性 Mock 实现：不启动本地 MCP 进程、不访问小红书，按输入生成
/// 固定结构数据（搜索/详情/发布），供单元测试与无 node/npx 环境回退使用。
/// 发布结果由构造参数控制（默认成功），用于验证确认与审计链路。
/// </summary>
public sealed class MockXhsMcpClient : IXhsMcpClient
{
    private readonly bool _loggedIn;
    private readonly bool _publishFails;

    /// <summary>构造确定性 Mock 客户端。</summary>
    /// <param name="loggedIn">模拟的登录状态，默认未登录。</param>
    /// <param name="publishFails">模拟发布失败，默认发布成功。</param>
    public MockXhsMcpClient(bool loggedIn = false, bool publishFails = false)
    {
        _loggedIn = loggedIn;
        _publishFails = publishFails;
    }

    /// <inheritdoc />
    public Task<XhsAuthStatus> GetAuthStatusAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_loggedIn
            ? new XhsAuthStatus(true, "小红书已登录（Mock）。")
            : new XhsAuthStatus(false, "小红书尚未登录（Mock）。"));

    /// <inheritdoc />
    public Task LogoutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc />
    public Task<XhsLoginHint> TriggerLoginAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new XhsLoginHint("请在小红书 App 中扫描二维码完成登录（Mock）。", "mock-qr://xhs-login"));

    /// <inheritdoc />
    public Task<XhsSearchResult> SearchNotesAsync(string query, int limit, CancellationToken cancellationToken = default)
    {
        var notes = Enumerable.Range(1, Math.Min(limit, 3)).Select(index => new XhsNoteSummary(
            $"mock-note-{index}",
            $"{query}·示例笔记{index}",
            $"https://mock.example.com/cover/{index}.jpg",
            $"示例作者{index}",
            $"https://mock.example.com/note/{index}")).ToArray();
        return Task.FromResult(new XhsSearchResult(notes));
    }

    /// <inheritdoc />
    public Task<XhsNoteDetail> GetNoteDetailAsync(string url, CancellationToken cancellationToken = default) =>
        Task.FromResult(new XhsNoteDetail("mock-note-1", "示例笔记详情", "这是 Mock 客户端返回的笔记正文。", ["https://mock.example.com/cover/1.jpg"], url));

    /// <inheritdoc />
    public Task<XhsPublishResult> PublishAsync(XhsPublishInput input, CancellationToken cancellationToken = default) =>
        Task.FromResult(_publishFails
            ? new XhsPublishResult(false, "", "Mock 发布失败。")
            : new XhsPublishResult(true, "mock-published-note-1", "小红书笔记发布成功（Mock）。"));
}
