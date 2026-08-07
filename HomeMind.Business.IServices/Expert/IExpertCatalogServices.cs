using HomeMind.Common.Model.ViewModel.Common;

namespace HomeMind.Business.IServices.Expert;

/// <summary>Expert 仅作为角色、Prompt 与 Skill 策略目录，不负责执行。</summary>
public interface IExpertCatalogServices
{
    /// <summary>按租户列出可见专家与专家组目录。</summary>
    /// <param name="userId">当前用户标识。</param>
    /// <param name="tenantId">当前租户标识。</param>
    /// <param name="query">名称/编码模糊查询，可空。</param>
    /// <param name="category">分类过滤，可空。</param>
    /// <param name="type">资源类型过滤：expert/group，可空表示两者。</param>
    /// <param name="scope">专家来源过滤（B21 起）：basic=平台基础（默认，向后兼容）、mine=本人自建、all=两者；专家组恒为 basic。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>200 + <see cref="Data.AI.ExpertCatalogItemView"/> 列表。</returns>
    Task<ServiceResult> ListAsync(long userId, long tenantId, string? query, string? category, string? type, string? scope, CancellationToken cancellationToken = default);

    /// <summary>按主键获取专家或专家组详情。</summary>
    /// <param name="userId">当前用户标识。</param>
    /// <param name="tenantId">当前租户标识。</param>
    /// <param name="expertId">专家或专家组主键。</param>
    /// <param name="type">资源类型：expert/group。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>200 + <see cref="Data.AI.ExpertDetailView"/>；跨租户/他人自建/已软删 404。</returns>
    Task<ServiceResult> GetAsync(long userId, long tenantId, long expertId, string type, CancellationToken cancellationToken = default);
}
