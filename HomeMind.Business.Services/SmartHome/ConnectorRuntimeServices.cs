using HomeMind.Business.IServices.SmartHome;
using HomeMind.Common.Model.Entities.SmartHome;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.SmartHome;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;

namespace HomeMind.Business.Services.SmartHome;

/// <summary>协调连接测试、发现和轮询同步；仅持久化协议无关的设备读模型。</summary>
public sealed class ConnectorRuntimeServices : IConnectorRuntimeServices
{
    private readonly HomeMindDbContext _db;
    private readonly IReadOnlyDictionary<string, IConnectorAdapter> _adapters;
    private readonly IConnectorSyncQueue _syncQueue;
    private readonly IAutomationRuleServices _automation;

    public ConnectorRuntimeServices(HomeMindDbContext db, IEnumerable<IConnectorAdapter> adapters, IConnectorSyncQueue syncQueue, IAutomationRuleServices automation)
    {
        _db = db;
        _adapters = adapters.ToDictionary(x => x.ProviderCode, StringComparer.OrdinalIgnoreCase);
        _syncQueue = syncQueue;
        _automation = automation;
    }

    public async Task<ServiceResult> TestConnectionAsync(long tenantId, long connectorId, CancellationToken cancellationToken = default)
    {
        var context = await LoadAsync(tenantId, connectorId, cancellationToken);
        if (context.Error is not null) return context.Error;

        var result = await context.Adapter!.TestConnectionAsync(context.Reference!, cancellationToken);
        var now = DateTime.UtcNow;
        context.Connector!.LastHealthAt = now;
        context.Connector.Status = result.Succeeded ? "connected" : "failed";
        context.Connector.UpdatedAt = now;
        await _db.SaveChangesAsync(cancellationToken);

        var view = new ConnectorOperationView(context.Connector.Id, context.Connector.Status, 0, context.Connector.LastHealthAt, context.Connector.LastSyncAt);
        return result.Succeeded
            ? new ServiceResult(200, "Home Assistant 连接测试成功。", view)
            : new ServiceResult(IsVaultError(result.ErrorCode) ? 503 : 502, result.Message ?? "无法连接 Home Assistant。", view);
    }

    public Task<ServiceResult> DiscoverDevicesAsync(long tenantId, long connectorId, CancellationToken cancellationToken = default) =>
        SynchronizeAsync(tenantId, connectorId, "设备发现完成。", cancellationToken);

    public async Task<ServiceResult> SyncStatesAsync(long tenantId, long connectorId, CancellationToken cancellationToken = default)
    {
        if (!await _db.WorkspaceConnectors.AnyAsync(x => x.Id == connectorId && x.TenantId == tenantId && x.DeletedAt == null, cancellationToken))
            return new ServiceResult(404, "请求的连接器不存在或已停用。");
        var now = DateTime.UtcNow;
        var job = new ConnectorSyncJob
        {
            TenantId = tenantId,
            WorkspaceConnectorId = connectorId,
            Reason = "manual",
            IdempotencyKey = Guid.NewGuid().ToString(),
            AvailableAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.ConnectorSyncJobs.Add(job);
        await _db.SaveChangesAsync(cancellationToken);
        await _syncQueue.EnqueueAsync(job.Id, cancellationToken);
        AutomationMetrics.SyncQueued.Add(1);
        return new ServiceResult(202, "设备状态同步已排队。", ToSyncJobView(job));
    }

    public async Task<ServiceResult> GetSyncJobAsync(long tenantId, long jobId, CancellationToken cancellationToken = default)
    {
        var job = await _db.ConnectorSyncJobs.SingleOrDefaultAsync(x => x.Id == jobId && x.TenantId == tenantId, cancellationToken);
        return job is null ? new ServiceResult(404, "请求的同步任务不存在。") : new ServiceResult(200, "查询成功。", ToSyncJobView(job));
    }

    public async Task ProcessSyncJobAsync(long jobId, CancellationToken cancellationToken = default)
    {
        var job = await _db.ConnectorSyncJobs.SingleOrDefaultAsync(x => x.Id == jobId, cancellationToken);
        if (job is null || job.Status != "queued" || job.AvailableAt > DateTime.UtcNow) return;
        job.Status = "running";
        job.AttemptNo++;
        job.StartedAt = job.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        ServiceResult result;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));
            result = await SynchronizeAsync(job.TenantId, job.WorkspaceConnectorId, "设备状态同步完成。", timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            result = new ServiceResult(504, "设备状态同步超时。");
        }
        catch (Exception)
        {
            result = new ServiceResult(502, "设备状态同步失败。");
        }

