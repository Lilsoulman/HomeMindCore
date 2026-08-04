using System;
using System.Threading;
using System.Threading.Tasks;
using HomeMind.Api.Controllers.Base;
using HomeMind.Api.Services;
using HomeMind.Business.IServices.Agent;
using HomeMind.Business.IServices.Expert;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.AI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HomeMind.Api.Controllers.AI;

/// <summary>专家目录和 AgentRun 兼容入口。Controller 不直接访问数据库或执行 Skill。</summary>
[Authorize]
[Route("api/v1")]
public sealed class ExpertsController : ApiControllerBase
{
    private readonly IExpertCatalogServices _experts;
    private readonly IAgentRunServices _agentRuns;

    public ExpertsController(IExpertCatalogServices experts, IAgentRunServices agentRuns)
    {
        _experts = experts;
        _agentRuns = agentRuns;
    }

    [Authorize(Policy = PermissionNames.AiRead)]
    [HttpGet("experts")]
    public async Task<ActionResult<ApiResponse<object>>> ListExperts(string query, string category, string type) =>
        ToResponse(await WithUserAsync((user, token) => _experts.ListAsync(user.UserId, user.TenantId, query, category, type, token)));

    [Authorize(Policy = PermissionNames.AiRead)]
    [HttpGet("experts/{id:long}")]
    public async Task<ActionResult<ApiResponse<object>>> GetExpert(long id, string type = "expert") =>
        ToResponse(await WithUserAsync((user, token) => _experts.GetAsync(user.UserId, user.TenantId, id, type, token)));

    [Authorize(Policy = PermissionNames.AiRun)]
    [HttpPost("expert-runs")]
    public async Task<ActionResult<ApiResponse<object>>> CreateRun(AgentRunCreateRequest request) =>
        ToResponse(await WithUserAsync((user, token) => _agentRuns.CreateAsync(user.UserId, user.TenantId, request, token)));

    [Authorize(Policy = PermissionNames.AiRun)]
    [HttpGet("expert-runs/{id:long}")]
    public async Task<ActionResult<ApiResponse<object>>> GetRunById(long id) =>
        ToResponse(await WithUserAsync((user, token) => _agentRuns.GetAsync(user.UserId, user.TenantId, id, token)));

    [Authorize(Policy = PermissionNames.AiRun)]
    [HttpGet("expert-runs/{id:long}/events")]
    public async Task<ActionResult<ApiResponse<object>>> Events(long id) =>
        ToResponse(await WithUserAsync((user, token) => _agentRuns.ListEventsAsync(user.UserId, user.TenantId, id, token)));

    [Authorize(Policy = PermissionNames.AiRun)]
    [HttpPost("expert-runs/{id:long}/cancel")]
    public async Task<ActionResult<ApiResponse<object>>> Cancel(long id) =>
        ToResponse(await WithUserAsync((user, token) => _agentRuns.CancelAsync(user.UserId, user.TenantId, id, token)));

    [Authorize(Policy = PermissionNames.AiRun)]
    [HttpPost("expert-runs/{id:long}/retry")]
    public async Task<ActionResult<ApiResponse<object>>> Retry(long id) =>
        ToResponse(await WithUserAsync((user, token) => _agentRuns.RetryAsync(user.UserId, user.TenantId, id, token)));

    [Authorize(Policy = PermissionNames.AiRun)]
    [HttpPost("expert-runs/{id:long}/actions")]
    public async Task<ActionResult<ApiResponse<object>>> CreateAction(long id, AgentRunActionRequest request) =>
        ToResponse(await WithUserAsync((user, token) => _agentRuns.CreateActionAsync(user.UserId, user.TenantId, id, request, token)));

    private async Task<ServiceResult> WithUserAsync(Func<UserContext, CancellationToken, Task<ServiceResult>> action) =>
        TryGetUser(out var user)
            ? await action(user, HttpContext.RequestAborted)
            : new ServiceResult(401, "未提供访问令牌，或访问令牌已过期。");

    private static ActionResult<ApiResponse<object>> ToResponse(ServiceResult result) =>
        new ObjectResult(result.Succeeded
            ? new ApiResponse<object>(0, result.Message, result.Data)
            : ApiResponse<object>.Fail(result.StatusCode, result.Message))
        { StatusCode = result.StatusCode };
}
