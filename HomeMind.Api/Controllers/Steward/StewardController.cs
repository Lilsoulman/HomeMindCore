using System;
using System.Threading;
using System.Threading.Tasks;
using HomeMind.Api.Authorization;
using HomeMind.Api.Controllers.Base;
using HomeMind.Api.Services;
using HomeMind.Business.IServices.Steward;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.Steward;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HomeMind.Api.Controllers.Steward;

/// <summary>
/// 管家协同 API 控制器。负责管家动态（列表/详情/撤销）与确认中心（列表/确认/拒绝/L1 批量确认）入口。
/// 所有路由的 <c>{homeId}</c> 必须等于 JWT 推导的租户主键。
/// </summary>
/// <remarks>
/// 权限策略（B14 已收敛）：
/// - 管家动态读取使用 <c>steward.activity.read</c>；确认中心读取使用 <c>confirmation.read</c>。
/// - 写入接口（Undo/Confirm/Deny/BatchConfirm）使用 <c>confirmation.write</c>。
/// 确认、拒绝、批量确认与撤销均写入家庭审计日志与可展示的管家动态。
/// </remarks>
[Authorize]
[Route("api/v1/homes/{homeId:long}")]
public sealed class StewardController : ApiControllerBase
{
    private readonly IStewardServices _steward;

    /// <summary>构造管家协同控制器。</summary>
    /// <param name="steward">管家动态与确认中心服务。</param>
    public StewardController(IStewardServices steward) => _steward = steward;

    // ─── 管家动态 ───

    /// <summary>按家庭游标分页列出管家动态。</summary>
    /// <remarks>权限：<c>steward.activity.read</c>。分页参数 limit 上限 50；cursor 由上次响应返回。</remarks>
    /// <param name="homeId">家庭主键，必须等于当前 JWT tenant_id。</param>
    /// <param name="limit">每页条数，默认 20，上限 50。</param>
    /// <param name="cursor">分页游标，首次请求不传。</param>
    /// <returns>动态列表统一响应与下一页游标。</returns>
    [RequireHomeOwner]
    [Authorize(Policy = PermissionNames.StewardActivityRead)]
    [HttpGet("activities")]
    public async Task<ActionResult<ApiResponse<object>>> ListActivities(long homeId, int limit = 20, string? cursor = null) =>
        ToResponse(await _steward.ListActivitiesAsync(homeId, limit, cursor, HttpContext.RequestAborted));

    /// <summary>获取单条管家动态详情。</summary>
    /// <remarks>权限：<c>steward.activity.read</c>。跨家庭或不存在返回 404。</remarks>
    /// <param name="homeId">家庭主键。</param>
    /// <param name="activityId">目标动态主键。</param>
    /// <returns>动态详情统一响应；不存在返回 404。</returns>
    [RequireHomeOwner]
    [Authorize(Policy = PermissionNames.StewardActivityRead)]
    [HttpGet("activities/{activityId:long}")]
    public async Task<ActionResult<ApiResponse<object>>> GetActivity(long homeId, long activityId) =>
        ToResponse(await _steward.GetActivityAsync(homeId, activityId, HttpContext.RequestAborted));

    /// <summary>撤销可撤销的已完成管家动态，写入审计。</summary>
    /// <remarks>权限：<c>confirmation.write</c>。仅接受 undoable=true 且已完成的活动；撤销前实时复验资源状态。</remarks>
    /// <param name="homeId">家庭主键。</param>
    /// <param name="activityId">目标动态主键。</param>
    /// <returns>撤销成功返回 200；不存在返回 404；非已完成/不可撤销返回 422；已撤销返回 409。</returns>
    [RequireHomeOwner]
    [Authorize(Policy = PermissionNames.ConfirmationWrite)]
    [HttpPost("activities/{activityId:long}/undo")]
    public async Task<ActionResult<ApiResponse<object>>> UndoActivity(long homeId, long activityId) =>
        ToResponse(await WithUserAsync((user, token) => _steward.UndoActivityAsync(homeId, user.UserId, activityId, token)));

    // ─── 确认中心 ───

    /// <summary>列出确认项，支持风险等级与状态过滤。</summary>
    /// <remarks>权限：<c>confirmation.read</c>。过期项按计算语义不返回；过滤参数非法返回 422。</remarks>
    /// <param name="homeId">家庭主键。</param>
    /// <param name="riskLevel">可选风险等级过滤：L1/L2/L3。</param>
    /// <param name="status">可选状态过滤：pending/confirmed/denied/expired/cancelled。</param>
    /// <returns>确认项列表统一响应。</returns>
    [RequireHomeOwner]
    [Authorize(Policy = PermissionNames.ConfirmationRead)]
    [HttpGet("confirmations")]
    public async Task<ActionResult<ApiResponse<object>>> ListConfirmations(long homeId, string? riskLevel = null, string? status = null) =>
        ToResponse(await _steward.ListConfirmationsAsync(homeId, riskLevel, status, HttpContext.RequestAborted));

