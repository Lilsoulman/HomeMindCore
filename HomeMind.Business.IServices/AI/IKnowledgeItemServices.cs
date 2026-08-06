using HomeMind.Common.Model.ViewModel.Common;

namespace HomeMind.Business.IServices.AI;

/// <summary>每日知识条目的租户隔离 CRUD，供知识管家取用与用户主动录入。</summary>
public interface IKnowledgeItemServices
{
    Task<ServiceResult> ListAsync(long userId, long tenantId, string? category, CancellationToken cancellationToken = default);
    Task<ServiceResult> CreateAsync(long userId, long tenantId, KnowledgeItemRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult> DeleteAsync(long userId, long tenantId, long id, CancellationToken cancellationToken = default);
}

/// <summary>知识条目创建请求。</summary>
/// <param name="Category">知识分类，如 yunhe_tcm / management / general。</param>
/// <param name="Title">知识标题。</param>
/// <param name="Content">知识正文。</param>
/// <param name="Source">来源说明，可空。</param>
public sealed record KnowledgeItemRequest(string Category, string Title, string Content, string? Source);
