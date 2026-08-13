using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.Media;

namespace HomeMind.Business.IServices.Media;

/// <summary>V2.8 剪辑任务持久化服务。</summary>
public interface IClippingTaskServices
{
    Task<ServiceResult> GetAsync(long userId, long tenantId, long taskId, CancellationToken cancellationToken = default);
}
