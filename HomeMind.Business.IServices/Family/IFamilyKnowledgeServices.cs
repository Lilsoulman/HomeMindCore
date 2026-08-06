using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.Family;

namespace HomeMind.Business.IServices.Family;

/// <summary>家庭知识服务契约；负责知识条的增删查，并在写入时按 latest/authority/majority 解决同 key 冲突、写入审计。</summary>
public interface IFamilyKnowledgeServices
{
    /// <summary>列出指定家庭下未删除的知识条；可按分类过滤。</summary>
    /// <param name="homeId">目标家庭主键。</param>
    /// <param name="category">可选的知识分类过滤。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>知识列表统一响应。</returns>
    Task<ServiceResult> ListAsync(long homeId, string? category, CancellationToken cancellationToken = default);

    /// <summary>写入一条知识；若同 home_id+category+knowledge_key 存在未删除行，则按冲突解决策略留痕。</summary>
    /// <param name="homeId">目标家庭主键。</param>
    /// <param name="actorUserId">操作者用户标识。</param>
    /// <param name="request">写入请求体。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>写入成功返回 201 + 知识视图与冲突解决摘要。</returns>
    /// <exception cref="InvalidOperationException">当 source_type 与 source_member_id 不满足 CHECK 时抛出。</exception>
    Task<ServiceResult> WriteAsync(long homeId, long actorUserId, FamilyKnowledgeWriteRequest request, CancellationToken cancellationToken = default);

    /// <summary>软删除一条知识（写 deleted_at），并审计。</summary>
    /// <param name="homeId">目标家庭主键。</param>
    /// <param name="actorUserId">操作者用户标识。</param>
    /// <param name="knowledgeId">目标知识主键。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>删除成功返回 200；不存在返回 404。</returns>
    Task<ServiceResult> DeleteAsync(long homeId, long actorUserId, long knowledgeId, CancellationToken cancellationToken = default);
}
