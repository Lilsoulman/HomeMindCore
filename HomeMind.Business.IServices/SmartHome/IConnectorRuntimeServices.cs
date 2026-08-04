using HomeMind.Common.Model.ViewModel.Common;

namespace HomeMind.Business.IServices.SmartHome;

public interface IConnectorRuntimeServices
{
    Task<ServiceResult> TestConnectionAsync(long tenantId, long connectorId, CancellationToken cancellationToken = default);
    Task<ServiceResult> DiscoverDevicesAsync(long tenantId, long connectorId, CancellationToken cancellationToken = default);
    Task<ServiceResult> SyncStatesAsync(long tenantId, long connectorId, CancellationToken cancellationToken = default);
    Task<ServiceResult> GetSyncJobAsync(long tenantId, long jobId, CancellationToken cancellationToken = default);
    Task ProcessSyncJobAsync(long jobId, CancellationToken cancellationToken = default);
    Task ProcessDueSyncJobsAsync(CancellationToken cancellationToken = default);
}
