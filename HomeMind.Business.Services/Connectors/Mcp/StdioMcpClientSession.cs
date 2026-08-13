using System.Text.Json.Nodes;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HomeMind.Business.IServices.Connector;

namespace HomeMind.Business.Services.Connectors.Mcp;

/// <summary>基于现有 stdio 客户端的 transport-neutral MCP 会话适配器。</summary>
public sealed class StdioMcpClientSession : IMcpClientSession
{
    private readonly IMcpProcessClient _client;
    private McpInitializeResult? _initialize;
    private McpToolManifest _manifest = new(Array.Empty<McpToolDefinition>(), "");

    /// <summary>构造 stdio 会话适配器。</summary>
    public StdioMcpClientSession(IMcpProcessClient client) => _client = client;

    /// <inheritdoc />
    public event EventHandler<McpToolsChangedEventArgs>? ToolsChanged;

    /// <inheritdoc />
    public Task<McpInitializeResult> InitializeAsync(CancellationToken cancellationToken = default)
    {
        _initialize ??= new McpInitializeResult("2025-03-26", "stdio-mcp", "1.0");
        return Task.FromResult(_initialize);
    }

    /// <inheritdoc />
    public async Task<McpToolManifest> ListToolsAsync(CancellationToken cancellationToken = default)
    {
        var result = await _client.ListToolsAsync(cancellationToken);
        var tools = new List<McpToolDefinition>();
        if (result?["tools"] is JsonArray array)
        {
            foreach (var item in array.OfType<JsonObject>())
                tools.Add(new McpToolDefinition(item["name"]?.GetValue<string>() ?? "", item["description"]?.GetValue<string>(), item["inputSchema"] as JsonObject));
        }
        var manifest = new McpToolManifest(tools.OrderBy(x => x.Name, StringComparer.Ordinal).ToArray(), CreateManifestHash(tools));
        var changed = !string.Equals(_manifest.Hash, manifest.Hash, StringComparison.Ordinal);
        _manifest = manifest;
        if (changed) ToolsChanged?.Invoke(this, new McpToolsChangedEventArgs(_manifest));
        return _manifest;
    }

    /// <inheritdoc />
    public Task<JsonNode?> CallToolAsync(string toolName, JsonObject? arguments, CancellationToken cancellationToken = default) =>
        _client.CallToolAsync(toolName, arguments, cancellationToken);

    /// <inheritdoc />
    public ValueTask DisposeAsync() => new(_client.StopAsync());

    private static string CreateManifestHash(IEnumerable<McpToolDefinition> tools)
    {
        var canonical = JsonSerializer.Serialize(tools.OrderBy(x => x.Name, StringComparer.Ordinal));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }
}