        var now = DateTime.UtcNow;
        if (result.Succeeded)
        {
            job.Status = "completed";
            job.CompletedAt = now;
            job.LastErrorCode = null;
        }
        else if (job.AttemptNo < 3 && result.StatusCode >= 500)
        {
            job.Status = "queued";
            job.AvailableAt = now.AddSeconds(Math.Pow(2, job.AttemptNo) * 5);
            job.LastErrorCode = result.StatusCode == 504 ? "timeout" : "connector_unavailable";
            await _syncQueue.EnqueueAsync(job.Id, cancellationToken);
            AutomationMetrics.SyncRetried.Add(1);
        }
        else
        {
            job.Status = "failed";
            job.CompletedAt = now;
            job.LastErrorCode = result.StatusCode == 504 ? "timeout" : "sync_failed";
            AutomationMetrics.SyncFailed.Add(1);
        }
        job.UpdatedAt = now;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task ProcessDueSyncJobsAsync(CancellationToken cancellationToken = default)
    {
        var dueIds = await _db.ConnectorSyncJobs.Where(x => x.Status == "queued" && x.AvailableAt <= DateTime.UtcNow)
            .OrderBy(x => x.AvailableAt).Select(x => x.Id).Take(10).ToArrayAsync(cancellationToken);
        foreach (var jobId in dueIds) await ProcessSyncJobAsync(jobId, cancellationToken);
    }

    private async Task<ServiceResult> SynchronizeAsync(long tenantId, long connectorId, string successMessage, CancellationToken cancellationToken)
    {
        var context = await LoadAsync(tenantId, connectorId, cancellationToken);
        if (context.Error is not null) return context.Error;
        var connector = context.Connector!;

        IReadOnlyList<DiscoveredDevice> discovered;
        try
        {
            discovered = await context.Adapter!.DiscoverDevicesAsync(context.Reference!, cancellationToken);
        }
        catch (ConnectorAdapterException error)
        {
            await MarkFailedAsync(connector, cancellationToken);
            return new ServiceResult(IsVaultError(error.ErrorCode) ? 503 : 502, error.Message);
        }

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
        return new ServiceResult(200, successMessage, new ConnectorOperationView(connector.Id, connector.Status, discovered.Count, connector.LastHealthAt, connector.LastSyncAt));
    }

    private async Task<RuntimeContext> LoadAsync(long tenantId, long connectorId, CancellationToken cancellationToken)
    {
        var connector = await (from instance in _db.WorkspaceConnectors
                               join provider in _db.ConnectorProviders on instance.ConnectorProviderId equals provider.Id
                               where instance.Id == connectorId && instance.TenantId == tenantId && instance.DeletedAt == null
                                     && provider.DeletedAt == null && provider.Status == "active"
                               select new { Connector = instance, provider.Code })
            .SingleOrDefaultAsync(cancellationToken);
        if (connector is null) return RuntimeContext.Failure(new ServiceResult(404, "请求的连接器不存在或已停用。"));
        if (string.IsNullOrWhiteSpace(connector.Connector.CredentialRef)) return RuntimeContext.Failure(new ServiceResult(422, "连接器未配置凭据引用。"));
        if (!_adapters.TryGetValue(connector.Code, out var adapter)) return RuntimeContext.Failure(new ServiceResult(501, "该连接器尚未提供运行期适配器。"));
        return new RuntimeContext(connector.Connector, adapter, new ConnectorReference(connector.Connector.Id, tenantId, connector.Connector.CredentialRef), null);
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

    private async Task MarkFailedAsync(WorkspaceConnector connector, CancellationToken cancellationToken)
    {
        connector.Status = "failed";
        connector.LastHealthAt = DateTime.UtcNow;
        connector.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
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

    private static bool IsVaultError(string? errorCode) => errorCode?.StartsWith("secret_vault", StringComparison.Ordinal) == true || errorCode == "invalid_secret";

    private static ConnectorSyncJobView ToSyncJobView(ConnectorSyncJob job) => new(job.Id, job.WorkspaceConnectorId, job.Status, job.Reason, job.AttemptNo, job.AvailableAt, job.CompletedAt, job.UpdatedAt);

    private sealed record RuntimeContext(WorkspaceConnector? Connector, IConnectorAdapter? Adapter, ConnectorReference? Reference, ServiceResult? Error)
    {
        public static RuntimeContext Failure(ServiceResult error) => new(null, null, null, error);
    }
}
