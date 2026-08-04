using System;
using System.Threading;
using System.Threading.Tasks;
using HomeMind.Api.Controllers.Base;
using HomeMind.Api.Services;
using HomeMind.Business.IServices.Expert;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.AI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HomeMind.Api.Controllers.AI;

/// <summary>版本化的多专家团队运行入口。仅支持显式 sequential / parallel / synthesis 三种模式；不得携带任意 Prompt 或工具调用。</summary>
[Authorize]
[Route("api/v1")]
public sealed class TeamRunsController : ApiControllerBase
{
    private readonly ITeamRunServices _teams;

    public TeamRunsController(ITeamRunServices teams) => _teams = teams;

    [Authorize(Policy = PermissionNames.TeamRunWrite)]
    [HttpPost("team-runs")]
    public async Task<ActionResult<ApiResponse<object>>> Create(TeamRunCreateRequest request) =>
        ToResponse(await WithUserAsync((user, token) => _teams.CreateAsync(user.UserId, user.TenantId, request, token)));

    [Authorize(Policy = PermissionNames.TeamRunRead)]
    [HttpGet("team-runs/{id:long}")]
    public async Task<ActionResult<ApiResponse<object>>> Get(long id) =>
        ToResponse(await WithUserAsync((user, token) => _teams.GetAsync(user.UserId, user.TenantId, id, token)));

    [Authorize(Policy = PermissionNames.TeamRunRead)]
    [HttpGet("team-runs/{id:long}/events")]
    public async Task<ActionResult<ApiResponse<object>>> Events(long id) =>
        ToResponse(await WithUserAsync((user, token) => _teams.ListEventsAsync(user.UserId, user.TenantId, id, token)));

    [Authorize(Policy = PermissionNames.TeamRunRead)]
    [HttpGet("team-runs/{id:long}/members")]
    public async Task<ActionResult<ApiResponse<object>>> Members(long id) =>
        ToResponse(await WithUserAsync((user, token) => _teams.ListMembersAsync(user.UserId, user.TenantId, id, token)));

    [Authorize(Policy = PermissionNames.TeamRunRead)]
    [HttpGet("team-runs/{id:long}/synthesis")]
    public async Task<ActionResult<ApiResponse<object>>> Synthesis(long id) =>
        ToResponse(await WithUserAsync((user, token) => _teams.GetSynthesisAsync(user.UserId, user.TenantId, id, token)));

    [Authorize(Policy = PermissionNames.TeamRunWrite)]
    [HttpPost("team-runs/{id:long}/cancel")]
    public async Task<ActionResult<ApiResponse<object>>> Cancel(long id) =>
        ToResponse(await WithUserAsync((user, token) => _teams.CancelAsync(user.UserId, user.TenantId, id, token)));

    [Authorize(Policy = PermissionNames.TeamRunWrite)]
    [HttpPost("team-runs/{id:long}/retry")]
    public async Task<ActionResult<ApiResponse<object>>> Retry(long id) =>
        ToResponse(await WithUserAsync((user, token) => _teams.RetryAsync(user.UserId, user.TenantId, id, token)));

    private async Task<ServiceResult> WithUserAsync(Func<UserContext, CancellationToken, Task<ServiceResult>> action) =>
        TryGetUser(out var user)
            ? await action(user, HttpContext.RequestAborted)
            : new ServiceResult(401, "未提供访问令牌，或访问令牌已过期。");

    private static ActionResult<ApiResponse<object>> ToResponse(ServiceResult result) => result.Succeeded
        ? new ObjectResult(new ApiResponse<object>(0, result.Message, result.Data)) { StatusCode = result.StatusCode }
        : new ObjectResult(ApiResponse<object>.Fail(result.StatusCode, result.Message)) { StatusCode = result.StatusCode };
}
