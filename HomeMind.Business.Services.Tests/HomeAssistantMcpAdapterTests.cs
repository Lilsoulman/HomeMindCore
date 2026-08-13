using System.Text.Json.Nodes;
using System.Text.Json;
using HomeMind.Business.IServices.Connector;
using HomeMind.Business.Services.Connectors.Adapters;
using HomeMind.Business.Services.Connectors.Mcp;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HomeMind.Business.Services.Tests;

/// <summary>Home Assistant MCP 只读适配器的定向测试。</summary>
public class HomeAssistantMcpAdapterTests
{
    /// <summary>发现仅映射为标准化设备，供应商实体标识不属于外部视图模型。</summary>
    [Fact]
    public async Task DiscoverDevicesAsync_Maps_ReadOnly_Mcp_Entities_To_Normalized_Devices()
    {
        await using var db = NewDb();
        var session = new FakeSession(["ha_list_entities", "ha_get_state"])
        {
            Responses =
            {
                ["ha_list_entities"] = new JsonObject
                {
                    ["entities"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["entity_id"] = "light.living_room_main",
                            ["state"] = "on",
                            ["last_updated"] = "2026-08-12T12:00:00Z",
                            ["attributes"] = new JsonObject { ["friendly_name"] = "客厅主灯", ["battery_level"] = 90, ["signal_lqi"] = 220 }
                        }
                    }
                }
            }
        };
        await using var manager = new McpClientManager(_ => session);
        var adapter = new HomeAssistantMcpAdapter(manager, db, new HomeAssistantMcpOptions());

        var devices = await adapter.DiscoverDevicesAsync(new ConnectorReference(7, 1, "unused"));

