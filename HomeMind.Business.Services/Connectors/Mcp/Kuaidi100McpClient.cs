using System.Text.Json.Nodes;
using HomeMind.Business.IServices.Connector;

namespace HomeMind.Business.Services.Connectors.Mcp;

/// <summary>快递100 MCP 客户端；仅做安全字段映射，不记录运单号。</summary>
public sealed class Kuaidi100McpClient : IKuaidi100McpClient
{
    private readonly IMcpProcessClient _process;
    /// <summary>构造快递100 MCP 客户端。</summary>
    public Kuaidi100McpClient(IMcpProcessClient process) => _process = process;
    /// <inheritdoc />
    public async Task<Kuaidi100TrackingResult> TrackAsync(string trackingNumber, string? carrier, CancellationToken cancellationToken = default)
    {
        var args = new JsonObject { ["trackingNumber"] = trackingNumber };
        if (!string.IsNullOrWhiteSpace(carrier)) args["carrier"] = carrier;
        try
        {
            var result = await _process.CallToolAsync("kuaidi100_track", args, cancellationToken);
            var array = result?["events"] as JsonArray ?? result?["data"] as JsonArray;
            if (array is null) throw new McpClientException("快递100 MCP 返回结构不兼容。");
            var events = array.OfType<JsonObject>().Select(item => new Kuaidi100TrackingEvent(
                item["status"]?.GetValue<string>() ?? "unknown",
                item["description"]?.GetValue<string>() ?? "物流状态已更新。",
                item["location"]?.GetValue<string>(),
                item["occurredAt"]?.GetValue<DateTime>() ?? DateTime.UtcNow)).ToArray();
            return new Kuaidi100TrackingResult(events);
        }
        catch (McpClientException) { throw; }
        catch (Exception error) { throw new McpClientException("快递100 MCP 返回结构不兼容。", error); }
    }
}

/// <summary>快递100 MCP 确定性 Mock。</summary>
public sealed class MockKuaidi100McpClient : IKuaidi100McpClient
{
    private readonly IReadOnlyList<Kuaidi100TrackingEvent> _events;
    /// <summary>构造 Mock，可注入状态事件。</summary>
    public MockKuaidi100McpClient(IReadOnlyList<Kuaidi100TrackingEvent>? events = null) => _events = events ?? [new("in_transit", "包裹运输中。", "杭州", DateTime.UtcNow)];
    /// <inheritdoc />
    public Task<Kuaidi100TrackingResult> TrackAsync(string trackingNumber, string? carrier, CancellationToken cancellationToken = default) => Task.FromResult(new Kuaidi100TrackingResult(_events));
}
