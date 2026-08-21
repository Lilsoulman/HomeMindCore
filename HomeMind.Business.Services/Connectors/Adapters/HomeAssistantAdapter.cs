using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using HomeMind.Business.IServices.Connector;
using HomeMind.Business.IServices.SmartHome;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;

namespace HomeMind.Business.Services.Connectors.Adapters;

/// <summary>
/// Home Assistant REST Adapter，同时实现设备边界三契约（<see cref="IDeviceAdapter"/>、<see cref="IDeviceDiscovery"/>、<see cref="IDeviceCommandExecutor"/>）。
/// 供应商实体和服务名只停留在本类内，业务层只处理标准化能力。
/// </summary>
public sealed class HomeAssistantAdapter : IDeviceAdapter, IDeviceDiscovery, IDeviceCommandExecutor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IConnectorSecretResolver _secrets;
    private readonly HomeMindDbContext _db;

    public HomeAssistantAdapter(IConnectorSecretResolver secrets, HomeMindDbContext db)
    {
        _secrets = secrets;
        _db = db;
    }

    public string ProviderCode => "home_assistant";

    public async Task<ConnectorConnectionTestResult> TestConnectionAsync(ConnectorReference connector, CancellationToken cancellationToken = default)
    {
        var clientResult = await CreateClientAsync(connector, cancellationToken);
        if (clientResult.Client is null) return new ConnectorConnectionTestResult(false, clientResult.ErrorCode, clientResult.Message);

        using var client = clientResult.Client;
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/");
        try
        {
            using var response = await client.SendAsync(request, cancellationToken);
            return response.IsSuccessStatusCode
                ? new ConnectorConnectionTestResult(true, Message: "Home Assistant 连接正常。")
                : new ConnectorConnectionTestResult(false, ToErrorCode(response.StatusCode), "Home Assistant 拒绝了连接测试请求。") ;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new ConnectorConnectionTestResult(false, "timeout", "连接 Home Assistant 超时。 ");
        }
        catch (HttpRequestException)
        {
            return new ConnectorConnectionTestResult(false, "unreachable", "无法连接 Home Assistant。 ");
        }
    }

    public async Task<IReadOnlyList<DiscoveredDevice>> DiscoverDevicesAsync(ConnectorReference connector, CancellationToken cancellationToken = default)
    {
        var clientResult = await CreateClientAsync(connector, cancellationToken);
        if (clientResult.Client is null) throw new ConnectorAdapterException(clientResult.ErrorCode!, clientResult.Message!);

        using var client = clientResult.Client;
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/states");
        try
        {
            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode) throw new ConnectorAdapterException(ToErrorCode(response.StatusCode), "Home Assistant 未能提供设备状态。 ");

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (document.RootElement.ValueKind != JsonValueKind.Array) throw new ConnectorAdapterException("invalid_response", "Home Assistant 返回了无法识别的设备数据。 ");

            var discovered = new List<DiscoveredDevice>();
            foreach (var entity in document.RootElement.EnumerateArray())
            {
                if (TryMapEntity(entity, out var device)) discovered.Add(device);
            }
            return discovered;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ConnectorAdapterException("timeout", "同步 Home Assistant 设备状态超时。 ");
        }
        catch (HttpRequestException)
        {
            throw new ConnectorAdapterException("unreachable", "无法连接 Home Assistant。 ");
        }
    }

    public async Task<AdapterDeviceState?> ReadDeviceStateAsync(ConnectorReference connector, long deviceId, CancellationToken cancellationToken = default)
    {
        var externalId = await _db.SmartHomeDevices
            .Where(x => x.Id == deviceId && x.TenantId == connector.TenantId && x.WorkspaceConnectorId == connector.ConnectorId && x.DeletedAt == null)
            .Select(x => x.ExternalId)
            .SingleOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(externalId)) return null;

        var clientResult = await CreateClientAsync(connector, cancellationToken);
        if (clientResult.Client is null) return null;

        using var client = clientResult.Client;
        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/states/{Uri.EscapeDataString(externalId)}");
        try
        {
            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode) return null;

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (!TryMapEntity(document.RootElement, out var device)) return null;
            using var normalizedState = JsonDocument.Parse(device.StateJson);
            return new AdapterDeviceState(deviceId, normalizedState.RootElement.Clone(), device.SampledAt);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public async Task<DeviceCommandResult> ExecuteCommandAsync(ConnectorReference connector, DeviceCommand command, CancellationToken cancellationToken = default)
    {
        var externalId = await _db.SmartHomeDevices
            .Where(x => x.Id == command.DeviceId && x.TenantId == connector.TenantId && x.WorkspaceConnectorId == connector.ConnectorId && x.DeletedAt == null)
            .Select(x => x.ExternalId)
            .SingleOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(externalId))
            return new DeviceCommandResult(false, "failed", "device_not_found", "设备未同步到当前连接器。");
        if (!TryBuildCommand(externalId, command.Capability, command.TargetValue, out var endpoint, out var payload))
            return new DeviceCommandResult(false, "failed", "unsupported_command", "该设备能力暂不支持写入。");

        var clientResult = await CreateClientAsync(connector, cancellationToken);
        if (clientResult.Client is null)
            return new DeviceCommandResult(false, "failed", clientResult.ErrorCode, clientResult.Message);

        using var client = clientResult.Client;
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json")
        };
        try
        {
            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return new DeviceCommandResult(false, "failed", ToErrorCode(response.StatusCode), "设备服务拒绝了该行动。");

            var state = await ReadDeviceStateAsync(connector, command.DeviceId, cancellationToken);
            return new DeviceCommandResult(true, "executed", Message: "设备行动已下发并完成状态回读。", StateJson: state?.State.GetRawText());
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new DeviceCommandResult(false, "failed", "timeout", "设备行动执行超时。");
        }
        catch (HttpRequestException)
        {
            return new DeviceCommandResult(false, "failed", "unreachable", "无法连接设备服务。");
        }
    }

    private async Task<ClientCreationResult> CreateClientAsync(ConnectorReference connector, CancellationToken cancellationToken)
    {
        var secret = await _secrets.ResolveAsync(connector, cancellationToken);
        if (!secret.Succeeded || string.IsNullOrWhiteSpace(secret.SecretJson))
            return new ClientCreationResult(null, secret.ErrorCode ?? "secret_vault_unavailable", secret.Message ?? "无法读取连接器凭据。 ");

        try
        {
            using var document = JsonDocument.Parse(secret.SecretJson);
            var root = document.RootElement;
            if (!root.TryGetProperty("baseUrl", out var baseUrlElement) || !root.TryGetProperty("accessToken", out var tokenElement))
                return new ClientCreationResult(null, "invalid_secret", "Home Assistant 凭据格式无效。 ");
            var baseUrl = baseUrlElement.GetString();
            var accessToken = tokenElement.GetString();
            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") || string.IsNullOrWhiteSpace(accessToken))
                return new ClientCreationResult(null, "invalid_secret", "Home Assistant 凭据格式无效。 ");

            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            client.BaseAddress = new Uri(baseUrl!.TrimEnd('/') + "/", UriKind.Absolute);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            return new ClientCreationResult(client, null, null);
        }
        catch (JsonException)
        {
            return new ClientCreationResult(null, "invalid_secret", "Home Assistant 凭据格式无效。 ");
        }
    }

    internal static bool TryMapEntity(JsonElement entity, out DiscoveredDevice device)
    {
        device = default!;
        if (!entity.TryGetProperty("entity_id", out var idElement) || idElement.ValueKind != JsonValueKind.String) return false;
        var externalId = idElement.GetString();
        if (string.IsNullOrWhiteSpace(externalId)) return false;
        var domain = externalId.Split('.', 2)[0];
        if (domain is not ("light" or "switch" or "climate" or "cover" or "sensor" or "binary_sensor")) return false;

        var attributes = entity.TryGetProperty("attributes", out var attrs) && attrs.ValueKind == JsonValueKind.Object ? attrs : default;
        var name = GetString(attributes, "friendly_name") ?? externalId;
        var state = GetString(entity, "state") ?? "unknown";
        var online = state is "unavailable" or "unknown" ? "offline" : "online";
        var sampledAt = entity.TryGetProperty("last_updated", out var updated) && DateTime.TryParse(updated.GetString(), out var timestamp)
            ? timestamp.ToUniversalTime()
            : DateTime.UtcNow;
        var spaceName = GetString(attributes, "area");
        var (deviceType, capabilities) = MapCapabilities(domain, attributes);
        var normalizedState = BuildState(domain, state, attributes);
        var zigbeeRole = MapZigbeeRole(attributes);
        var batteryLevel = MapBatteryLevel(attributes);
        var signalLqi = MapSignalLqi(attributes);
        var healthStatus = DeriveHealthStatus(online, batteryLevel, signalLqi);
        device = new DiscoveredDevice(externalId, name, deviceType, online, JsonSerializer.Serialize(normalizedState, JsonOptions), sampledAt, spaceName, capabilities, zigbeeRole, batteryLevel, signalLqi, healthStatus);
        return true;
    }

    private static (string DeviceType, IReadOnlyList<DiscoveredDeviceCapability> Capabilities) MapCapabilities(string domain, JsonElement attributes) => domain switch
    {
        "light" => ("light", [Capability("power", "boolean", true, "smart_home.light.write"), Capability("brightness", "integer", true, "smart_home.light.write")]),
        "switch" => ("switch", [Capability("power", "boolean", true, "smart_home.switch.write")]),
        "climate" => ("air_conditioner", [Capability("power", "boolean", true, "smart_home.air_conditioner.write"), Capability("temperature", "number", true, "smart_home.air_conditioner.write"), Capability("mode", "string", true, "smart_home.air_conditioner.write")]),
        "cover" => ("cover", [Capability("position", "integer", true, "smart_home.cover.write")]),
        _ => ("sensor", [Capability(SensorCapability(attributes), "number", false, "smart_home.environment.read")])
    };

    private static DiscoveredDeviceCapability Capability(string name, string type, bool writable, string permission) =>
        new(name, JsonSerializer.Serialize(new Dictionary<string, string> { ["type"] = type }), writable);

    private static string SensorCapability(JsonElement attributes) => GetString(attributes, "device_class") switch
    {
        "temperature" => "temperature",
        "humidity" => "humidity",
        "battery" => "battery",
        "motion" => "motion",
        _ => "value"
    };

    private static Dictionary<string, object?> BuildState(string domain, string state, JsonElement attributes)
    {
        var result = new Dictionary<string, object?>();
        switch (domain)
        {
            case "light":
            case "switch": result["power"] = state == "on"; break;
            case "climate":
                result["power"] = state != "off";
                result["mode"] = state;
                AddNumber(attributes, "temperature", "temperature", result);
                break;
            case "cover": AddNumber(attributes, "current_position", "position", result); break;
            default: result[SensorCapability(attributes)] = state; break;
        }
        return result;
    }

    private static void AddNumber(JsonElement attributes, string source, string target, IDictionary<string, object?> state)
    {
        if (attributes.ValueKind == JsonValueKind.Object && attributes.TryGetProperty(source, out var value) && value.TryGetDouble(out var number)) state[target] = number;
    }

    private static string? GetString(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static readonly HashSet<string> AllowedZigbeeRoles = new(StringComparer.OrdinalIgnoreCase) { "end_device", "router", "coordinator" };

    private static string? MapZigbeeRole(JsonElement attributes)
    {
        var value = GetString(attributes, "zigbee_role") ?? GetString(attributes, "zigbee_role_hint");
        return value is null ? null : AllowedZigbeeRoles.Contains(value) ? value.ToLowerInvariant() : null;
    }

    private static byte? MapBatteryLevel(JsonElement attributes)
    {
        if (attributes.ValueKind != JsonValueKind.Object) return null;
        if (!attributes.TryGetProperty("battery_level", out var element) && !attributes.TryGetProperty("battery", out element)) return null;
        if (element.ValueKind == JsonValueKind.Number && element.TryGetByte(out var direct) && direct <= 100) return direct;
        if (element.ValueKind == JsonValueKind.String && byte.TryParse(element.GetString(), out var parsed) && parsed <= 100) return parsed;
        return null;
    }

    private static int? MapSignalLqi(JsonElement attributes)
    {
        if (attributes.ValueKind != JsonValueKind.Object) return null;
        if (!attributes.TryGetProperty("signal_lqi", out var element) && !attributes.TryGetProperty("lqi", out element)) return null;
        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var direct) && direct >= 0 && direct <= 255) return direct;
        if (element.ValueKind == JsonValueKind.String && int.TryParse(element.GetString(), out var parsed) && parsed >= 0 && parsed <= 255) return parsed;
        return null;
    }

    private static string DeriveHealthStatus(string onlineStatus, byte? batteryLevel, int? signalLqi)
    {
        if (onlineStatus == "offline") return "offline";
        if (batteryLevel is <= 20) return "low_battery";
        if (signalLqi is < 50 || batteryLevel is <= 40) return "degraded";
        return "healthy";
    }

    private static bool TryBuildCommand(string externalId, string capability, JsonElement targetValue, out string endpoint, out string payload)
    {
        endpoint = "";
        payload = "";
        var domain = externalId.Split('.', 2)[0];
        var body = new Dictionary<string, object?> { ["entity_id"] = externalId };

        switch ((domain, capability))
        {
            case ("light", "power") or ("switch", "power") or ("climate", "power") when targetValue.ValueKind is JsonValueKind.True or JsonValueKind.False:
                endpoint = $"api/services/{domain}/{(targetValue.GetBoolean() ? "turn_on" : "turn_off")}";
                break;
            case ("light", "brightness") when targetValue.TryGetInt32(out var brightness):
                endpoint = "api/services/light/turn_on";
                body["brightness"] = brightness;
                break;
            case ("climate", "temperature") when targetValue.TryGetDouble(out var temperature):
                endpoint = "api/services/climate/set_temperature";
                body["temperature"] = temperature;
                break;
            case ("climate", "mode") when targetValue.ValueKind == JsonValueKind.String:
                endpoint = "api/services/climate/set_hvac_mode";
                body["hvac_mode"] = targetValue.GetString();
                break;
            case ("cover", "position") when targetValue.TryGetInt32(out var position):
                endpoint = "api/services/cover/set_cover_position";
                body["position"] = position;
                break;
            default:
                return false;
        }

        payload = JsonSerializer.Serialize(body, JsonOptions);
        return true;
    }

    private static string ToErrorCode(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "authentication_failed",
        HttpStatusCode.NotFound => "not_found",
        _ => "remote_error"
    };

    private sealed record ClientCreationResult(HttpClient? Client, string? ErrorCode, string? Message);
}

/// <summary>适配器连接级异常；携带错误码供业务层映射 HTTP 状态。</summary>
public sealed class ConnectorAdapterException : Exception
{
    /// <summary>构造连接级异常。</summary>
    /// <param name="errorCode">错误码，如 timeout / unreachable / invalid_response。</param>
    /// <param name="message">面向用户的中文说明。</param>
    public ConnectorAdapterException(string errorCode, string message) : base(message) => ErrorCode = errorCode;
    /// <summary>错误码。</summary>
    public string ErrorCode { get; }
}
