using HomeMind.Business.IServices.Connector;
using HomeMind.Business.IServices.SmartHome;
using HomeMind.Business.Services.Connectors.Adapters;
using HomeMind.Business.Services.Connectors.Bridge;
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
    private readonly IReadOnlyDictionary<string, IDeviceAdapter> _adapters;
    private readonly DeviceSyncService _sync;
    private readonly IConnectorSyncQueue _syncQueue;

    public ConnectorRuntimeServices(HomeMindDbContext db, IEnumerable<IDeviceAdapter> adapters, DeviceSyncService sync, IConnectorSyncQueue syncQueue)
    {
        _db = db;
        _adapters = adapters.ToDictionary(x => x.ProviderCode, StringComparer.OrdinalIgnoreCase);
        _sync = sync;
        _syncQueue = syncQueue;
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

        int discoveredCount;
        try
        {
            discoveredCount = await _sync.SyncAsync(tenantId, connector, context.ProviderCode!, context.Reference!, cancellationToken);
        }
        catch (ConnectorAdapterException error)
        {
            await _sync.MarkFailedAsync(connector, cancellationToken);
            return new ServiceResult(IsVaultError(error.ErrorCode) ? 503 : 502, error.Message);
        }

        return new ServiceResult(200, successMessage, new ConnectorOperationView(connector.Id, connector.Status, discoveredCount, connector.LastHealthAt, connector.LastSyncAt));
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
        return new RuntimeContext(connector.Connector, adapter, connector.Code, new ConnectorReference(connector.Connector.Id, tenantId, connector.Connector.CredentialRef), null);
    }

    private static bool IsVaultError(string? errorCode) => errorCode?.StartsWith("secret_vault", StringComparison.Ordinal) == true || errorCode == "invalid_secret";

    private static ConnectorSyncJobView ToSyncJobView(ConnectorSyncJob job) => new(job.Id, job.WorkspaceConnectorId, job.Status, job.Reason, job.AttemptNo, job.AvailableAt, job.CompletedAt, job.UpdatedAt);

    private sealed record RuntimeContext(WorkspaceConnector? Connector, IDeviceAdapter? Adapter, string? ProviderCode, ConnectorReference? Reference, ServiceResult? Error)
    {
        public static RuntimeContext Failure(ServiceResult error) => new(null, null, null, null, error);
    }
}
