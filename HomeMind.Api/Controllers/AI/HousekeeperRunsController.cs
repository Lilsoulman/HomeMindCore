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
[Authorize]
[Route("api/v1")]
public sealed class HousekeeperRunsController : ApiControllerBase
{
    private readonly IHousekeeperRunServices _runs;

    public HousekeeperRunsController(IHousekeeperRunServices runs) => _runs = runs;

    [Authorize(Policy = PermissionNames.AiRun)]
    [HttpPost("housekeeper-runs")]
    public async Task<ActionResult<ApiResponse<object>>> Create(HousekeeperRunRequest request) =>
        ToResponse(await WithUserAsync((user, token) => _runs.CreateAsync(user.UserId, user.TenantId, request, token)));

    [Authorize(Policy = PermissionNames.AiRun)]
    [HttpGet("expert-runs/{runId:long}/actions")]
    public async Task<ActionResult<ApiResponse<object>>> GetActions(long runId) =>
        ToResponse(await WithUserAsync((user, token) => _runs.GetActionsAsync(user.UserId, user.TenantId, runId, token)));

    [Authorize(Policy = PermissionNames.AiRun)]
    [HttpPost("expert-runs/{runId:long}/actions/{actionId:long}/confirm")]
    public async Task<ActionResult<ApiResponse<object>>> ConfirmAction(long runId, long actionId, ConfirmHousekeeperActionRequest request) =>
        ToResponse(await WithUserAsync((user, token) => _runs.ConfirmActionAsync(user.UserId, user.TenantId, runId, actionId, request, token)));

    private async Task<ServiceResult> WithUserAsync(Func<UserContext, CancellationToken, Task<ServiceResult>> action) =>
        TryGetUser(out var user)
            ? await action(user, HttpContext.RequestAborted)
            : new ServiceResult(401, "未提供访问令牌，或访问令牌已过期。");

    private static ActionResult<ApiResponse<object>> ToResponse(ServiceResult result) => result.Succeeded
        ? new ObjectResult(new ApiResponse<object>(0, result.Message, result.Data)) { StatusCode = result.StatusCode }
        : new ObjectResult(ApiResponse<object>.Fail(result.StatusCode, result.Message)) { StatusCode = result.StatusCode };
}