    /// <summary>单项确认确认项（L2/L3 逐项，L1 亦可），同一事务内复验归属与资源状态。</summary>
    /// <remarks>权限：<c>confirmation.write</c>。请求体必须携带 UUID 幂等键；重复确认已确认项返回现有结果。</remarks>
    /// <param name="homeId">家庭主键。</param>
    /// <param name="confirmationId">目标确认项主键。</param>
    /// <param name="request">确认请求体，含幂等键。</param>
    /// <returns>确认成功返回 200；不存在返回 404；幂等键非法返回 422；已终态或过期返回 409。</returns>
    [RequireHomeOwner]
    [Authorize(Policy = PermissionNames.ConfirmationWrite)]
    [HttpPost("confirmations/{confirmationId:long}/confirm")]
    public async Task<ActionResult<ApiResponse<object>>> Confirm(long homeId, long confirmationId, ConfirmationConfirmRequest request) =>
        ToResponse(await WithUserAsync((user, token) => _steward.ConfirmAsync(homeId, user.UserId, confirmationId, request, token)));

    /// <summary>拒绝确认项，原因必填并写入审计与管家动态。</summary>
    /// <remarks>权限：<c>confirmation.write</c>。拒绝原因长度 1-512，用于审计留痕。</remarks>
    /// <param name="homeId">家庭主键。</param>
    /// <param name="confirmationId">目标确认项主键。</param>
    /// <param name="request">拒绝请求体，含原因。</param>
    /// <returns>拒绝成功返回 200；不存在返回 404；原因缺失返回 422；已确认/终态/过期返回 409。</returns>
    [RequireHomeOwner]
    [Authorize(Policy = PermissionNames.ConfirmationWrite)]
    [HttpPost("confirmations/{confirmationId:long}/deny")]
    public async Task<ActionResult<ApiResponse<object>>> Deny(long homeId, long confirmationId, ConfirmationDenyRequest request) =>
        ToResponse(await WithUserAsync((user, token) => _steward.DenyAsync(homeId, user.UserId, confirmationId, request, token)));

    /// <summary>L1 批量确认：预验证全部通过后单事务原子确认，同幂等键仅返回首次结果。</summary>
    /// <remarks>权限：<c>confirmation.write</c>。任一 L2/L3、跨家庭、已终态、过期或重复 ID 都会整体拒绝，不做部分成功。</remarks>
    /// <param name="homeId">家庭主键。</param>
    /// <param name="request">批量确认请求体，含确认项 ID 列表与幂等键。</param>
    /// <returns>确认成功返回 200；形状非法返回 422；任一 ID 跨家庭返回 404；任一违规项返回 409。</returns>
    [RequireHomeOwner]
    [Authorize(Policy = PermissionNames.ConfirmationWrite)]
    [HttpPost("confirmations/batch-confirm")]
    public async Task<ActionResult<ApiResponse<object>>> BatchConfirm(long homeId, ConfirmationBatchConfirmRequest request) =>
        ToResponse(await WithUserAsync((user, token) => _steward.BatchConfirmAsync(homeId, user.UserId, request, token)));

    /// <summary>在用户上下文就绪时执行给定的业务回调，否则返回 401。</summary>
    /// <param name="action">执行业务逻辑的回调。</param>
    /// <returns>业务执行结果 <see cref="ServiceResult"/>。</returns>
    private async Task<ServiceResult> WithUserAsync(Func<UserContext, CancellationToken, Task<ServiceResult>> action) => TryGetUser(out var user)
        ? await action(user, HttpContext.RequestAborted)
        : new ServiceResult(401, "未提供访问令牌，或访问令牌已过期。");

    /// <summary>将 <see cref="ServiceResult"/> 转换为统一 HTTP 响应。</summary>
    /// <param name="result">业务执行结果。</param>
    /// <returns>统一响应体与对应状态码。</returns>
    private static ActionResult<ApiResponse<object>> ToResponse(ServiceResult result) => result.Succeeded
        ? new ObjectResult(new ApiResponse<object>(0, result.Message, result.Data)) { StatusCode = result.StatusCode }
        : new ObjectResult(ApiResponse<object>.Fail(result.Code, result.Message)) { StatusCode = result.StatusCode };
}
