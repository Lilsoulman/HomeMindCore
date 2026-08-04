using HomeMind.Common.Model.ViewModel.Common;

namespace HomeMind.Business.IServices.Dashboard;

/// <summary>Returns a user-safe dashboard where each module can degrade independently.</summary>
public interface IDashboardServices
{
    Task<ServiceResult> GetAsync(long userId, long tenantId, CancellationToken cancellationToken = default);
}
