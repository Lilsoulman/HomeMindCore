using HomeMind.Common.Model.ViewModel.Common;

namespace HomeMind.Business.IServices.Memory;

/// <summary>学习记忆库只读查询服务契约。</summary>
public interface ILearningMemoryServices
{
    /// <summary>按可见性、类型、状态和关键词游标分页查询学习记忆。</summary>
    Task<ServiceResult> ListAsync(long homeId, long actorUserId, string? scope, string? kind, string? status, string? query, int limit, string? cursor, CancellationToken cancellationToken = default);

    /// <summary>读取一条当前用户可见的学习记忆详情。</summary>
    Task<ServiceResult> GetAsync(long homeId, long actorUserId, long memoryId, CancellationToken cancellationToken = default);
}
