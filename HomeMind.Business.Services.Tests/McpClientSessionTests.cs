using System.Text.Json.Nodes;
using HomeMind.Business.IServices.Connector;
using HomeMind.Business.Services.Connectors.Mcp;
using Xunit;

namespace HomeMind.Business.Services.Tests;

/// <summary>MCP 会话协议、缓存和失效行为测试。</summary>
public class McpClientSessionTests
{
    /// <summary>同一服务的并发连接只创建一个已初始化会话。</summary>
    [Fact]
    public async Task Manager_Concurrent_Ensure_Uses_One_Cached_Session()
    {
        var created = 0;
        await using var manager = new McpClientManager(_ =>
        {
            Interlocked.Increment(ref created);
            return new FakeSession();
        });

        var sessions = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => manager.EnsureConnectedAsync("home-assistant")));

        Assert.Equal(1, created);
        Assert.Single(sessions.Distinct());
    }

    /// <summary>清单刷新失败会清除失效会话，下一次连接创建新会话。</summary>
    [Fact]
    public async Task Manager_Refresh_Failure_Invalidates_Cached_Session()
    {
        var sessions = new Queue<FakeSession>([new FakeSession { ThrowOnList = true }, new FakeSession(), new FakeSession()]);
        await using var manager = new McpClientManager(_ => sessions.Dequeue());

        await Assert.ThrowsAsync<McpClientException>(() => manager.EnsureConnectedAsync("home-assistant"));
        var healthy = await manager.EnsureConnectedAsync("home-assistant");
        ((FakeSession)healthy).ThrowOnList = true;
        await Assert.ThrowsAsync<McpClientException>(() => manager.RefreshManifestAsync("home-assistant"));

        var reconnected = await manager.EnsureConnectedAsync("home-assistant");
        Assert.NotSame(healthy, reconnected);
    }

    /// <summary>stdio 会话通过协议级 tools/list 获取清单，并仅在内容变化时发送事件。</summary>
    [Fact]
    public async Task Stdio_Session_Lists_Tools_And_Only_Notifies_On_Change()
    {
        var process = new FakeProcessClient(new JsonObject
        {
            ["tools"] = new JsonArray { new JsonObject { ["name"] = "get_states", ["description"] = "读取状态", ["inputSchema"] = new JsonObject() } }
        });
        await using var session = new StdioMcpClientSession(process);
        var notifications = 0;
        session.ToolsChanged += (_, _) => notifications++;

        var first = await session.ListToolsAsync();
        var second = await session.ListToolsAsync();

        Assert.Equal(2, process.ListCalls);
        Assert.Equal(first.Hash, second.Hash);
        Assert.Equal(1, notifications);
    }

    private sealed class FakeSession : IMcpClientSession
    {
        public bool ThrowOnList { get; set; }
        public event EventHandler<McpToolsChangedEventArgs>? ToolsChanged;
        public Task<McpInitializeResult> InitializeAsync(CancellationToken cancellationToken = default) => Task.FromResult(new McpInitializeResult("2025-03-26", "fake", "1"));
        public Task<McpToolManifest> ListToolsAsync(CancellationToken cancellationToken = default)
        {
            if (ThrowOnList) throw new McpClientException("超时");
            return Task.FromResult(new McpToolManifest([], "hash"));
        }
        public Task<JsonNode?> CallToolAsync(string toolName, JsonObject? arguments, CancellationToken cancellationToken = default) => Task.FromResult<JsonNode?>(null);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeProcessClient(JsonObject manifest) : IMcpProcessClient
    {
        public int ListCalls { get; private set; }
        public Task<JsonObject?> ListToolsAsync(CancellationToken cancellationToken = default)
        {
            ListCalls++;
            return Task.FromResult<JsonObject?>(manifest);
        }
        public Task<JsonNode?> CallToolAsync(string toolName, JsonObject? arguments, CancellationToken cancellationToken = default) => Task.FromResult<JsonNode?>(null);
        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
