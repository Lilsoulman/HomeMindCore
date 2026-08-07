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
/// <remarks>所有运行均由服务层冻结为 AgentRun；跨租户或他人的运行返回 404。</remarks>
[Authorize]
[Route("api/v1")]
public sealed class ExpertsController : ApiControllerBase
{
    private readonly IExpertCatalogServices _experts;
    private readonly IExpertSelfServeServices _selfServe;
    private readonly IAgentRunServices _agentRuns;

    /// <summary>构造专家与运行控制器。</summary>
    /// <param name="experts">专家目录业务服务。</param>
    /// <param name="selfServe">自建专家业务服务。</param>
    /// <param name="agentRuns">AgentRun 业务服务。</param>
    public ExpertsController(IExpertCatalogServices experts, IExpertSelfServeServices selfServe, IAgentRunServices agentRuns)
    {
        _experts = experts;
        _selfServe = selfServe;
        _agentRuns = agentRuns;
    }

    /// <summary>列出当前租户内可见的专家目录。</summary>
    /// <remarks>权限：<c>ai.read</c>。支持按名称、分类、类型筛选；B21 起支持 <c>scope</c> 区分平台基础专家与本人自建专家。</remarks>
    /// <param name="query">名称模糊查询字符串，可空。</param>
    /// <param name="category">分类，可空。</param>
    /// <param name="type">类型，可空。</param>
    /// <param name="scope">来源过滤：<c>basic</c>（默认，平台基础）/ <c>mine</c>（本人自建）/ <c>all</c>（两者）。</param>
    /// <returns>专家列表的统一响应。</returns>
    [Authorize(Policy = PermissionNames.AiRead)]
    [HttpGet("experts")]
    public async Task<ActionResult<ApiResponse<object>>> ListExperts(string query, string category, string type, string? scope = "basic") =>
        ToResponse(await WithUserAsync((user, token) => _experts.ListAsync(user.UserId, user.TenantId, query, category, type, scope, token)));

    /// <summary>创建用户自建专家（PC 用户端「我的专家」）。</summary>
    /// <remarks>权限：<c>expert.mine.write</c>。创建自动生成 <c>custom-</c> 前缀编码与 v1 已发布版本，仅创建者本人可见可维护。</remarks>
    /// <param name="request">自建专家创建请求体。</param>
    /// <returns>已创建自建专家的统一响应。</returns>
    [Authorize(Policy = PermissionNames.ExpertMineWrite)]
    [HttpPost("experts")]
    public async Task<ActionResult<ApiResponse<object>>> CreateExpert(ExpertCreateRequest request) =>
        ToResponse(await WithUserAsync((user, token) => _selfServe.CreateAsync(user.UserId, user.TenantId, request, token)));

