using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using HomeMind.Business.IServices.Connector;

namespace HomeMind.Business.Services.Connectors.Mcp;

/// <summary>确定性 MCP Mock，会话测试与无外部服务环境的回退实现。</summary>
public sealed class MockMcpClientSession : IMcpClientSession
{
    private McpToolManifest _manifest = BuildManifest(Array.Empty<McpToolDefinition>());

    /// <inheritdoc />
    public event EventHandler<McpToolsChangedEventArgs>? ToolsChanged;

    /// <inheritdoc />
    public Task<McpInitializeResult> InitializeAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new McpInitializeResult("2025-03-26", "mock-mcp", "1.0"));

    /// <inheritdoc />
    public Task<McpToolManifest> ListToolsAsync(CancellationToken cancellationToken = default) => Task.FromResult(_manifest);

    /// <inheritdoc />
    public Task<JsonNode?> CallToolAsync(string toolName, JsonObject? arguments, CancellationToken cancellationToken = default)
    {
        if (!_manifest.Tools.Any(x => x.Name == toolName))
            throw new McpClientException($"MCP 工具不可用：{toolName}");
        return Task.FromResult<JsonNode?>(new JsonObject { ["ok"] = true, ["tool"] = toolName });
    }

    /// <summary>替换工具清单并通知订阅者。</summary>
    public void SetTools(IEnumerable<McpToolDefinition> tools)
    {
        _manifest = BuildManifest(tools);
        ToolsChanged?.Invoke(this, new McpToolsChangedEventArgs(_manifest));
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static McpToolManifest BuildManifest(IEnumerable<McpToolDefinition> tools)
    {
        var list = tools.OrderBy(x => x.Name, StringComparer.Ordinal).ToArray();
        var json = JsonSerializer.Serialize(list);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
        return new McpToolManifest(list, hash);
    }
}
