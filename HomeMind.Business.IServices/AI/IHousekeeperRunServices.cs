using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.AI;

namespace HomeMind.Business.IServices.AI;

/// <summary>家庭管家编排与已确认设备行动执行。</summary>
public interface IHousekeeperRunServices
{
    Task<ServiceResult> CreateAsync(long userId, long tenantId, HousekeeperRunRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult> GetActionsAsync(long userId, long tenantId, long runId, CancellationToken cancellationToken = default);
    Task<ServiceResult> ConfirmActionAsync(long userId, long tenantId, long runId, long actionId, ConfirmHousekeeperActionRequest request, CancellationToken cancellationToken = default);
}
