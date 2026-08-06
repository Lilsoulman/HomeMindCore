using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.Family;

namespace HomeMind.Business.IServices.Family;

/// <summary>家庭成员服务契约；负责 CRUD 与状态机流转（active ↔ away 双向，终态更正需审计）。</summary>
public interface IFamilyMemberServices
{
    /// <summary>列出指定家庭下所有未删除的成员。</summary>
    /// <param name="homeId">目标家庭主键，由 Controller 的 RequireHomeOwner 校验。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>成员列表统一响应。</returns>
    Task<ServiceResult> ListAsync(long homeId, CancellationToken cancellationToken = default);

    /// <summary>创建一名新成员；默认状态为 <see cref="FamilyMemberStatus.Active"/>。</summary>
    /// <param name="homeId">目标家庭主键。</param>
    /// <param name="actorUserId">操作者用户标识。</param>
    /// <param name="request">创建请求体。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>创建成功返回 201 与新视图；校验失败返回 422。</returns>
    Task<ServiceResult> CreateAsync(long homeId, long actorUserId, FamilyMemberCreateRequest request, CancellationToken cancellationToken = default);

    /// <summary>部分更新成员信息；仅允许在 active 与 away 之间切换。</summary>
    /// <param name="homeId">目标家庭主键。</param>
    /// <param name="actorUserId">操作者用户标识。</param>
    /// <param name="memberId">目标成员主键。</param>
    /// <param name="request">更新请求体。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>更新成功返回 200；成员不存在返回 404；状态非法返回 422。</returns>
    Task<ServiceResult> UpdateAsync(long homeId, long actorUserId, long memberId, FamilyMemberUpdateRequest request, CancellationToken cancellationToken = default);

    /// <summary>成员终态更正或恢复；任何进入/退出终态的操作均需在同一事务内写入终端三字段并审计。</summary>
    /// <param name="homeId">目标家庭主键。</param>
    /// <param name="actorUserId">操作者用户标识。</param>
    /// <param name="memberId">目标成员主键。</param>
    /// <param name="request">更正请求体，含目标状态与原因。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>更正成功返回 200 与新视图；成员不存在返回 404；状态非法返回 422。</returns>
    Task<ServiceResult> CorrectAsync(long homeId, long actorUserId, long memberId, FamilyMemberCorrectionRequest request, CancellationToken cancellationToken = default);
}
