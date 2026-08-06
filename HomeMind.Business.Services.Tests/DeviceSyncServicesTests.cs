using System.Text.Json;
using HomeMind.Business.IServices.Connector;
using HomeMind.Business.IServices.SmartHome;
using HomeMind.Business.Services.Connectors.Adapters;
using HomeMind.Business.Services.Connectors.Bridge;
using HomeMind.Common.Model.Entities.SmartHome;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.SmartHome;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HomeMind.Business.Services.Tests;

/// <summary>设备同步桥接定向测试：发现结果标准化落库、健康字段派生与连接器状态回写。</summary>
public class DeviceSyncServicesTests
{
    private static readonly ConnectorReference Reference = new(1, 1, "vault://ha");

    /// <summary>发现结果落库为标准化设备：健康字段、空间归属与连接器状态回写。</summary>
    [Fact]
    public async Task SyncAsync_Persists_Standardized_Devices_And_Marks_Connector_Connected()
    {
        await using var db = NewDb("sync-persist");
        var connector = SeedConnector(db);
        var service = new DeviceSyncService(db, [new FakeDiscovery("ha")], new FakeAutomation());

        var count = await service.SyncAsync(1, connector, "ha", Reference, CancellationToken.None);

        Assert.Equal(1, count);
        var device = await db.SmartHomeDevices.SingleAsync();
        Assert.Equal("客厅主灯", device.Name);
        Assert.Equal("light", device.DeviceType);
        Assert.Equal("online", device.OnlineStatus);
        Assert.Equal("router", device.ZigbeeRole);
        Assert.Equal((byte)95, device.BatteryLevel);
        Assert.Equal(220, device.SignalLqi);
        Assert.Equal("healthy", device.HealthStatus);
        Assert.Equal("客厅", (await db.SmartHomeSpaces.SingleAsync()).Name);
        Assert.Equal("connected", connector.Status);
        Assert.NotNull(connector.LastSyncAt);
    }

    /// <summary>发现失败时抛出适配器异常，由调用方（ConnectorRuntimeServices）负责标记连接器失败。</summary>
    [Fact]
    public async Task SyncAsync_Throws_Connector_Error_On_Discovery_Failure()
    {
        await using var db = NewDb("sync-fail");
        var connector = SeedConnector(db);
        var service = new DeviceSyncService(db, [new FakeDiscovery("ha", fail: true)], new FakeAutomation());

        var error = await Assert.ThrowsAsync<ConnectorAdapterException>(() => service.SyncAsync(1, connector, "ha", Reference, CancellationToken.None));
        Assert.Equal("unreachable", error.ErrorCode);
    }

    /// <summary>MarkFailedAsync 把连接器状态回写为失败并记录健康检查时间。</summary>
    [Fact]
    public async Task MarkFailedAsync_Marks_Connector_Failed()
    {
        await using var db = NewDb("sync-markfailed");
        var connector = SeedConnector(db);
        var service = new DeviceSyncService(db, [new FakeDiscovery("ha")], new FakeAutomation());

        await service.MarkFailedAsync(connector, CancellationToken.None);

        Assert.Equal("failed", connector.Status);
        Assert.NotNull(connector.LastHealthAt);
    }

    /// <summary>未注册的 Provider 返回适配器不可用异常。</summary>
    [Fact]
    public async Task SyncAsync_Throws_For_Unknown_Provider()
    {
        await using var db = NewDb("sync-unknown");
        var connector = SeedConnector(db);
        var service = new DeviceSyncService(db, [new FakeDiscovery("ha")], new FakeAutomation());

        var error = await Assert.ThrowsAsync<ConnectorAdapterException>(() => service.SyncAsync(1, connector, "mqtt", Reference, CancellationToken.None));
        Assert.Equal("adapter_unavailable", error.ErrorCode);
    }

    private static HomeMindDbContext NewDb(string name) =>
        new(new DbContextOptionsBuilder<HomeMindDbContext>()
            .UseInMemoryDatabase($"hm-b13-device-sync-{name}-{Guid.NewGuid()}")
            .Options);

    private static WorkspaceConnector SeedConnector(HomeMindDbContext db)
    {
        var connector = new WorkspaceConnector { TenantId = 1, Name = "Home Assistant", Status = "pending", CredentialRef = "vault://ha", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        db.WorkspaceConnectors.Add(connector);
        db.SaveChanges();
        return connector;
    }

    private sealed class FakeDiscovery : IDeviceDiscovery
    {
        public FakeDiscovery(string providerCode, bool fail = false)
        {
            ProviderCode = providerCode;
            _fail = fail;
        }

        private readonly bool _fail;
        public string ProviderCode { get; }

        public Task<IReadOnlyList<DiscoveredDevice>> DiscoverDevicesAsync(ConnectorReference connector, CancellationToken cancellationToken = default)
        {
            if (_fail) throw new ConnectorAdapterException("unreachable", "无法连接设备服务。");
            var device = new DiscoveredDevice(
                "light.living_room_main",
                "客厅主灯",
                "light",
                "online",
                JsonSerializer.Serialize(new { power = true }),
                DateTime.UtcNow,
                "客厅",
                [new DiscoveredDeviceCapability("power", JsonSerializer.Serialize(new { type = "boolean" }), true)],
                "router",
                95,
                220,
                "healthy");
            return Task.FromResult<IReadOnlyList<DiscoveredDevice>>([device]);
        }
    }

    private sealed class FakeAutomation : IAutomationRuleServices
    {
        public Task<ServiceResult> ListAsync(long tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ServiceResult(200, "ok"));
        public Task<ServiceResult> CreateAsync(long userId, long tenantId, AutomationRuleRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ServiceResult(201, "ok"));
        public Task<ServiceResult> UpdateAsync(long userId, long tenantId, long ruleId, UpdateAutomationRuleRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ServiceResult(200, "ok"));
        public Task<ServiceResult> HandleDeviceStateChangeAsync(long tenantId, long deviceId, string state, DateTime occurredAt, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ServiceResult(200, "ok"));
        public Task<ServiceResult> HandleSceneCompletedAsync(long tenantId, string sceneKey, DateTime occurredAt, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ServiceResult(200, "ok"));
        public Task<ServiceResult> HandleSyncCompletedAsync(long tenantId, long connectorId, DateTime occurredAt, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ServiceResult(200, "ok"));
        public Task<int> ProcessDueSchedulesAsync(DateTime now, CancellationToken cancellationToken = default) =>
            Task.FromResult(0);
    }
}
