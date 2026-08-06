using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.AI;

namespace HomeMind.Business.IServices.AI;

/// <summary>AI 配置业务服务约定；配置按用户隔离，密钥只存密文。</summary>
public interface IAiConfigServices
{
    Task<ServiceResult> GetAsync(long userId, CancellationToken cancellationToken = default);
    Task<ServiceResult> SaveAsync(long userId, AiConfigRequest request, CancellationToken cancellationToken = default);
}
