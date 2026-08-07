using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.Connectors;
using HomeMind.Common.Model.ViewModel.Data.SmartHome;

namespace HomeMind.Business.IServices.SmartHome;

public interface IConnectorServices
{
    Task<ServiceResult> ListProvidersAsync(CancellationToken cancellationToken = default);
    Task<ServiceResult> ListConnectorsAsync(long userId, long tenantId, bool canManage, CancellationToken cancellationToken = default);
    Task<ServiceResult> CreateConnectorAsync(long userId, long tenantId, CreateConnectorRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult> GetMyAuthorizationAsync(long userId, long tenantId, long connectorId, CancellationToken cancellationToken = default);
    Task<ServiceResult> UpdateAuthorizationAsync(long tenantId, long connectorId, long memberUserId, ConnectorAuthorizationRequest request, CancellationToken cancellationToken = default);

    /// <summary>汇总当前用户在当前家庭的所有 personal Connector 及最近一次授权会话状态。</summary>
    /// <param name="userId">当前用户主键。</param>
    /// <param name="tenantId">当前家庭（租户）主键，来自 JWT。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>仅返回当前用户作为 owner 的 personal 实例；不返回凭据引用或 owner 标识。</returns>
    Task<ServiceResult> ListMyPersonalConnectionsAsync(long userId, long tenantId, CancellationToken cancellationToken = default);
}
