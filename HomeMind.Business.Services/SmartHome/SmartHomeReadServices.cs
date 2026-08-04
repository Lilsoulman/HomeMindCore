using HomeMind.Business.IServices.SmartHome;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.SmartHome;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HomeMind.Business.Services.SmartHome;

/// <summary>返回租户的标准化家庭读模型；Mock 仅在显式开发配置下启用。</summary>
public sealed class SmartHomeReadServices : ISmartHomeReadServices
{
    private readonly HomeMindDbContext _db;
    private readonly bool _mockEnabled;

    public SmartHomeReadServices(HomeMindDbContext db, IConfiguration configuration)
    {
        _db = db;
        _mockEnabled = bool.TryParse(configuration["SmartHome:MockEnabled"], out var enabled) && enabled;
    }

    public async Task<ServiceResult> ListSpacesAsync(long tenantId, CancellationToken cancellationToken = default)
    {
        var spaces = await _db.SmartHomeSpaces
            .Where(x => x.TenantId == tenantId && x.DeletedAt == null)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);
        if (spaces.Count == 0 && _mockEnabled) return new ServiceResult(200, "查询成功。", MockSpaces());

        var counts = await _db.SmartHomeDevices
            .Where(x => x.TenantId == tenantId && x.DeletedAt == null && x.SpaceId != null)
            .GroupBy(x => x.SpaceId!.Value)
            .Select(x => new { SpaceId = x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.SpaceId, x => x.Count, cancellationToken);
        return new ServiceResult(200, "查询成功。", spaces.Select(x => new SmartHomeSpaceView(x.Id, x.Name, x.SpaceType, x.Summary, counts.GetValueOrDefault(x.Id), x.UpdatedAt)));
    }

    public async Task<ServiceResult> ListDevicesAsync(long tenantId, long? spaceId, CancellationToken cancellationToken = default)
    {
        var query = _db.SmartHomeDevices.Where(x => x.TenantId == tenantId && x.DeletedAt == null);
        if (spaceId is not null) query = query.Where(x => x.SpaceId == spaceId);
        var devices = await query.OrderBy(x => x.Name).ToListAsync(cancellationToken);
        if (devices.Count == 0 && _mockEnabled) return new ServiceResult(200, "查询成功。", MockDevices(spaceId));

        var deviceIds = devices.Select(x => x.Id).ToArray();
        var capabilities = await _db.DeviceCapabilities
            .Where(x => deviceIds.Contains(x.DeviceId) && x.DeletedAt == null)
            .OrderBy(x => x.Capability)
            .ToListAsync(cancellationToken);
        var latestStates = await _db.DeviceStates
            .Where(x => deviceIds.Contains(x.DeviceId))
            .GroupBy(x => x.DeviceId)
            .Select(x => new { DeviceId = x.Key, SampledAt = x.Max(s => s.SampledAt) })
            .ToDictionaryAsync(x => x.DeviceId, x => x.SampledAt, cancellationToken);
        return new ServiceResult(200, "查询成功。", devices.Select(device => new SmartHomeDeviceView(
            device.Id,
            device.SpaceId,
            device.Name,
            device.DeviceType,
            device.OnlineStatus,
            device.StateSummary,
            latestStates.GetValueOrDefault(device.Id),
            capabilities.Where(x => x.DeviceId == device.Id).Select(x => new DeviceCapabilityView(x.Capability, x.ValueSchema, x.Permission, x.IsWritable)).ToArray())));
    }

    public async Task<ServiceResult> ListScenesAsync(long tenantId, CancellationToken cancellationToken = default)
    {
        var scenes = await _db.Scenes
            .Where(x => x.TenantId == tenantId && x.DeletedAt == null && x.Status == "active")
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
        if (scenes.Count == 0 && _mockEnabled) return new ServiceResult(200, "查询成功。", MockScenes());
        return new ServiceResult(200, "查询成功。", scenes.Select(x => new SceneView(x.Id, x.SceneKey, x.Name, x.Summary, x.Status, x.UpdatedAt)));
    }

    private static IReadOnlyList<SmartHomeSpaceView> MockSpaces() =>
    [
        new(-101, "客厅", "living_room", "环境舒适，主灯已开启。", 2, DateTime.UtcNow),
        new(-102, "卧室", "bedroom", "睡眠模式待确认。", 1, DateTime.UtcNow),
        new(-103, "老人房", "elder_room", "设备状态正常。", 1, DateTime.UtcNow)
    ];

    private static IReadOnlyList<SmartHomeDeviceView> MockDevices(long? spaceId)
    {
        var all = new[]
        {
            new SmartHomeDeviceView(-201, -101, "客厅主灯", "light", "online", "已开启，亮度 60%。", DateTime.UtcNow, [new DeviceCapabilityView("power", "{\"type\":\"boolean\"}", "smart_home.light.write", true), new DeviceCapabilityView("brightness", "{\"type\":\"number\"}", "smart_home.light.write", true)]),
            new SmartHomeDeviceView(-202, -101, "客厅温湿度传感器", "sensor", "online", "温度 25 C，湿度 48%。", DateTime.UtcNow, [new DeviceCapabilityView("temperature", "{\"type\":\"number\"}", "smart_home.environment.read", false), new DeviceCapabilityView("humidity", "{\"type\":\"number\"}", "smart_home.environment.read", false)]),
            new SmartHomeDeviceView(-203, -102, "卧室空调", "air_conditioner", "online", "26 C，睡眠模式未开启。", DateTime.UtcNow, [new DeviceCapabilityView("power", "{\"type\":\"boolean\"}", "smart_home.air_conditioner.write", true), new DeviceCapabilityView("temperature", "{\"type\":\"number\"}", "smart_home.air_conditioner.write", true)]),
            new SmartHomeDeviceView(-204, -103, "老人房照明", "light", "online", "已关闭。", DateTime.UtcNow, [new DeviceCapabilityView("power", "{\"type\":\"boolean\"}", "smart_home.light.write", true)])
        };
        return spaceId is null ? all : all.Where(x => x.SpaceId == spaceId).ToArray();
    }

    private static IReadOnlyList<SceneView> MockScenes() =>
    [
        new(-301, "arrive_home", "回家", "恢复舒适照明与环境。", "active", DateTime.UtcNow),
        new(-302, "leave_home", "离家", "关闭非必要设备并检查安全状态。", "active", DateTime.UtcNow),
        new(-303, "sleep", "睡眠", "调整卧室灯光和空调。", "active", DateTime.UtcNow)
    ];
}
