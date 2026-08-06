using HomeMind.Business.IServices.Connector;
using HomeMind.Business.IServices.SmartHome;
using HomeMind.Business.Services.Connectors.Adapters;
using HomeMind.Business.Services.SmartHome;
using HomeMind.Common.Model.Entities.SmartHome;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;

namespace HomeMind.Business.Services.Connectors.Bridge;

/// <summary>
/// 设备状态同步桥接服务。负责把适配器发现结果转换为标准化设备状态、健康信息并落库，
/// 再触发自动化状态变更回调；业务层与 Controller 不感知任何具体厂商实现。
/// </summary>
public sealed class DeviceSyncService
{
    private readonly HomeMindDbContext _db;
    private readonly IReadOnlyDictionary<string, IDeviceDiscovery> _discovery;
    private readonly IAutomationRuleServices _automation;

    /// <summary>构造设备同步桥接服务。</summary>
    /// <param name="db">数据库上下文。</param>
    /// <param name="discovery">全部设备发现适配器，按 ProviderCode 索引。</param>
    /// <param name="automation">自动化规则服务，用于状态变更与同步完成回调。</param>
    public DeviceSyncService(HomeMindDbContext db, IEnumerable<IDeviceDiscovery> discovery, IAutomationRuleServices automation)
    {
        _db = db;
        _discovery = discovery.ToDictionary(x => x.ProviderCode, StringComparer.OrdinalIgnoreCase);
        _automation = automation;
    }

    /// <summary>
    /// 执行一次设备发现与标准化落库：同步空间、设备、能力、状态与健康字段，
    /// 回写连接器为已连接状态，并对发生状态变更的设备触发自动化回调。
    /// </summary>
    /// <param name="tenantId">租户主键，由 JWT 推导。</param>
    /// <param name="connector">连接器实例，方法内更新健康与同步时间。</param>
    /// <param name="providerCode">连接器 Provider 编码，用于选择发现适配器。</param>
    /// <param name="reference">连接器引用（含凭据引用）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>本次发现的设备数量。</returns>
    /// <exception cref="ConnectorAdapterException">连接失败、超时或响应不可识别时抛出。</exception>
    public async Task<int> SyncAsync(long tenantId, WorkspaceConnector connector, string providerCode, ConnectorReference reference, CancellationToken cancellationToken)
    {
        if (!_discovery.TryGetValue(providerCode, out var discovery))
            throw new ConnectorAdapterException("adapter_unavailable", "该连接器尚未提供运行期适配器。");

        var discovered = await discovery.DiscoverDevicesAsync(reference, cancellationToken);

        var now = DateTime.UtcNow;
        var changedDeviceIds = new List<long>();
        foreach (var device in discovered)
        {
            var space = await FindOrCreateSpaceAsync(tenantId, device.SpaceName, cancellationToken);
            var persisted = await _db.SmartHomeDevices.SingleOrDefaultAsync(
                x => x.WorkspaceConnectorId == connector.Id && x.ExternalId == device.ExternalId,
                cancellationToken);
            if (persisted is null)
            {
                persisted = new SmartHomeDevice
                {
                    TenantId = tenantId,
                    WorkspaceConnectorId = connector.Id,
                    ExternalId = device.ExternalId,
                    CreatedAt = now
                };
                _db.SmartHomeDevices.Add(persisted);
            }

            persisted.SpaceId = space.Id;
            persisted.Name = device.Name;
            persisted.DeviceType = device.DeviceType;
            persisted.OnlineStatus = device.OnlineStatus;
            persisted.ZigbeeRole = device.ZigbeeRole;
            persisted.BatteryLevel = device.BatteryLevel;
            persisted.SignalLqi = device.SignalLqi;
            persisted.HealthStatus = device.HealthStatus ?? "healthy";
            persisted.StateSummary = StateSummary(device);
            persisted.LastSeenAt = device.SampledAt;
            persisted.UpdatedAt = now;
            persisted.DeletedAt = null;
            await UpsertCapabilitiesAsync(persisted, device.Capabilities, now, cancellationToken);
            var previousState = await _db.DeviceStates.Where(x => x.DeviceId == persisted.Id).OrderByDescending(x => x.SampledAt).Select(x => x.State).FirstOrDefaultAsync(cancellationToken);
            _db.DeviceStates.Add(new DeviceState { DeviceId = persisted.Id, State = device.StateJson, SampledAt = device.SampledAt, CreatedAt = now });
            if (!string.Equals(previousState, device.StateJson, StringComparison.Ordinal)) changedDeviceIds.Add(persisted.Id);
        }

        connector.Status = "connected";
        connector.LastHealthAt = now;
        connector.LastSyncAt = now;
        connector.UpdatedAt = now;
        await _db.SaveChangesAsync(cancellationToken);
        foreach (var deviceId in changedDeviceIds)
        {
            var state = discovered.FirstOrDefault(x => x.ExternalId == _db.SmartHomeDevices.Local.FirstOrDefault(d => d.Id == deviceId)?.ExternalId)?.StateJson ?? "{}";
            await _automation.HandleDeviceStateChangeAsync(tenantId, deviceId, state, now, cancellationToken);
        }
        await _automation.HandleSyncCompletedAsync(tenantId, connector.Id, now, cancellationToken);
        return discovered.Count;
    }

