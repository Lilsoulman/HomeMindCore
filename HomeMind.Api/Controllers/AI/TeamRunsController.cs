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
/// <remarks>外部副作用仍由运行动作的确认、幂等与审计链产生；团队编排绝不绕过该边界。</remarks>
[Authorize]
[Route("api/v1")]
public sealed class TeamRunsController : ApiControllerBase
{
    private readonly ITeamRunServices _teams;

    /// <summary>构造团队运行控制器。</summary>
    /// <param name="teams">团队运行业务服务。</param>
    public TeamRunsController(ITeamRunServices teams) => _teams = teams;

    /// <summary>创建一个新的团队运行，并将模板与成员冻结到版本快照。</summary>
    /// <remarks>权限：<c>team_run.write</c>。客户端必须精确发送 <c>teamVersion="1"</c>；权限交集由服务端计算。</remarks>
    /// <param name="request">团队运行创建请求体。</param>
    /// <returns>新建团队运行的统一响应。</returns>
    [Authorize(Policy = PermissionNames.TeamRunWrite)]
    [HttpPost("team-runs")]
    public async Task<ActionResult<ApiResponse<object>>> Create(TeamRunCreateRequest request) =>
        ToResponse(await WithUserAsync((user, token) => _teams.CreateAsync(user.UserId, user.TenantId, request, token)));

    /// <summary>按主键获取团队运行汇总视图。</summary>
    /// <remarks>权限：<c>team_run.read</c>。跨租户或未知 ID 返回 404。</remarks>
    /// <param name="id">团队运行主键。</param>
    /// <returns>团队运行汇总视图的统一响应。</returns>
    [Authorize(Policy = PermissionNames.TeamRunRead)]
    [HttpGet("team-runs/{id:long}")]
    public async Task<ActionResult<ApiResponse<object>>> Get(long id) =>
        ToResponse(await WithUserAsync((user, token) => _teams.GetAsync(user.UserId, user.TenantId, id, token)));

    /// <summary>列出团队运行的事件，仅含展示字段。</summary>
    /// <remarks>权限：<c>team_run.read</c>。不返回提示或模型输出。</remarks>
    /// <param name="id">团队运行主键。</param>
    /// <returns>事件列表的统一响应。</returns>
    [Authorize(Policy = PermissionNames.TeamRunRead)]
    [HttpGet("team-runs/{id:long}/events")]
    public async Task<ActionResult<ApiResponse<object>>> Events(long id) =>
        ToResponse(await WithUserAsync((user, token) => _teams.ListEventsAsync(user.UserId, user.TenantId, id, token)));

    /// <summary>列出团队运行成员视图。</summary>
    /// <remarks>权限：<c>team_run.read</c>。</remarks>
    /// <param name="id">团队运行主键。</param>
    /// <returns>成员列表的统一响应。</returns>
    [Authorize(Policy = PermissionNames.TeamRunRead)]
    [HttpGet("team-runs/{id:long}/members")]
    public async Task<ActionResult<ApiResponse<object>>> Members(long id) =>
        ToResponse(await WithUserAsync((user, token) => _teams.ListMembersAsync(user.UserId, user.TenantId, id, token)));

    /// <summary>获取团队运行的聚合结果。</summary>
    /// <remarks>权限：<c>team_run.read</c>。仅在 <c>completed</c> 状态下可用；其他状态返回 409。</remarks>
    /// <param name="id">团队运行主键。</param>
    /// <returns>聚合结果视图的统一响应。</returns>
    [Authorize(Policy = PermissionNames.TeamRunRead)]
    [HttpGet("team-runs/{id:long}/synthesis")]
    public async Task<ActionResult<ApiResponse<object>>> Synthesis(long id) =>
        ToResponse(await WithUserAsync((user, token) => _teams.GetSynthesisAsync(user.UserId, user.TenantId, id, token)));

    /// <summary>取消一个处于 <c>pending</c> 或 <c>running</c> 状态的团队运行。</summary>
    /// <remarks>权限：<c>team_run.write</c>。终态运行返回 409；会写入审计条目并增加 <c>team_runs_triggered_total</c> 指标。</remarks>
    /// <param name="id">团队运行主键。</param>
    /// <returns>取消结果的统一响应。</returns>
    [Authorize(Policy = PermissionNames.TeamRunWrite)]
    [HttpPost("team-runs/{id:long}/cancel")]
    public async Task<ActionResult<ApiResponse<object>>> Cancel(long id) =>
        ToResponse(await WithUserAsync((user, token) => _teams.CancelAsync(user.UserId, user.TenantId, id, token)));

    /// <summary>重试一个达到终态的团队运行。</summary>
    /// <remarks>权限：<c>team_run.write</c>。非终态运行返回 409；会写入审计条目并增加 <c>team_runs_triggered_total</c> 指标。</remarks>
    /// <param name="id">团队运行主键。</param>
    /// <returns>重试结果的统一响应。</returns>
    [Authorize(Policy = PermissionNames.TeamRunWrite)]
    [HttpPost("team-runs/{id:long}/retry")]
    public async Task<ActionResult<ApiResponse<object>>> Retry(long id) =>
        ToResponse(await WithUserAsync((user, token) => _teams.RetryAsync(user.UserId, user.TenantId, id, token)));

    /// <summary>在用户上下文就绪时执行给定的业务回调，否则返回 401。</summary>
    /// <param name="action">执行业务逻辑的回调。</param>
    /// <returns>业务执行结果 <see cref="ServiceResult"/>。</returns>
    private async Task<ServiceResult> WithUserAsync(Func<UserContext, CancellationToken, Task<ServiceResult>> action) =>
        TryGetUser(out var user)
            ? await action(user, HttpContext.RequestAborted)
            : new ServiceResult(401, "未提供访问令牌，或访问令牌已过期。");

    /// <summary>将 <see cref="ServiceResult"/> 转换为统一 HTTP 响应。</summary>
    /// <param name="result">业务执行结果。</param>
    /// <returns>统一响应体与对应状态码。</returns>
    private static ActionResult<ApiResponse<object>> ToResponse(ServiceResult result) => result.Succeeded
        ? new ObjectResult(new ApiResponse<object>(0, result.Message, result.Data)) { StatusCode = result.StatusCode }
        : new ObjectResult(ApiResponse<object>.Fail(result.Code, result.Message)) { StatusCode = result.StatusCode };
}
