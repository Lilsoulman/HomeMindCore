using System.Text.Json;
using System.Text.Json.Nodes;
using HomeMind.Business.IServices.Connector;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;

namespace HomeMind.Business.Services.Connectors.Adapters;

/// <summary>Home Assistant MCP 适配器，将受控 MCP 工具结果映射为既有标准化设备模型。</summary>
public sealed class HomeAssistantMcpAdapter : IDeviceAdapter, IDeviceDiscovery, IDeviceCommandExecutor
{
    private const string ListEntitiesTool = "ha_list_entities";
    private const string GetStateTool = "ha_get_state";
    private const string ControlDeviceTool = "ha_control_device";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IMcpClientManager _clientManager;
    private readonly HomeMindDbContext _db;
    private readonly HomeAssistantMcpOptions _options;

    /// <summary>构造只读 MCP 适配器。</summary>
    public HomeAssistantMcpAdapter(IMcpClientManager clientManager, HomeMindDbContext db, HomeAssistantMcpOptions options)
    {
        _clientManager = clientManager;
        _db = db;
        _options = options;
    }

    /// <inheritdoc />
    public string ProviderCode => "home_assistant";

    /// <inheritdoc />
    public async Task<ConnectorConnectionTestResult> TestConnectionAsync(ConnectorReference connector, CancellationToken cancellationToken = default)
    {
        try
        {
            var session = await _clientManager.EnsureConnectedAsync(_options.ServerName, cancellationToken);
            var manifest = await session.ListToolsAsync(cancellationToken);
            return HasTool(manifest, ListEntitiesTool) && HasTool(manifest, GetStateTool)
                ? new ConnectorConnectionTestResult(true, Message: "Home Assistant MCP 连接正常。")
                : new ConnectorConnectionTestResult(false, "mcp_tool_unavailable", "Home Assistant MCP 未提供所需只读工具。 ");
        }
        catch (Exception exception) when (exception is McpClientException or OperationCanceledException)
        {
            return new ConnectorConnectionTestResult(false, ToErrorCode(exception), "无法连接 Home Assistant MCP。 ");
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DiscoveredDevice>> DiscoverDevicesAsync(ConnectorReference connector, CancellationToken cancellationToken = default)
    {
        var session = await RequireToolAsync(ListEntitiesTool, cancellationToken);
        var result = await CallReadToolAsync(session, ListEntitiesTool, new JsonObject(), cancellationToken);
        var entities = GetEntities(result);
        var discovered = new List<DiscoveredDevice>();
        foreach (var entity in entities)
        {
            if (TryMapEntity(entity, out var device)) discovered.Add(device);
        }
        return discovered;
    }

    /// <inheritdoc />
    public async Task<AdapterDeviceState?> ReadDeviceStateAsync(ConnectorReference connector, long deviceId, CancellationToken cancellationToken = default)
    {
        var externalId = await _db.SmartHomeDevices
            .Where(x => x.Id == deviceId && x.TenantId == connector.TenantId && x.WorkspaceConnectorId == connector.ConnectorId && x.DeletedAt == null)
            .Select(x => x.ExternalId)
            .SingleOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(externalId)) return null;

        var session = await RequireToolAsync(GetStateTool, cancellationToken);
        var result = await CallReadToolAsync(session, GetStateTool, new JsonObject { ["entity_id"] = externalId }, cancellationToken);
        var entity = GetEntities(result).FirstOrDefault() ?? result as JsonObject;
        if (entity is null || !TryMapEntity(entity, out var device)) return null;
        using var document = JsonDocument.Parse(device.StateJson);
        return new AdapterDeviceState(deviceId, document.RootElement.Clone(), device.SampledAt);
    }

    /// <inheritdoc />
    public async Task<DeviceCommandResult> ExecuteCommandAsync(ConnectorReference connector, DeviceCommand command, CancellationToken cancellationToken = default)
    {
        var externalId = await _db.SmartHomeDevices
            .Where(x => x.Id == command.DeviceId && x.TenantId == connector.TenantId && x.WorkspaceConnectorId == connector.ConnectorId && x.DeletedAt == null)
            .Select(x => x.ExternalId)
            .SingleOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(externalId))
            return new DeviceCommandResult(false, "failed", "ha_entity_not_found", "目标设备不属于当前连接器。");
        if (!IsAllowedCommand(externalId, command.Capability, command.TargetValue))
            return new DeviceCommandResult(false, "failed", "ha_validation_failed", "设备能力或目标值不在允许的受控写入范围内。");

        IMcpClientSession session;
        try { session = await RequireToolAsync(ControlDeviceTool, cancellationToken); }
        catch (ConnectorAdapterException error) { return new DeviceCommandResult(false, "failed", error.ErrorCode, error.Message); }
        try
        {
            await session.CallToolAsync(ControlDeviceTool, new JsonObject
            {
                ["entity_id"] = externalId,
                ["capability"] = command.Capability,
                ["value"] = JsonNode.Parse(command.TargetValue.GetRawText())
            }, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new DeviceCommandResult(false, "result_unknown", "ha_result_unknown", "设备写入超时，结果未知，系统不会自动重试。");
        }
        catch (McpClientException)
        {
            return new DeviceCommandResult(false, "failed", "ha_disconnected", "Home Assistant MCP 写入调用失败。");
        }

        try
        {
            var state = await ReadDeviceStateAsync(connector, command.DeviceId, cancellationToken);
            return new DeviceCommandResult(true, "executed", Message: "设备行动已下发并完成状态回读。", StateJson: state is null ? null : state.State.GetRawText());
        }
        catch (ConnectorAdapterException error)
        {
            return new DeviceCommandResult(false, "result_unknown", error.ErrorCode == "ha_timeout" ? "ha_result_unknown" : error.ErrorCode, "设备写入已发送，但状态回读失败，结果未知。");
        }
    }

    private async Task<IMcpClientSession> RequireToolAsync(string toolName, CancellationToken cancellationToken)
    {
        try
        {
            var session = await _clientManager.EnsureConnectedAsync(_options.ServerName, cancellationToken);
            var manifest = await session.ListToolsAsync(cancellationToken);
            if (!HasTool(manifest, toolName)) throw new ConnectorAdapterException("mcp_tool_unavailable", "Home Assistant MCP 未提供所需只读工具。 ");
            return session;
        }
        catch (ConnectorAdapterException) { throw; }
        catch (Exception exception) when (exception is McpClientException or OperationCanceledException)
        {
            throw new ConnectorAdapterException(ToErrorCode(exception), "Home Assistant MCP 暂不可用。 ");
        }
    }

    private static async Task<JsonNode?> CallReadToolAsync(IMcpClientSession session, string toolName, JsonObject arguments, CancellationToken cancellationToken)
    {
        try { return await session.CallToolAsync(toolName, arguments, cancellationToken); }
        catch (McpClientException exception) { throw new ConnectorAdapterException("ha_disconnected", $"Home Assistant MCP 调用失败：{exception.Message}"); }
    }

    private static bool HasTool(McpToolManifest manifest, string toolName) => manifest.Tools.Any(x => string.Equals(x.Name, toolName, StringComparison.Ordinal));

    private static bool IsAllowedCommand(string externalId, string capability, JsonElement value)
    {
        var domain = externalId.Split('.', 2)[0];
        return (domain, capability) switch
        {
            ("light", "power") or ("switch", "power") or ("climate", "power") => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
            ("light", "brightness") => value.TryGetInt32(out var brightness) && brightness is >= 0 and <= 255,
            ("climate", "temperature") => value.TryGetDouble(out var temperature) && temperature is >= 5 and <= 35,
            ("climate", "mode") => value.ValueKind == JsonValueKind.String && value.GetString() is "heat" or "cool" or "auto" or "off",
            ("cover", "position") => value.TryGetInt32(out var position) && position is >= 0 and <= 100,
            _ => false
        };
    }

    private static IEnumerable<JsonObject> GetEntities(JsonNode? result)
    {
        var array = result switch
        {
            JsonArray direct => direct,
            JsonObject obj when obj["entities"] is JsonArray entities => entities,
            JsonObject obj when obj["data"] is JsonArray data => data,
            JsonObject obj when obj["entity"] is JsonObject entity => new JsonArray(entity),
            _ => null
        };
        return array?.OfType<JsonObject>() ?? Enumerable.Empty<JsonObject>();
    }

    private static bool TryMapEntity(JsonObject entity, out DiscoveredDevice device)
    {
        device = default!;
        var externalId = entity["entity_id"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(externalId)) return false;
        var domain = externalId.Split('.', 2)[0];
        if (domain is not ("light" or "switch" or "climate" or "cover" or "sensor" or "binary_sensor")) return false;
        var attributes = entity["attributes"] as JsonObject ?? new JsonObject();
        var state = entity["state"]?.GetValue<string>() ?? "unknown";
        var capabilities = domain switch
        {
            "light" => new[] { Capability("power", "boolean", true), Capability("brightness", "integer", true) },
            "switch" => new[] { Capability("power", "boolean", true) },
            "climate" => new[] { Capability("power", "boolean", true), Capability("temperature", "number", true), Capability("mode", "string", true) },
            "cover" => new[] { Capability("position", "integer", true) },
            _ => new[] { Capability(attributes["device_class"]?.GetValue<string>() ?? "value", "number", false) }
        };
        var normalizedState = new JsonObject();
        if (domain is "light" or "switch") normalizedState["power"] = state == "on";
        else if (domain == "climate") { normalizedState["power"] = state != "off"; normalizedState["mode"] = state; normalizedState["temperature"] = attributes["temperature"]?.DeepClone(); }
        else if (domain == "cover") normalizedState["position"] = attributes["current_position"]?.DeepClone();
        else normalizedState[attributes["device_class"]?.GetValue<string>() ?? "value"] = state;
        var timestamp = DateTime.TryParse(entity["last_updated"]?.GetValue<string>(), out var parsed) ? parsed.ToUniversalTime() : DateTime.UtcNow;
        var battery = ToByte(attributes["battery_level"] ?? attributes["battery"]);
        var lqi = ToInt32(attributes["signal_lqi"] ?? attributes["lqi"]);
        var online = state is "unavailable" or "unknown" ? "offline" : "online";
        var health = online == "offline" ? "offline" : battery is <= 20 ? "low_battery" : lqi is < 50 || battery is <= 40 ? "degraded" : "healthy";
        device = new DiscoveredDevice(externalId, attributes["friendly_name"]?.GetValue<string>() ?? externalId, domain == "climate" ? "air_conditioner" : domain, online, normalizedState.ToJsonString(JsonOptions), timestamp, attributes["area"]?.GetValue<string>(), capabilities, null, battery, lqi, health);
        return true;
    }

    private static DiscoveredDeviceCapability Capability(string capability, string type, bool writable) => new(capability, JsonSerializer.Serialize(new { type }), writable);
    private static byte? ToByte(JsonNode? node) => byte.TryParse(node?.ToString(), out var value) && value <= 100 ? value : null;
    private static int? ToInt32(JsonNode? node) => int.TryParse(node?.ToString(), out var value) && value is >= 0 and <= 255 ? value : null;
    private static string ToErrorCode(Exception exception) => exception is OperationCanceledException ? "ha_timeout" : "ha_disconnected";
}