        var device = Assert.Single(devices);
        Assert.Equal("客厅主灯", device.Name);
        Assert.Equal("light", device.DeviceType);
        Assert.Equal("healthy", device.HealthStatus);
        Assert.Equal("light.living_room_main", device.ExternalId);
        Assert.Equal("ha_list_entities", session.Calls.Single());
    }

    /// <summary>缺少受控只读工具时拒绝连接，不尝试调用任意 MCP 工具。</summary>
    [Fact]
    public async Task TestConnectionAsync_Rejects_Missing_Required_Read_Tools()
    {
        await using var db = NewDb();
        await using var manager = new McpClientManager(_ => new FakeSession(["ha_list_entities"]));
        var adapter = new HomeAssistantMcpAdapter(manager, db, new HomeAssistantMcpOptions());

        var result = await adapter.TestConnectionAsync(new ConnectorReference(7, 1, "unused"));

        Assert.False(result.Succeeded);
        Assert.Equal("mcp_tool_unavailable", result.ErrorCode);
    }

    /// <summary>读取状态按连接器和租户约束设备，跨家庭设备不会向 MCP 泄露实体标识。</summary>
    [Fact]
    public async Task ReadDeviceStateAsync_Does_Not_Call_Mcp_For_CrossTenant_Device()
    {
        await using var db = NewDb();
        db.SmartHomeDevices.Add(new HomeMind.Common.Model.Entities.SmartHome.SmartHomeDevice
        {
            TenantId = 2,
            WorkspaceConnectorId = 7,
            ExternalId = "light.other_home",
            Name = "其他家庭主灯",
            DeviceType = "light",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var session = new FakeSession(["ha_list_entities", "ha_get_state"]);
        await using var manager = new McpClientManager(_ => session);
        var adapter = new HomeAssistantMcpAdapter(manager, db, new HomeAssistantMcpOptions());

        var state = await adapter.ReadDeviceStateAsync(new ConnectorReference(7, 1, "unused"), 1);

        Assert.Null(state);
        Assert.Empty(session.Calls);
    }

    /// <summary>H4 仅允许已映射设备通过固定工具写入，并在成功后回读标准化状态。</summary>
    [Fact]
    public async Task ExecuteCommandAsync_Calls_Control_Tool_And_Reads_Back_State()
    {
        await using var db = NewDb();
        db.SmartHomeDevices.Add(new HomeMind.Common.Model.Entities.SmartHome.SmartHomeDevice
        {
            TenantId = 1,
            WorkspaceConnectorId = 7,
            ExternalId = "light.living_room_main",
            Name = "客厅主灯",
            DeviceType = "light",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var session = new FakeSession(["ha_list_entities", "ha_get_state", "ha_control_device"])
        {
            Responses =
            {
                ["ha_get_state"] = new JsonObject
                {
                    ["entity_id"] = "light.living_room_main",
                    ["state"] = "on",
                    ["last_updated"] = "2026-08-12T12:00:00Z",
                    ["attributes"] = new JsonObject()
                }
            }
        };
        await using var manager = new McpClientManager(_ => session);
        var adapter = new HomeAssistantMcpAdapter(manager, db, new HomeAssistantMcpOptions());
        using var targetDocument = JsonDocument.Parse("true");
        var command = new DeviceCommand(7, 1, "power", targetDocument.RootElement.Clone(), 1, 1, Guid.NewGuid().ToString());

        var result = await adapter.ExecuteCommandAsync(new ConnectorReference(7, 1, "unused"), command);

        Assert.True(result.Succeeded);
        Assert.Equal("executed", result.Status);
        Assert.Equal(["ha_control_device", "ha_get_state"], session.Calls);
        Assert.Contains("power", result.StateJson);
    }

    /// <summary>超时后的写操作标记为结果未知，避免调用方自动重复产生副作用。</summary>
    [Fact]
    public async Task ExecuteCommandAsync_Returns_ResultUnknown_When_Control_Tool_Times_Out()
    {
        await using var db = NewDb();
        db.SmartHomeDevices.Add(new HomeMind.Common.Model.Entities.SmartHome.SmartHomeDevice
        {
            TenantId = 1,
            WorkspaceConnectorId = 7,
            ExternalId = "switch.kitchen",
            Name = "厨房开关",
            DeviceType = "switch",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var session = new FakeSession(["ha_list_entities", "ha_get_state", "ha_control_device"]) { ThrowOnCall = true };
        await using var manager = new McpClientManager(_ => session);
        var adapter = new HomeAssistantMcpAdapter(manager, db, new HomeAssistantMcpOptions());
        using var targetDocument = JsonDocument.Parse("true");

        var result = await adapter.ExecuteCommandAsync(new ConnectorReference(7, 1, "unused"), new DeviceCommand(7, 1, "power", targetDocument.RootElement.Clone(), 1, 1, Guid.NewGuid().ToString()));

        Assert.False(result.Succeeded);
        Assert.Equal("result_unknown", result.Status);
        Assert.Equal("ha_result_unknown", result.ErrorCode);
    }

    private static HomeMindDbContext NewDb() => new(new DbContextOptionsBuilder<HomeMindDbContext>().UseInMemoryDatabase($"hm-h2-{Guid.NewGuid()}").Options);

    private sealed class FakeSession(IEnumerable<string> tools) : IMcpClientSession
    {
        private readonly McpToolManifest _manifest = new(tools.Select(name => new McpToolDefinition(name, null, null)).ToArray(), "test");
        public Dictionary<string, JsonNode?> Responses { get; } = new(StringComparer.Ordinal);
        public List<string> Calls { get; } = [];
        public bool ThrowOnCall { get; init; }
        public event EventHandler<McpToolsChangedEventArgs>? ToolsChanged;
        public Task<McpInitializeResult> InitializeAsync(CancellationToken cancellationToken = default) => Task.FromResult(new McpInitializeResult("2025-03-26", "fake-ha", "1"));
        public Task<McpToolManifest> ListToolsAsync(CancellationToken cancellationToken = default) => Task.FromResult(_manifest);
        public Task<JsonNode?> CallToolAsync(string toolName, JsonObject? arguments, CancellationToken cancellationToken = default)
        {
            Calls.Add(toolName);
            if (ThrowOnCall) throw new OperationCanceledException();
            return Task.FromResult(Responses.GetValueOrDefault(toolName));
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
