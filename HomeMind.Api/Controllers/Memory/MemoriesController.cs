using System;
using System.Threading;
using System.Threading.Tasks;
using HomeMind.Api.Controllers.Base;
using HomeMind.Api.Services;
using HomeMind.Business.IServices.Memory;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.Memory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HomeMind.Api.Controllers.Memory;

/// <summary>学习记忆库与记忆候选审核 API；家庭作用域始终由 JWT 推导。</summary>
[Authorize]
[Route("api/v1")]
public sealed class MemoriesController : ApiControllerBase
{
    private readonly IMemoryCandidateServices _candidates;
    private readonly ILearningMemoryServices _memories;

    /// <summary>构造学习记忆控制器。</summary>
    public MemoriesController(IMemoryCandidateServices candidates, ILearningMemoryServices memories)
    {
        _candidates = candidates;
        _memories = memories;
    }

    /// <summary>列出当前用户可审核的记忆候选。</summary>
    /// <remarks>权限：<c>memory.read</c>；个人候选仅本人可见。</remarks>
    [Authorize(Policy = PermissionNames.MemoryRead)]
    [HttpGet("memory-candidates")]
    public async Task<ActionResult<ApiResponse<object>>> ListCandidates(string? scope, string? status) =>
        ToResponse(await WithUserAsync((user, token) => _candidates.ListAsync(user.TenantId, user.UserId, scope, status, token)));

    /// <summary>接受一条待审核候选，并原子写入事实源和学习记忆投影。</summary>
    /// <remarks>权限：<c>memory.write</c>；重复接受返回已有学习记忆，不重复写入。</remarks>
    [Authorize(Policy = PermissionNames.MemoryWrite)]
    [HttpPost("memory-candidates/{candidateId:long}/accept")]
    public async Task<ActionResult<ApiResponse<object>>> AcceptCandidate(long candidateId, ResolveMemoryCandidateRequest request) =>
        ToResponse(await WithUserAsync((user, token) => _candidates.AcceptAsync(user.TenantId, user.UserId, candidateId, request, token)));

    /// <summary>拒绝一条待审核候选。</summary>
    /// <remarks>权限：<c>memory.write</c>；重复拒绝安全重放。</remarks>
    [Authorize(Policy = PermissionNames.MemoryWrite)]
    [HttpPost("memory-candidates/{candidateId:long}/reject")]
    public async Task<ActionResult<ApiResponse<object>>> RejectCandidate(long candidateId) =>
        ToResponse(await WithUserAsync((user, token) => _candidates.RejectAsync(user.TenantId, user.UserId, candidateId, token)));

    /// <summary>游标分页查询当前用户可见的学习记忆库。</summary>
    /// <remarks>权限：<c>memory.read</c>；响应不含候选原始证据、会话正文或 Prompt。</remarks>
    [Authorize(Policy = PermissionNames.MemoryRead)]
    [HttpGet("memories")]
    public async Task<ActionResult<ApiResponse<object>>> ListMemories(string? scope, string? kind, string? status, string? query, int limit = 20, string? cursor = null) =>
        ToResponse(await WithUserAsync((user, token) => _memories.ListAsync(user.TenantId, user.UserId, scope, kind, status, query, limit, cursor, token)));

    /// <summary>查询一条当前用户可见的学习记忆详情。</summary>
    /// <remarks>权限：<c>memory.read</c>；跨家庭或跨成员个人记忆返回 404。</remarks>
    [Authorize(Policy = PermissionNames.MemoryRead)]
    [HttpGet("memories/{memoryId:long}")]
    public async Task<ActionResult<ApiResponse<object>>> GetMemory(long memoryId) =>
        ToResponse(await WithUserAsync((user, token) => _memories.GetAsync(user.TenantId, user.UserId, memoryId, token)));

    /// <summary>在用户上下文就绪时执行服务回调。</summary>
    private async Task<ServiceResult> WithUserAsync(Func<UserContext, CancellationToken, Task<ServiceResult>> action) => TryGetUser(out var user)
        ? await action(user, HttpContext.RequestAborted)
        : new ServiceResult(401, "未提供访问令牌，或访问令牌已过期。");

    /// <summary>转换业务服务结果为统一 HTTP 响应。</summary>
    private static ActionResult<ApiResponse<object>> ToResponse(ServiceResult result) => result.Succeeded
        ? new ObjectResult(new ApiResponse<object>(0, result.Message, result.Data)) { StatusCode = result.StatusCode }
        : new ObjectResult(ApiResponse<object>.Fail(result.Code, result.Message)) { StatusCode = result.StatusCode };
}
