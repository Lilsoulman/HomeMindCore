using HomeMind.Business.IServices.Connector;

namespace HomeMind.Business.Services.Connectors.Mcp;

/// <summary>进程级 MCP 会话缓存管理器，默认使用确定性 Mock 会话。</summary>
public sealed class McpClientManager : IMcpClientManager, IAsyncDisposable
{
    private readonly Func<string, IMcpClientSession> _factory;
    private readonly Dictionary<string, IMcpClientSession> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>构造会话管理器。</summary>
    public McpClientManager(Func<string, IMcpClientSession>? factory = null) => _factory = factory ?? (_ => new MockMcpClientSession());

    /// <inheritdoc />
    public async Task<IMcpClientSession> EnsureConnectedAsync(string serverName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(serverName)) throw new ArgumentException("MCP 服务名不能为空。", nameof(serverName));
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_sessions.TryGetValue(serverName, out var existing)) return existing;
            var session = _factory(serverName);
            try
            {
                await session.InitializeAsync(cancellationToken);
                await session.ListToolsAsync(cancellationToken);
                _sessions[serverName] = session;
                return session;
            }
            catch
            {
                await session.DisposeAsync();
                throw;
            }
        }
        finally { _gate.Release(); }
    }

    /// <inheritdoc />
    public async Task<McpToolManifest> RefreshManifestAsync(string serverName, CancellationToken cancellationToken = default)
    {
        var session = await EnsureConnectedAsync(serverName, cancellationToken);
        try
        {
            return await session.ListToolsAsync(cancellationToken);
        }
        catch
        {
            await _gate.WaitAsync(CancellationToken.None);
            try
            {
                if (_sessions.Remove(serverName, out var stale)) await stale.DisposeAsync();
            }
            finally { _gate.Release(); }
            throw;
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        foreach (var session in _sessions.Values) await session.DisposeAsync();
        _sessions.Clear();
        _gate.Dispose();
    }
}
