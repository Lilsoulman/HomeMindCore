using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.Memory;

namespace HomeMind.Business.IServices.Memory;

/// <summary>记忆候选审核服务契约。</summary>
public interface IMemoryCandidateServices
{
    /// <summary>按当前用户可见范围列出待审核候选。</summary>
    Task<ServiceResult> ListAsync(long homeId, long actorUserId, string? scope, string? status, CancellationToken cancellationToken = default);

    /// <summary>接受候选并在同一事务写入对应事实源与学习投影。</summary>
    Task<ServiceResult> AcceptAsync(long homeId, long actorUserId, long candidateId, ResolveMemoryCandidateRequest request, CancellationToken cancellationToken = default);

    /// <summary>拒绝仍处于待审核状态的候选。</summary>
    Task<ServiceResult> RejectAsync(long homeId, long actorUserId, long candidateId, CancellationToken cancellationToken = default);
}
