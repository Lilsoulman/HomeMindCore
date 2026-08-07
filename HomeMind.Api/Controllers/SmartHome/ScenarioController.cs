using System;
using System.Threading;
using System.Threading.Tasks;
using HomeMind.Api.Controllers.Base;
using HomeMind.Api.Services;
using HomeMind.Business.IServices.SmartHome;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.SmartHome;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HomeMind.Api.Controllers.SmartHome;

/// <summary>场景工作流入口：平台模板浏览、家庭实例启用与场景运行；执行经确认、幂等与审计链路。</summary>
/// <remarks>步骤上下文由运行动作的 RequestJson 承载；不返回凭据、厂商字段或设备原始状态。</remarks>
[Authorize]
[Route("api/v1/smart-home/scenarios")]
public sealed class ScenarioController : ApiControllerBase
{
    private readonly IScenarioWorkflowServices _scenarios;

    /// <summary>构造场景工作流控制器。</summary>
    /// <param name="scenarios">场景工作流服务。</param>
    public ScenarioController(IScenarioWorkflowServices scenarios) => _scenarios = scenarios;

    /// <summary>列出平台级场景模板。</summary>
    /// <remarks>权限：<c>smart_home.read</c>。模板未解析设备，仅描述能力模板。</remarks>
    /// <returns>模板列表统一响应。</returns>
    [Authorize(Policy = PermissionNames.SmartHomeRead)]
    [HttpGet("templates")]
    public async Task<ActionResult<ApiResponse<object>>> ListTemplates() =>
        ToResponse(await WithUserAsync((user, token) => _scenarios.ListTemplatesAsync(user.TenantId, token)));

    /// <summary>列出当前家庭的场景实例。</summary>
    /// <remarks>权限：<c>smart_home.read</c>。实例步骤已解析到具体设备，unavailable 步骤执行时跳过。</remarks>
    /// <returns>实例列表统一响应。</returns>
    [Authorize(Policy = PermissionNames.SmartHomeRead)]
    [HttpGet("instances")]
    public async Task<ActionResult<ApiResponse<object>>> ListInstances() =>
        ToResponse(await WithUserAsync((user, token) => _scenarios.ListInstancesAsync(user.TenantId, token)));

    /// <summary>启用一个场景模板：按 device_type + room + capability 解析家庭设备生成实例；缺设备不阻塞启用。</summary>
    /// <remarks>权限：<c>smart_home.write</c>。同一模板重复启用返回既有实例。</remarks>
    /// <param name="templateCode">模板业务键，如 goodnight / arrive_home / leave_home。</param>
    /// <returns>实例视图统一响应；模板不存在或已停用返回 404。</returns>
    [Authorize(Policy = PermissionNames.SmartHomeWrite)]
    [HttpPost("templates/{templateCode}/enable")]
    public async Task<ActionResult<ApiResponse<object>>> Enable(string templateCode) =>
        ToResponse(await WithUserAsync((user, token) => _scenarios.EnableAsync(user.UserId, user.TenantId, templateCode, token)));

    /// <summary>运行一个已启用的场景实例；创建待确认的场景运行动作。</summary>
    /// <remarks>权限：<c>ai.run</c>。确认前不执行任何设备命令；确认/幂等/审计复用既有链路。</remarks>
    /// <param name="instanceId">场景实例主键。</param>
    /// <param name="request">运行请求体，可选幂等键。</param>
    /// <returns>运行视图统一响应；实例不存在返回 404。</returns>
    [Authorize(Policy = PermissionNames.AiRun)]
    [HttpPost("instances/{instanceId:long}/run")]
    public async Task<ActionResult<ApiResponse<object>>> Run(long instanceId, ScenarioRunRequest request) =>
        ToResponse(await WithUserAsync((user, token) => _scenarios.RunAsync(user.UserId, user.TenantId, instanceId, request, token)));

    /// <summary>确认并执行一个场景运行动作；逐步下发设备命令并按 success / partial / failed 汇总。</summary>
    /// <remarks>权限：<c>ai.run</c>。需要必填的 <c>idempotencyKey</c>；重复确认返回既有结果，不重复执行设备命令。</remarks>
    /// <param name="runId">运行主键。</param>
    /// <param name="actionId">动作主键。</param>
    /// <param name="request">确认请求体，含 UUID 幂等键。</param>
    /// <returns>执行结果统一响应；非法幂等键返回 422；动作不存在返回 404；已终态返回 409。</returns>
    [Authorize(Policy = PermissionNames.AiRun)]
    [HttpPost("runs/{runId:long}/actions/{actionId:long}/confirm")]
    public async Task<ActionResult<ApiResponse<object>>> ConfirmAction(long runId, long actionId, ConfirmScenarioActionRequest request) =>
        ToResponse(await WithUserAsync((user, token) => _scenarios.ConfirmActionAsync(user.UserId, user.TenantId, runId, actionId, request, token)));

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