    /// <summary>更新本人自建专家；生成 version+1 已发布版本。</summary>
    /// <remarks>权限：<c>expert.mine.write</c>。携带 RowVersion 乐观锁，冲突返回 409/40903。</remarks>
    /// <param name="id">自建专家主键。</param>
    /// <param name="request">自建专家更新请求体。</param>
    /// <returns>更新后自建专家的统一响应。</returns>
    [Authorize(Policy = PermissionNames.ExpertMineWrite)]
    [HttpPut("experts/{id:long}")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateExpert(long id, ExpertUpdateRequest request) =>
        ToResponse(await WithUserAsync((user, token) => _selfServe.UpdateAsync(user.UserId, user.TenantId, id, request, token)));

    /// <summary>软删除本人自建专家；已删专家从目录、运行解析与会话发送全部消失。</summary>
    /// <remarks>权限：<c>expert.mine.write</c>。重复删除返回 404。</remarks>
    /// <param name="id">自建专家主键。</param>
    /// <returns>删除结果的统一响应。</returns>
    [Authorize(Policy = PermissionNames.ExpertMineWrite)]
    [HttpDelete("experts/{id:long}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteExpert(long id) =>
        ToResponse(await WithUserAsync((user, token) => _selfServe.DeleteAsync(user.UserId, user.TenantId, id, token)));

    /// <summary>按主键获取单个专家或专家组的详情。</summary>
    /// <remarks>权限：<c>ai.read</c>。<c>type=expert_group</c> 时查询专家组；其他取值视为专家。</remarks>
    /// <param name="id">专家或专家组主键。</param>
    /// <param name="type">资源类型，默认为 <c>expert</c>。</param>
    /// <returns>专家详情的统一响应。</returns>
    [Authorize(Policy = PermissionNames.AiRead)]
    [HttpGet("experts/{id:long}")]
    public async Task<ActionResult<ApiResponse<object>>> GetExpert(long id, string type = "expert") =>
        ToResponse(await WithUserAsync((user, token) => _experts.GetAsync(user.UserId, user.TenantId, id, type, token)));

    /// <summary>创建一个新的 AgentRun，立即返回 <c>queued</c> 状态。</summary>
    /// <remarks>权限：<c>ai.run</c>。模型调用不会在 API 请求线程中执行；重复的 <c>idempotencyKey</c> 复用既有运行。</remarks>
    /// <param name="request">运行创建请求体。</param>
    /// <returns>已排队 AgentRun 的统一响应。</returns>
    [Authorize(Policy = PermissionNames.AiRun)]
    [HttpPost("expert-runs")]
    public async Task<ActionResult<ApiResponse<object>>> CreateRun(AgentRunCreateRequest request) =>
        ToResponse(await WithUserAsync((user, token) => _agentRuns.CreateAsync(user.UserId, user.TenantId, request, token)));

    /// <summary>按主键获取运行详情，仅返回展示安全的字段。</summary>
    /// <remarks>权限：<c>ai.run</c>。跨租户或他人的运行返回 404。</remarks>
    /// <param name="id">运行主键。</param>
    /// <returns>运行详情的统一响应。</returns>
    [Authorize(Policy = PermissionNames.AiRun)]
    [HttpGet("expert-runs/{id:long}")]
    public async Task<ActionResult<ApiResponse<object>>> GetRunById(long id) =>
        ToResponse(await WithUserAsync((user, token) => _agentRuns.GetAsync(user.UserId, user.TenantId, id, token)));

    /// <summary>列出指定运行的展示安全事件列表。</summary>
    /// <remarks>权限：<c>ai.run</c>。不返回提示或模型原始输出。</remarks>
    /// <param name="id">运行主键。</param>
    /// <returns>运行事件列表的统一响应。</returns>
    [Authorize(Policy = PermissionNames.AiRun)]
    [HttpGet("expert-runs/{id:long}/events")]
    public async Task<ActionResult<ApiResponse<object>>> Events(long id) =>
        ToResponse(await WithUserAsync((user, token) => _agentRuns.ListEventsAsync(user.UserId, user.TenantId, id, token)));

    /// <summary>列出当前用户的运行记录，可按专家与来源筛选。</summary>
    /// <remarks>权限：<c>ai.run</c>。仅返回当前用户自己的运行。</remarks>
    /// <param name="sourceType">来源类型，可空。</param>
    /// <param name="expertId">专家主键，可空。</param>
    /// <param name="limit">数量上限，默认 10。</param>
    /// <returns>运行列表的统一响应。</returns>
    [Authorize(Policy = PermissionNames.AiRun)]
    [HttpGet("expert-runs")]
    public async Task<ActionResult<ApiResponse<object>>> ListRuns(string? sourceType, long? expertId, int limit = 10) =>
        ToResponse(await WithUserAsync((user, token) => _agentRuns.ListAsync(user.UserId, user.TenantId, sourceType, expertId, limit, token)));

    /// <summary>请求取消一个运行；运行进入 <c>cancelled</c> 终态。</summary>
    /// <remarks>权限：<c>ai.run</c>。终态运行返回 409。</remarks>
    /// <param name="id">运行主键。</param>
    /// <returns>取消结果的统一响应。</returns>
    [Authorize(Policy = PermissionNames.AiRun)]
    [HttpPost("expert-runs/{id:long}/cancel")]
    public async Task<ActionResult<ApiResponse<object>>> Cancel(long id) =>
        ToResponse(await WithUserAsync((user, token) => _agentRuns.CancelAsync(user.UserId, user.TenantId, id, token)));

    /// <summary>重试一个失败的运行。</summary>
    /// <remarks>权限：<c>ai.run</c>。仅在运行达到终态后可重试；终态外的运行返回 409。</remarks>
    /// <param name="id">运行主键。</param>
    /// <returns>重试结果的统一响应。</returns>
    [Authorize(Policy = PermissionNames.AiRun)]
    [HttpPost("expert-runs/{id:long}/retry")]
    public async Task<ActionResult<ApiResponse<object>>> Retry(long id) =>
        ToResponse(await WithUserAsync((user, token) => _agentRuns.RetryAsync(user.UserId, user.TenantId, id, token)));

    /// <summary>为运行追加一个受控动作，例如 <c>smart_home_device</c>。</summary>
    /// <remarks>权限：<c>ai.run</c>。动作在确认前不执行；不会绕过确认、幂等与审计链。</remarks>
    /// <param name="id">运行主键。</param>
    /// <param name="request">动作请求体。</param>
    /// <returns>动作创建结果的统一响应。</returns>
    [Authorize(Policy = PermissionNames.AiRun)]
    [HttpPost("expert-runs/{id:long}/actions")]
    public async Task<ActionResult<ApiResponse<object>>> CreateAction(long id, AgentRunActionRequest request) =>
        ToResponse(await WithUserAsync((user, token) => _agentRuns.CreateActionAsync(user.UserId, user.TenantId, id, request, token)));

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
    private static ActionResult<ApiResponse<object>> ToResponse(ServiceResult result) =>
        new ObjectResult(result.Succeeded
            ? new ApiResponse<object>(0, result.Message, result.Data)
            : ApiResponse<object>.Fail(result.Code, result.Message))
        { StatusCode = result.StatusCode };
}
