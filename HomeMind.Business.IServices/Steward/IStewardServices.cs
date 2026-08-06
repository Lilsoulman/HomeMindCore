using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.Steward;

namespace HomeMind.Business.IServices.Steward;

/// <summary>
/// 管家动态与确认中心服务契约；复用 B9 实体（<c>steward_activities</c>、<c>confirmation_items</c>），不重定义状态机。
/// 所有确认、拒绝、批量确认与撤销均写入家庭审计日志与可展示的管家动态。
/// </summary>
public interface IStewardServices
{
    /// <summary>按家庭游标分页列出管家动态（created_at + id 复合游标，倒序）。</summary>
    /// <param name="homeId">目标家庭主键，由 Controller 的 RequireHomeOwner 校验。</param>
    /// <param name="limit">每页条数，默认 20，上限 50。</param>
    /// <param name="cursor">分页游标，首次请求不传。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>查询成功返回 200 与 <c>{ Items, Cursor }</c> 分页数据。</returns>
    Task<ServiceResult> ListActivitiesAsync(long homeId, int limit, string? cursor, CancellationToken cancellationToken = default);

    /// <summary>获取单条管家动态详情。</summary>
    /// <param name="homeId">目标家庭主键。</param>
    /// <param name="activityId">目标活动主键。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>查询成功返回 200 与活动视图；不存在返回 404。</returns>
    Task<ServiceResult> GetActivityAsync(long homeId, long activityId, CancellationToken cancellationToken = default);

    /// <summary>撤销可撤销的已完成管家动态；同一事务内写入 <c>activity_undo</c> 审计并置位撤销时间。</summary>
    /// <param name="homeId">目标家庭主键。</param>
    /// <param name="actorUserId">操作者用户标识。</param>
    /// <param name="activityId">目标活动主键。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>撤销成功返回 200；不存在返回 404；非已完成或不可撤销返回 422；已撤销返回 409。</returns>
    Task<ServiceResult> UndoActivityAsync(long homeId, long actorUserId, long activityId, CancellationToken cancellationToken = default);

    /// <summary>列出确认项，支持风险等级与状态过滤；过期项按计算语义不返回。</summary>
    /// <param name="homeId">目标家庭主键。</param>
    /// <param name="riskLevel">风险等级过滤：L1/L2/L3。</param>
    /// <param name="status">状态过滤：pending/confirmed/denied/expired/cancelled。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>查询成功返回 200 与确认项视图列表；过滤参数非法返回 422。</returns>
    Task<ServiceResult> ListConfirmationsAsync(long homeId, string? riskLevel, string? status, CancellationToken cancellationToken = default);

    /// <summary>单项确认确认项（L2/L3 逐项，L1 亦可）；同一事务内复验归属与资源状态，幂等键仅校验格式。</summary>
    /// <param name="homeId">目标家庭主键。</param>
    /// <param name="actorUserId">操作者用户标识。</param>
    /// <param name="confirmationId">目标确认项主键。</param>
    /// <param name="request">确认请求体，含幂等键。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>确认成功返回 200；不存在返回 404；幂等键非法返回 422；已确认重放返回 200；已终态或过期返回 409。</returns>
    Task<ServiceResult> ConfirmAsync(long homeId, long actorUserId, long confirmationId, ConfirmationConfirmRequest request, CancellationToken cancellationToken = default);

    /// <summary>拒绝确认项；原因必填，同一事务内写入 <c>confirmation_deny</c> 审计与管家动态。</summary>
    /// <param name="homeId">目标家庭主键。</param>
    /// <param name="actorUserId">操作者用户标识。</param>
    /// <param name="confirmationId">目标确认项主键。</param>
    /// <param name="request">拒绝请求体，含原因。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>拒绝成功返回 200；不存在返回 404；原因缺失返回 422；已拒绝重放返回 200；已确认或过期返回 409。</returns>
    Task<ServiceResult> DenyAsync(long homeId, long actorUserId, long confirmationId, ConfirmationDenyRequest request, CancellationToken cancellationToken = default);

    /// <summary>L1 批量确认：预验证全部通过后单事务原子确认；同一幂等键仅返回首次记录的结果。</summary>
    /// <param name="homeId">目标家庭主键。</param>
    /// <param name="actorUserId">操作者用户标识。</param>
    /// <param name="request">批量确认请求体，含确认项 ID 列表与幂等键。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>
    /// 确认成功返回 200 与批量结果视图；请求形状非法返回 422；任一 ID 不在家庭作用域返回 404；
    /// 任一非 L1 / 非 pending / 已过期 / 同键异集返回 409；同键同集重放返回 200 首次结果。
    /// </returns>
    Task<ServiceResult> BatchConfirmAsync(long homeId, long actorUserId, ConfirmationBatchConfirmRequest request, CancellationToken cancellationToken = default);
}
