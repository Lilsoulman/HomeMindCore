using System;
using System.Threading;
using System.Threading.Tasks;
using HomeMind.Api.Controllers.Base;
using HomeMind.Api.Services;
using HomeMind.Business.IServices.Life;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.Life;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HomeMind.Api.Controllers.Life;

/// <summary>个人生活专家运行入口（探店翻牌与行程规划）。</summary>
/// <remarks>翻牌建议为只读 L1；行程同步日历的动作经确认、幂等与审计链路执行。</remarks>
[Authorize]
[Route("api/v1")]
public sealed class LifeRunsController : ApiControllerBase
{
    private readonly ILifeExpertRunServices _runs;

    /// <summary>构造个人生活专家运行控制器。</summary>
    /// <param name="runs">个人生活专家运行服务。</param>
    public LifeRunsController(ILifeExpertRunServices runs) => _runs = runs;

    /// <summary>创建一个个人生活专家运行：翻牌返回 Top1-2 建议，行程生成待确认动作。</summary>
    /// <remarks>权限：<c>ai.run</c>。意图仅限 <c>recommend</c>（行程规划随后续版本开放）；输入为合法 JSON。</remarks>
    /// <param name="request">运行创建请求体，包含意图、输入 JSON 与可选幂等键。</param>
    /// <returns>运行详情统一响应。</returns>
    [Authorize(Policy = PermissionNames.AiRun)]
    [HttpPost("experts/personal-life-expert/runs")]
    public async Task<ActionResult<ApiResponse<object>>> Create(LifeExpertRunRequest request) =>
        ToResponse(await WithUserAsync((user, token) => _runs.CreateAsync(user.UserId, user.TenantId, request, token)));

    /// <summary>确认并执行一个待确认的行程同步动作（calendar_create_event），确认后逐日写入日历。</summary>
    /// <remarks>权限：<c>ai.run</c>。需要必填的 <c>idempotencyKey</c>；重复确认返回既有结果，不重复创建日历事件。</remarks>
    /// <param name="runId">运行主键。</param>
    /// <param name="actionId">动作主键。</param>
    /// <param name="request">确认请求体，含 UUID 幂等键。</param>
    /// <returns>执行结果统一响应；幂等键非法返回 422；动作不存在返回 404；已终态返回 409。</returns>
    [Authorize(Policy = PermissionNames.AiRun)]
    [HttpPost("experts/personal-life-expert/runs/{runId:long}/actions/{actionId:long}/confirm")]
    public async Task<ActionResult<ApiResponse<object>>> ConfirmAction(long runId, long actionId, ConfirmLifeExpertActionRequest request) =>
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
