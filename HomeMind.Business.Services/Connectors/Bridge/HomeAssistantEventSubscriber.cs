using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using HomeMind.Business.IServices.Connector;
using HomeMind.Business.IServices.SmartHome;
using HomeMind.Business.Services.Connectors.Adapters;
using HomeMind.Common.Model.Entities.SmartHome;

namespace HomeMind.Business.Services.Connectors.Bridge;

/// <summary>Home Assistant WebSocket 状态订阅器，负责鉴权、白名单过滤、冷却去重与断线边界。</summary>
public sealed class HomeAssistantEventSubscriber : IHomeAssistantEventSubscriber
{
    private readonly IConnectorSecretResolver _secrets;
    private readonly DeviceSyncService _sync;
    private readonly HomeAssistantMcpOptions _options;
    private readonly Dictionary<string, EventFingerprint> _recentEvents = new(StringComparer.Ordinal);

    /// <summary>构造实时状态订阅器。</summary>
    public HomeAssistantEventSubscriber(IConnectorSecretResolver secrets, DeviceSyncService sync, HomeAssistantMcpOptions options)
    {
        _secrets = secrets;
        _sync = sync;
        _options = options;
    }

    /// <inheritdoc />
    public async Task SubscribeAsync(WorkspaceConnector connector, CancellationToken cancellationToken)
    {
        var secret = await _secrets.ResolveAsync(new ConnectorReference(connector.Id, connector.TenantId, connector.CredentialRef ?? string.Empty), cancellationToken);
        if (!secret.Succeeded || string.IsNullOrWhiteSpace(secret.SecretJson)) throw new ConnectorAdapterException(secret.ErrorCode ?? "secret_vault_unavailable", "无法读取 Home Assistant 连接凭据。");
        var connection = ParseConnection(secret.SecretJson);
        using var socket = new ClientWebSocket();
        await socket.ConnectAsync(ToWebSocketUri(connection.BaseUrl), cancellationToken);
        await ExpectTypeAsync(socket, "auth_required", cancellationToken);
        await SendAsync(socket, JsonSerializer.Serialize(new { type = "auth", access_token = connection.AccessToken }), cancellationToken);
        await ExpectTypeAsync(socket, "auth_ok", cancellationToken);
        await SendAsync(socket, "{\"id\":1,\"type\":\"subscribe_events\",\"event_type\":\"state_changed\"}", cancellationToken);
        await ExpectResultAsync(socket, 1, cancellationToken);

        while (!cancellationToken.IsCancellationRequested && socket.State == WebSocketState.Open)
        {
            using var document = JsonDocument.Parse(await ReceiveAsync(socket, cancellationToken));
            await HandleEventAsync(connector, document.RootElement, cancellationToken);
        }
    }

    private async Task HandleEventAsync(WorkspaceConnector connector, JsonElement message, CancellationToken cancellationToken)
    {
        if (!message.TryGetProperty("type", out var type) || type.GetString() != "event" || !message.TryGetProperty("event", out var eventData)) return;
        if (!eventData.TryGetProperty("data", out var data) || !data.TryGetProperty("entity_id", out var entity) || string.IsNullOrWhiteSpace(entity.GetString()) || !data.TryGetProperty("new_state", out var state)) return;
        var externalId = entity.GetString()!;
        if (!IsAllowed(externalId)) return;
        var stateJson = JsonSerializer.Serialize(state);
        var now = DateTime.UtcNow;
        if (_recentEvents.TryGetValue(externalId, out var recent) && (recent.State == stateJson || now - recent.ReceivedAt < TimeSpan.FromSeconds(Math.Max(0, _options.EventCooldownSeconds)))) return;
        _recentEvents[externalId] = new EventFingerprint(stateJson, now);
        if (!HomeAssistantAdapter.TryMapEntity(state, out var normalized)) return;
        await _sync.ApplyStateChangedAsync(connector.TenantId, connector.Id, externalId, normalized.StateJson, normalized.SampledAt, cancellationToken);
    }

    private bool IsAllowed(string externalId)
    {
        if (_options.IgnoreEntities.Contains(externalId, StringComparer.OrdinalIgnoreCase)) return false;
        if (_options.WatchEntities.Length > 0) return _options.WatchEntities.Contains(externalId, StringComparer.OrdinalIgnoreCase);
        var separator = externalId.IndexOf('.');
        return separator > 0 && _options.WatchDomains.Contains(externalId[..separator], StringComparer.OrdinalIgnoreCase);
    }

    private static async Task ExpectTypeAsync(ClientWebSocket socket, string expected, CancellationToken cancellationToken)
    {
        using var document = JsonDocument.Parse(await ReceiveAsync(socket, cancellationToken));
        if (!document.RootElement.TryGetProperty("type", out var type) || type.GetString() != expected) throw new ConnectorAdapterException("authentication_failed", "Home Assistant WebSocket 鉴权失败。");
    }

    private static async Task ExpectResultAsync(ClientWebSocket socket, int id, CancellationToken cancellationToken)
    {
        using var document = JsonDocument.Parse(await ReceiveAsync(socket, cancellationToken));
        if (!document.RootElement.TryGetProperty("id", out var responseId) || responseId.GetInt32() != id || !document.RootElement.TryGetProperty("success", out var success) || !success.GetBoolean()) throw new ConnectorAdapterException("remote_error", "Home Assistant 未能建立状态订阅。");
    }

    private static async Task SendAsync(ClientWebSocket socket, string message, CancellationToken cancellationToken) => await socket.SendAsync(Encoding.UTF8.GetBytes(message), WebSocketMessageType.Text, true, cancellationToken);
    private static async Task<string> ReceiveAsync(ClientWebSocket socket, CancellationToken cancellationToken)
    {
        var buffer = new ArraySegment<byte>(new byte[16 * 1024]);
        using var stream = new MemoryStream();
        WebSocketReceiveResult result;
        do { result = await socket.ReceiveAsync(buffer, cancellationToken); if (result.MessageType == WebSocketMessageType.Close) throw new ConnectorAdapterException("connection_closed", "Home Assistant 实时连接已关闭。"); stream.Write(buffer.Array!, buffer.Offset, result.Count); } while (!result.EndOfMessage);
        return Encoding.UTF8.GetString(stream.ToArray());
    }
    private static Uri ToWebSocketUri(string baseUrl)
    {
        var uri = new Uri(baseUrl.TrimEnd('/') + "/api/websocket");
        var builder = new UriBuilder(uri) { Scheme = uri.Scheme == "https" ? "wss" : "ws", Port = uri.IsDefaultPort ? -1 : uri.Port };
        return builder.Uri;
    }
    private static Connection ParseConnection(string secretJson)
    {
        using var document = JsonDocument.Parse(secretJson);
        var root = document.RootElement;
        if (!root.TryGetProperty("baseUrl", out var baseUrl) || !root.TryGetProperty("accessToken", out var token) || string.IsNullOrWhiteSpace(baseUrl.GetString()) || string.IsNullOrWhiteSpace(token.GetString())) throw new ConnectorAdapterException("invalid_secret", "Home Assistant 凭据格式无效。");
        return new Connection(baseUrl.GetString()!, token.GetString()!);
    }
    private sealed record Connection(string BaseUrl, string AccessToken);
    private sealed record EventFingerprint(string State, DateTime ReceivedAt);
}
