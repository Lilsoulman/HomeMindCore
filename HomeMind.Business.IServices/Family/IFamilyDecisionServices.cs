using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.Family;

namespace HomeMind.Business.IServices.Family;

/// <summary>家庭决策历史服务契约；仅追加记录，不可修改或删除。</summary>
public interface IFamilyDecisionServices
{
    /// <summary>列出指定家庭的决策历史，支持游标分页。</summary>
    /// <param name="homeId">目标家庭主键。</param>
    /// <param name="memberId">可选成员过滤。</param>
    /// <param name="limit">每页条数，上限 50。</param>
    /// <param name="cursor">分页游标（base64 编码的复合键），首次请求不传。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>决策列表统一响应。</returns>
    Task<ServiceResult> ListAsync(long homeId, long? memberId, int limit, string? cursor, CancellationToken cancellationToken = default);

    /// <summary>记录一条家庭决策，写入审计。</summary>
    /// <param name="homeId">目标家庭主键。</param>
    /// <param name="actorUserId">操作者用户标识。</param>
    /// <param name="request">决策写入请求体。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>创建成功返回 201 与决策视图。</returns>
    Task<ServiceResult> RecordAsync(long homeId, long actorUserId, FamilyDecisionWriteRequest request, CancellationToken cancellationToken = default);
}
