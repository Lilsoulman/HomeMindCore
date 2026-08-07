using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.AI;

namespace HomeMind.Business.IServices.Expert;

/// <summary>
/// 用户自建专家服务：创建/更新/软删除仅本人自建专家（<c>experts.owner_user_id=本人</c>）。
/// 创建自动生成 <c>custom-</c> 前缀编码与 v1 已发布版本；更新生成 version+1 已发布版本（版本不可变不变量）；
/// 删除为软删除（deleted_at），已删专家从目录、运行解析与会话发送全部消失。不写家庭审计（设计 §13.1）。
/// </summary>
public interface IExpertSelfServeServices
{
    /// <summary>创建自建专家（本人租户、owner=本人、custom/active），并生成 v1 已发布版本。</summary>
    /// <param name="userId">当前用户标识。</param>
    /// <param name="tenantId">当前租户标识。</param>
    /// <param name="request">创建请求。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>201 + <see cref="ExpertDetailView"/>（Source=mine）；缺字段/非法 ToolPolicyJson 422。</returns>
    Task<ServiceResult> CreateAsync(long userId, long tenantId, ExpertCreateRequest request, CancellationToken cancellationToken = default);

    /// <summary>更新本人自建专家：替换头部字段并生成 version+1 已发布版本；乐观锁冲突 409。</summary>
    /// <param name="userId">当前用户标识。</param>
    /// <param name="tenantId">当前租户标识。</param>
    /// <param name="expertId">自建专家主键。</param>
    /// <param name="request">更新请求，携带 RowVersion。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>200 + <see cref="ExpertDetailView"/>（最新版本）；非本人/已软删 404，RowVersion 不符 409/40903。</returns>
    Task<ServiceResult> UpdateAsync(long userId, long tenantId, long expertId, ExpertUpdateRequest request, CancellationToken cancellationToken = default);

    /// <summary>软删除本人自建专家。</summary>
    /// <param name="userId">当前用户标识。</param>
    /// <param name="tenantId">当前租户标识。</param>
    /// <param name="expertId">自建专家主键。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>200；非本人/已软删 404。</returns>
    Task<ServiceResult> DeleteAsync(long userId, long tenantId, long expertId, CancellationToken cancellationToken = default);
}
