using System.Text.Json.Nodes;

namespace HomeMind.Business.IServices.Connector;

/// <summary>MCP 服务初始化结果，记录协议版本与服务端能力摘要。</summary>
public sealed record McpInitializeResult(string ProtocolVersion, string ServerName, string ServerVersion);

/// <summary>MCP 工具定义及其输入约束。</summary>
public sealed record McpToolDefinition(string Name, string? Description, JsonObject? InputSchema);

/// <summary>MCP 工具清单及稳定哈希，用于检测服务端工具撤销或新增。</summary>
public sealed record McpToolManifest(IReadOnlyList<McpToolDefinition> Tools, string Hash);

/// <summary>MCP 工具清单变更通知。</summary>
public sealed class McpToolsChangedEventArgs(McpToolManifest manifest) : EventArgs
{
    /// <summary>更新后的工具清单。</summary>
    public McpToolManifest Manifest { get; } = manifest;
}

/// <summary>与传输协议无关的 MCP 会话契约。</summary>
public interface IMcpClientSession : IAsyncDisposable
{
    /// <summary>完成 MCP 初始化握手。</summary>
    Task<McpInitializeResult> InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>读取服务端工具清单。</summary>
    Task<McpToolManifest> ListToolsAsync(CancellationToken cancellationToken = default);

    /// <summary>调用指定工具；工具不存在或调用失败时抛出 MCP 异常。</summary>
    Task<JsonNode?> CallToolAsync(string toolName, JsonObject? arguments, CancellationToken cancellationToken = default);

    /// <summary>工具清单发生变化时触发。</summary>
    event EventHandler<McpToolsChangedEventArgs>? ToolsChanged;
}

/// <summary>MCP 会话管理器，按服务名缓存会话并在失效后重新建立。</summary>
public interface IMcpClientManager
{
    /// <summary>获取或创建指定 MCP 服务会话。</summary>
    Task<IMcpClientSession> EnsureConnectedAsync(string serverName, CancellationToken cancellationToken = default);

    /// <summary>刷新指定服务的工具清单。</summary>
    Task<McpToolManifest> RefreshManifestAsync(string serverName, CancellationToken cancellationToken = default);
}
