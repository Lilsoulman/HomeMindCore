using System;
using System.Threading;
using System.Threading.Tasks;
using HomeMind.Api.Controllers.Base;
using HomeMind.Api.Services;
using HomeMind.Business.IServices.AI;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.AI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HomeMind.Api.Controllers.AI;

/// <summary>家庭管家运行入口，仅生成可确认的设备行动草案。</summary>
/// <remarks>新功能请改用 AgentRun；本控制器保留仅为兼容旧 housekeeper-runs 路由。所有动作必须经过确认、授权、幂等与审计。</remarks>
[Authorize]
[Route("api/v1")]
public sealed class HousekeeperRunsController : ApiControllerBase
{
    private readonly IHousekeeperRunServices _runs;

    /// <summary>构造家庭管家运行控制器。</summary>
    /// <param name="runs">家庭管家运行业务服务。</param>
    public HousekeeperRunsController(IHousekeeperRunServices runs) => _runs = runs;

    /// <summary>创建一个家庭管家运行，返回带展示安全动作的事件与动作草稿。</summary>
    /// <remarks>权限：<c>ai.run</c>。意图仅限 <c>arrive</c>、<c>away</c>、<c>environment_review</c> 与 <c>sleep</c>。</remarks>
    /// <param name="request">运行创建请求体，包含意图、可选空间 ID 与幂等键。</param>
    /// <returns>运行详情与动作草稿的统一响应。</returns>
    [Authorize(Policy = PermissionNames.AiRun)]
    [HttpPost("housekeeper-runs")]
    public async Task<ActionResult<ApiResponse<object>>> Create(HousekeeperRunRequest request) =>
        ToResponse(await WithUserAsync((user, token) => _runs.CreateAsync(user.UserId, user.TenantId, request, token)));

    /// <summary>获取指定 AgentRun 关联的动作草稿列表。</summary>
    /// <remarks>权限：<c>ai.run</c>。仅展示设备 ID、名称、能力、目标值；不返回凭据或厂商字段。</remarks>
    /// <param name="runId">运行主键。</param>
    /// <returns>动作列表的统一响应。</returns>
    [Authorize(Policy = PermissionNames.AiRun)]
    [HttpGet("expert-runs/{runId:long}/actions")]
    public async Task<ActionResult<ApiResponse<object>>> GetActions(long runId) =>
        ToResponse(await WithUserAsync((user, token) => _runs.GetActionsAsync(user.UserId, user.TenantId, runId, token)));

    /// <summary>确认并调度一个待执行动作。调度前会重新检查授权、连接器状态与设备能力。</summary>
    /// <remarks>权限：<c>ai.run</c>。需要必填的 <c>idempotencyKey</c>；凭据与厂商实体 ID 绝不离开适配器。</remarks>
    /// <param name="runId">运行主键。</param>
    /// <param name="actionId">动作主键。</param>
    /// <param name="request">确认请求体，包含 UUID 幂等键。</param>
    /// <returns>动作执行结果视图的统一响应。</returns>
    [Authorize(Policy = PermissionNames.AiRun)]
    [HttpPost("expert-runs/{runId:long}/actions/{actionId:long}/confirm")]
    public async Task<ActionResult<ApiResponse<object>>> ConfirmAction(long runId, long actionId, ConfirmHousekeeperActionRequest request) =>
        ToResponse(await WithUserAsync((user, token) => _runs.ConfirmActionAsync(user.UserId, user.TenantId, runId, actionId, request, token)));

    /// <summary>在用户上下文就绪时执行给定的业务回调，否则返回 401。</summary>
    /// <param name="action">执行业务逻辑的回调。</param>
    /// <returns>业务执行结果 <see cref="ServiceResult"/>。</returns>
    private async Task<ServiceResult> WithUserAsync(Func<UserContext, CancellationToken, Task<ServiceResult>> action) =>
        TryGetUser(out var user)
            ? await action(user, HttpContext.RequestAborted)
            : new ServiceResult(401, "未提供访问令牌，或访问令牌已过期。");

    /// <summary>将 <see cref="ServiceResult"/> 转换为统一 HTTP 响应。</summary>
    /// <param name="result">业务执行结果。</param>
    /// <returns>成功 2xx；失败按 <see cref="ServiceResult.StatusCode"/> 返回。</returns>
    private static ActionResult<ApiResponse<object>> ToResponse(ServiceResult result) => result.Succeeded
        ? new ObjectResult(new ApiResponse<object>(0, result.Message, result.Data)) { StatusCode = result.StatusCode }
        : new ObjectResult(ApiResponse<object>.Fail(result.Code, result.Message)) { StatusCode = result.StatusCode };
}