    /// <summary>把连接器标记为同步失败并记录健康检查时间。</summary>
    /// <param name="connector">连接器实例，状态回写为 failed。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public async Task MarkFailedAsync(WorkspaceConnector connector, CancellationToken cancellationToken)
    {
        connector.Status = "failed";
        connector.LastHealthAt = DateTime.UtcNow;
        connector.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<SmartHomeSpace> FindOrCreateSpaceAsync(long tenantId, string? name, CancellationToken cancellationToken)
    {
        var normalizedName = string.IsNullOrWhiteSpace(name) ? "未分配空间" : name.Trim();
        var existing = await _db.SmartHomeSpaces
            .Where(x => x.TenantId == tenantId && x.Name == normalizedName && x.DeletedAt == null)
            .OrderBy(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (existing is not null) return existing;

        var now = DateTime.UtcNow;
        var space = new SmartHomeSpace
        {
            TenantId = tenantId,
            Name = normalizedName,
            SpaceType = "other",
            Summary = "由 Home Assistant 同步。",
            SortOrder = 999,
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.SmartHomeSpaces.Add(space);
        await _db.SaveChangesAsync(cancellationToken);
        return space;
    }

    private async Task UpsertCapabilitiesAsync(SmartHomeDevice device, IReadOnlyList<DiscoveredDeviceCapability> discovered, DateTime now, CancellationToken cancellationToken)
    {
        await _db.SaveChangesAsync(cancellationToken);
        var existing = await _db.DeviceCapabilities.Where(x => x.DeviceId == device.Id).ToListAsync(cancellationToken);
        foreach (var capability in discovered)
        {
            var persisted = existing.SingleOrDefault(x => x.Capability == capability.Capability);
            if (persisted is null)
            {
                _db.DeviceCapabilities.Add(new DeviceCapability
                {
                    DeviceId = device.Id,
                    Capability = capability.Capability,
                    CreatedAt = now
                });
                persisted = _db.DeviceCapabilities.Local.Last();
            }
            persisted.ValueSchema = capability.ValueSchema;
            persisted.IsWritable = capability.IsWritable;
            persisted.Permission = CapabilityPermission(device.DeviceType, capability.Capability, capability.IsWritable);
            persisted.UpdatedAt = now;
            persisted.DeletedAt = null;
        }
    }

    private static string StateSummary(DiscoveredDevice device) => device.OnlineStatus == "offline"
        ? "设备暂时离线。"
        : device.DeviceType switch
        {
            "light" => "照明状态已同步。",
            "air_conditioner" => "空调状态已同步。",
            "cover" => "遮阳设备状态已同步。",
            "switch" => "开关状态已同步。",
            _ => "环境状态已同步。"
        };

    private static string CapabilityPermission(string deviceType, string capability, bool writable) => !writable
        ? "smart_home.environment.read"
        : deviceType switch
        {
            "light" => "smart_home.light.write",
            "air_conditioner" => "smart_home.air_conditioner.write",
            "cover" => "smart_home.cover.write",
            _ => "smart_home.switch.write"
        };
}
