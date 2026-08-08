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

/// <summary>Skill 独立执行入口：按 skillCode 创建 Skill 运行（SourceType=skill，不绑定专家）。</summary>
/// <remarks>运行轮询/取消/重试复用既有 expert-runs 契约；响应不包含素材目录内容、MCP 内部路径、草稿绝对路径或 Prompt。</remarks>
[Authorize]
[Route("api/v1/skills")]
public sealed class SkillRunsController : ApiControllerBase
{
    private readonly ISkillRunServices _skillRuns;

    /// <summary>构造 Skill 运行控制器。</summary>
    /// <param name="skillRuns">Skill 运行服务。</param>
    public SkillRunsController(ISkillRunServices skillRuns) => _skillRuns = skillRuns;

    /// <summary>创建 Skill 运行：解析平台 Skill 目录，确定性生成剪辑方案并产出待确认的 draft_generate 动作。</summary>
    /// <remarks>权限：<c>ai.run</c> + <c>media.read</c>。确认前不写入任何草稿；确认/幂等/审计复用既有链路。</remarks>
    /// <param name="skillCode">Skill 业务键，如 quick-edit。</param>
    /// <param name="request">运行请求体，含 UUID 幂等键与 Skill 输入参数（media_location 必填）。</param>
    /// <returns>运行视图统一响应；未知/未启用 Skill 或输入非法返回 422。</returns>
    [Authorize(Policy = PermissionNames.AiRun)]
    [Authorize(Policy = PermissionNames.MediaRead)]
    [HttpPost("{skillCode}/runs")]
    public async Task<ActionResult<ApiResponse<object>>> Create(string skillCode, SkillRunCreateRequest request) =>
        ToResponse(await WithUserAsync((user, token) => _skillRuns.CreateAsync(user.UserId, user.TenantId, skillCode, request, token)));

    /// <summary>确认并执行 Skill 运行动作：经剪辑 MCP 生成 .draft 草稿并登记为生成文件；同幂等键重放首次结果。</summary>
    /// <remarks>权限：<c>ai.run</c> + <c>media.read</c>。需要必填的 <c>idempotencyKey</c>；登记后下载复用既有
    /// <c>POST /api/v1/expert-files/&#123;fileId&#125;/read-token</c>（10 分钟 readToken）。</remarks>
    /// <param name="runId">运行主键。</param>
    /// <param name="actionId">动作主键。</param>
    /// <param name="request">确认请求体，含 UUID 幂等键。</param>
    /// <returns>执行结果统一响应；非法幂等键返回 422；动作不存在返回 404；已终态返回 409。</returns>
    [Authorize(Policy = PermissionNames.AiRun)]
    [Authorize(Policy = PermissionNames.MediaRead)]
    [HttpPost("runs/{runId:long}/actions/{actionId:long}/confirm")]
    public async Task<ActionResult<ApiResponse<object>>> ConfirmAction(long runId, long actionId, ConfirmSkillRunActionRequest request) =>
        ToResponse(await WithUserAsync((user, token) => _skillRuns.ConfirmActionAsync(user.UserId, user.TenantId, runId, actionId, request, token)));

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
