using HomeMind.Common.Model.ViewModel.Common;

namespace HomeMind.Business.IServices.Expert;

/// <summary>Expert 仅作为角色、Prompt 与 Skill 策略目录，不负责执行。</summary>
public interface IExpertCatalogServices
{
    Task<ServiceResult> ListAsync(long userId, long tenantId, string? query, string? category, string? type, CancellationToken cancellationToken = default);
    Task<ServiceResult> GetAsync(long userId, long tenantId, long expertId, string type, CancellationToken cancellationToken = default);
}
