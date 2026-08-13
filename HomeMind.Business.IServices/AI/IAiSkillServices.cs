using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.AI;

namespace HomeMind.Business.IServices.AI;

/// <summary>AI 技能业务服务约定。</summary>
public interface IAiSkillServices
{
    Task<ServiceResult> ListAsync(long userId, long tenantId, CancellationToken cancellationToken = default);
    Task<ServiceResult> ListPlatformAsync(long tenantId, string role, CancellationToken cancellationToken = default);
    Task<ServiceResult> ListAllAsync(long tenantId, string role, CancellationToken cancellationToken = default);
    Task<ServiceResult> CreateAsync(long userId, long tenantId, SkillRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult> UpdateAsync(long userId, long tenantId, long id, SkillRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult> DeleteAsync(long userId, long tenantId, long id, CancellationToken cancellationToken = default);
}
