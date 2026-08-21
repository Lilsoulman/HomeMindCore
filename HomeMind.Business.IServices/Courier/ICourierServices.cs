using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.Courier;

namespace HomeMind.Business.IServices.Courier;

/// <summary>个人快递登记、状态同步与异常建议服务契约。</summary>
public interface ICourierServices
{
    /// <summary>登记当前用户的个人运单。</summary>
    Task<ServiceResult> CreateAsync(long homeId, long ownerUserId, CourierShipmentCreateRequest request, CancellationToken cancellationToken = default);
    /// <summary>列出当前用户在家庭中的个人运单。</summary>
    Task<ServiceResult> ListAsync(long homeId, long ownerUserId, CancellationToken cancellationToken = default);
    /// <summary>经快递100 MCP 刷新运单状态并生成异常建议。</summary>
    Task<ServiceResult> RefreshAsync(long homeId, long ownerUserId, long shipmentId, CancellationToken cancellationToken = default);
    /// <summary>列出当前用户尚未处理的异常建议。</summary>
    Task<ServiceResult> ListAnomaliesAsync(long homeId, long ownerUserId, CancellationToken cancellationToken = default);
}
