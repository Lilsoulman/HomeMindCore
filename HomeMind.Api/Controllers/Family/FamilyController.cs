using System;
using System.Threading;
using System.Threading.Tasks;
using HomeMind.Api.Authorization;
using HomeMind.Api.Controllers.Base;
using HomeMind.Api.Services;
using HomeMind.Business.IServices.Family;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.Family;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HomeMind.Api.Controllers.Family;

/// <summary>
/// 家庭上下文 API 控制器。负责家庭成员、知识库、决策历史的 CRUD 与审计入口。
/// 所有路由的 <c>{homeId}</c> 必须等于 JWT 推导的租户主键。
/// </summary>
/// <remarks>
/// 权限策略（B14 已收敛）：
/// - 只读接口（List）使用 <c>family.read</c>。
/// - 写入接口（Create/Update/Correct/Write/Record/Delete）使用 <c>family.write</c>。
/// </remarks>
[Authorize]
[Route("api/v1/homes/{homeId:long}")]
public sealed class FamilyController : ApiControllerBase
{
    private readonly IFamilyMemberServices _members;
    private readonly IFamilyKnowledgeServices _knowledge;
    private readonly IFamilyDecisionServices _decisions;

    /// <summary>构造家庭上下文控制器。</summary>
    /// <param name="members">家庭成员服务。</param>
    /// <param name="knowledge">家庭知识服务。</param>
    /// <param name="decisions">家庭决策历史服务。</param>
    public FamilyController(IFamilyMemberServices members, IFamilyKnowledgeServices knowledge, IFamilyDecisionServices decisions)
    {
        _members = members;
        _knowledge = knowledge;
        _decisions = decisions;
    }

    // ─── 家庭成员 ───

    /// <summary>列出当前家庭下未删除的家庭成员。</summary>
    /// <remarks>权限：<c>family.read</c>。租户由 JWT 推导，路径 homeId 必须与之相等。</remarks>
    /// <param name="homeId">家庭主键，必须等于当前 JWT tenant_id。</param>
    /// <returns>成员列表统一响应。</returns>
    [RequireHomeOwner]
    [Authorize(Policy = PermissionNames.FamilyRead)]
    [HttpGet("family-members")]
    public async Task<ActionResult<ApiResponse<object>>> ListMembers(long homeId) =>
        ToResponse(await _members.ListAsync(homeId, HttpContext.RequestAborted));

    /// <summary>创建一名新家庭成员；默认状态为 active。</summary>
    /// <remarks>权限：<c>family.write</c>。租户由 JWT 推导。</remarks>
    /// <param name="homeId">家庭主键，必须等于当前 JWT tenant_id。</param>
    /// <param name="request">创建请求体。</param>
    /// <returns>创建成功返回 201；校验失败返回 422。</returns>
    [RequireHomeOwner]
    [Authorize(Policy = PermissionNames.FamilyWrite)]
    [HttpPost("members")]
    public async Task<ActionResult<ApiResponse<object>>> CreateMember(long homeId, FamilyMemberCreateRequest request) =>
        ToResponse(await WithUserAsync((user, token) => _members.CreateAsync(homeId, user.UserId, request, token)));

