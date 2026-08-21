using System.Text.Json;
using HomeMind.Business.IServices.Connector;
using HomeMind.Business.Services.Connectors.Adapters;
using Xunit;

namespace HomeMind.Business.Services.Tests;

public class HomeAssistantConnectorAdapterMappingTests
{
    [Fact]
    public void Maps_All_Health_Fields_For_Healthy_Online_Device()
    {
        var json = """
        [
          {
            "entity_id": "light.living_room_main",
            "state": "on",
            "last_updated": "2026-08-05T10:00:00Z",
            "attributes": {
              "friendly_name": "客厅主灯",
              "area": "客厅",
              "zigbee_role": "router",
              "battery_level": 95,
              "signal_lqi": 220
            }
          }
        ]
        """;
        using var document = JsonDocument.Parse(json);
        var entity = document.RootElement.EnumerateArray().First();

        var device = Map(entity);

        Assert.Equal("router", device.ZigbeeRole);
        Assert.Equal((byte)95, device.BatteryLevel);
        Assert.Equal(220, device.SignalLqi);
        Assert.Equal("healthy", device.HealthStatus);
    }

    [Fact]
    public void Marks_Low_Battery_When_Battery_Below_20()
    {
        var device = Map(BuildEntity(battery: 15, lqi: 120, state: "on", role: "end_device"));
        Assert.Equal("low_battery", device.HealthStatus);
        Assert.Equal((byte)15, device.BatteryLevel);
    }

    [Fact]
    public void Marks_Offline_When_State_Is_Unavailable()
    {
        var device = Map(BuildEntity(battery: 80, lqi: 200, state: "unavailable", role: "end_device"));
        Assert.Equal("offline", device.HealthStatus);
    }

    [Fact]
    public void Falls_Back_To_Healthy_When_Health_Fields_Missing()
    {
        var device = Map(BuildEntity(battery: null, lqi: null, state: "on", role: null));
        Assert.Null(device.ZigbeeRole);
        Assert.Null(device.BatteryLevel);
        Assert.Null(device.SignalLqi);
        Assert.Equal("healthy", device.HealthStatus);
    }

    [Fact]
    public void Drops_Unknown_Zigbee_Role_As_Null()
    {
        var device = Map(BuildEntity(battery: 60, lqi: 180, state: "on", role: "made_up"));
        Assert.Null(device.ZigbeeRole);
        // 健康派生只依据 online/battery/lqi，角色未知不影响健康状态（battery=60、lqi=180 未达降级阈值）。
        Assert.Equal("healthy", device.HealthStatus);
    }

    [Fact]
    public void Maps_State_Changed_Payload_To_The_Same_Normalized_State_As_Discovery()
    {
        var device = Map(BuildEntity(battery: null, lqi: null, state: "on", role: null));

        using var state = JsonDocument.Parse(device.StateJson);
        Assert.True(state.RootElement.GetProperty("power").GetBoolean());
        Assert.Equal("online", device.OnlineStatus);
    }

    private static DiscoveredDevice Map(JsonElement entity) =>
        InvokeTryMapEntity(entity);

    private static DiscoveredDevice InvokeTryMapEntity(JsonElement entity)
    {
        var method = typeof(HomeAssistantAdapter)
            .GetMethod("TryMapEntity", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(method);
        var args = new object?[] { entity, null };
        var ok = (bool)method!.Invoke(null, args)!;
        Assert.True(ok);
        return (DiscoveredDevice)args[1]!;
    }

    private static JsonElement BuildEntity(byte? battery, int? lqi, string state, string? role)
    {
        var attributes = new Dictionary<string, object> { ["friendly_name"] = "x" };
        if (battery.HasValue) attributes["battery_level"] = battery.Value;
        if (lqi.HasValue) attributes["signal_lqi"] = lqi.Value;
        if (role is not null) attributes["zigbee_role"] = role;
        var payload = new
        {
            entity_id = "light.test",
            state,
            last_updated = "2026-08-05T10:00:00Z",
            attributes
        };
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(payload));
        return document.RootElement.Clone();
    }
}
