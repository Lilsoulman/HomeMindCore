using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.SmartHome;

namespace HomeMind.Business.IServices.SmartHome;

public interface IConnectorServices
{
    Task<ServiceResult> ListProvidersAsync(CancellationToken cancellationToken = default);
    Task<ServiceResult> ListConnectorsAsync(long userId, long tenantId, bool canManage, CancellationToken cancellationToken = default);
    Task<ServiceResult> CreateConnectorAsync(long userId, long tenantId, CreateConnectorRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult> GetMyAuthorizationAsync(long userId, long tenantId, long connectorId, CancellationToken cancellationToken = default);
    Task<ServiceResult> UpdateAuthorizationAsync(long tenantId, long connectorId, long memberUserId, ConnectorAuthorizationRequest request, CancellationToken cancellationToken = default);
}