    /// <summary>部分更新家庭成员信息，仅允许在 active 与 away 之间切换。</summary>
    /// <remarks>权限：<c>family.write</c>。终态更正需使用 correction 端点。</remarks>
    /// <param name="homeId">家庭主键。</param>
    /// <param name="memberId">目标成员主键。</param>
    /// <param name="request">更新请求体。</param>
    /// <returns>更新成功返回 200；成员不存在返回 404；状态非法返回 422。</returns>
    [RequireHomeOwner]
    [Authorize(Policy = PermissionNames.FamilyWrite)]
    [HttpPut("members/{memberId:long}")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateMember(long homeId, long memberId, FamilyMemberUpdateRequest request) =>
        ToResponse(await WithUserAsync((user, token) => _members.UpdateAsync(homeId, user.UserId, memberId, request, token)));

    /// <summary>成员终态更正或恢复；任何进入/退出终态的操作均写入终端三字段与审计。</summary>
    /// <remarks>权限：<c>family.write</c>。进入终态时原因必填。审计动作按更正/恢复区分写入 family_audit_logs。</remarks>
    /// <param name="homeId">家庭主键。</param>
    /// <param name="memberId">目标成员主键。</param>
    /// <param name="request">更正请求体，含目标状态与原因。</param>
    /// <returns>更正成功返回 200；成员不存在返回 404；状态非法返回 422。</returns>
    [RequireHomeOwner]
    [Authorize(Policy = PermissionNames.FamilyWrite)]
    [HttpPost("members/{memberId:long}/correction")]
    public async Task<ActionResult<ApiResponse<object>>> CorrectMember(long homeId, long memberId, FamilyMemberCorrectionRequest request) =>
        ToResponse(await WithUserAsync((user, token) => _members.CorrectAsync(homeId, user.UserId, memberId, request, token)));

    // ─── 家庭知识库 ───

    /// <summary>列出当前家庭下未删除的知识条；可按分类过滤。</summary>
    /// <remarks>权限：<c>family.read</c>。</remarks>
    /// <param name="homeId">家庭主键。</param>
    /// <param name="category">可选的知识分类：property/wifi/repair/cleaning/insurance/other。</param>
    /// <returns>知识列表统一响应。</returns>
    [RequireHomeOwner]
    [Authorize(Policy = PermissionNames.FamilyRead)]
    [HttpGet("knowledge")]
    public async Task<ActionResult<ApiResponse<object>>> ListKnowledge(long homeId, string? category) =>
        ToResponse(await _knowledge.ListAsync(homeId, category, HttpContext.RequestAborted));

    /// <summary>写入一条家庭知识；同 key 冲突按 latest/authority/majority 策略留痕。</summary>
    /// <remarks>权限：<c>family.write</c>。同 home_id+category+knowledge_key 的冲突在同一事务内解决并写入审计。</remarks>
    /// <param name="homeId">家庭主键。</param>
    /// <param name="request">写入请求体。</param>
    /// <returns>写入成功返回 201 与知识视图及冲突解决摘要；校验失败返回 422。</returns>
    [RequireHomeOwner]
    [Authorize(Policy = PermissionNames.FamilyWrite)]
    [HttpPost("knowledge")]
    public async Task<ActionResult<ApiResponse<object>>> WriteKnowledge(long homeId, FamilyKnowledgeWriteRequest request) =>
        ToResponse(await WithUserAsync((user, token) => _knowledge.WriteAsync(homeId, user.UserId, request, token)));

    /// <summary>软删除一条家庭知识，写入审计。</summary>
    /// <remarks>权限：<c>family.write</c>。</remarks>
    /// <param name="homeId">家庭主键。</param>
    /// <param name="knowledgeId">目标知识主键。</param>
    /// <returns>删除成功返回 200；不存在返回 404。</returns>
    [RequireHomeOwner]
    [Authorize(Policy = PermissionNames.FamilyWrite)]
    [HttpDelete("knowledge/{knowledgeId:long}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteKnowledge(long homeId, long knowledgeId) =>
        ToResponse(await WithUserAsync((user, token) => _knowledge.DeleteAsync(homeId, user.UserId, knowledgeId, token)));

    // ─── 家庭决策历史 ───

    /// <summary>列出指定家庭的决策历史，支持游标分页。</summary>
    /// <remarks>权限：<c>family.read</c>。分页参数 limit 上限 50；cursor 由上次响应返回。</remarks>
    /// <param name="homeId">家庭主键。</param>
    /// <param name="memberId">可选成员过滤。</param>
    /// <param name="limit">每页条数，默认 20，上限 50。</param>
    /// <param name="cursor">分页游标，首次请求不传。</param>
    /// <returns>决策列表统一响应与下一页游标。</returns>
    [RequireHomeOwner]
    [Authorize(Policy = PermissionNames.FamilyRead)]
    [HttpGet("decisions")]
    public async Task<ActionResult<ApiResponse<object>>> ListDecisions(long homeId, long? memberId, int limit = 20, string? cursor = null) =>
        ToResponse(await _decisions.ListAsync(homeId, memberId, limit, cursor, HttpContext.RequestAborted));

    /// <summary>记录一条家庭决策，仅追加，不可修改或删除。</summary>
    /// <remarks>权限：<c>family.write</c>。决策写入后审计。</remarks>
    /// <param name="homeId">家庭主键。</param>
    /// <param name="request">决策写入请求体。</param>
    /// <returns>创建成功返回 201 与决策视图。</returns>
    [RequireHomeOwner]
    [Authorize(Policy = PermissionNames.FamilyWrite)]
    [HttpPost("decisions")]
    public async Task<ActionResult<ApiResponse<object>>> RecordDecision(long homeId, FamilyDecisionWriteRequest request) =>
        ToResponse(await WithUserAsync((user, token) => _decisions.RecordAsync(homeId, user.UserId, request, token)));

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
