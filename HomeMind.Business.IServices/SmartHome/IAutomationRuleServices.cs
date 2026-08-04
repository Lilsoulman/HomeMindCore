using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.SmartHome;

namespace HomeMind.Business.IServices.SmartHome;

public interface IAutomationRuleServices
{
    Task<ServiceResult> ListAsync(long tenantId, CancellationToken cancellationToken = default);
    Task<ServiceResult> CreateAsync(long userId, long tenantId, AutomationRuleRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult> UpdateAsync(long userId, long tenantId, long ruleId, UpdateAutomationRuleRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult> HandleDeviceStateChangeAsync(long tenantId, long deviceId, string state, DateTime occurredAt, CancellationToken cancellationToken = default);
    Task<ServiceResult> HandleSceneCompletedAsync(long tenantId, string sceneKey, DateTime occurredAt, CancellationToken cancellationToken = default);
    Task<ServiceResult> HandleSyncCompletedAsync(long tenantId, long connectorId, DateTime occurredAt, CancellationToken cancellationToken = default);
    Task<int> ProcessDueSchedulesAsync(DateTime now, CancellationToken cancellationToken = default);
}
