using System;
using System.Threading;
using System.Threading.Tasks;
using HomeMind.Api.Controllers.Base;
using HomeMind.Api.Services;
using HomeMind.Business.IServices.AI;
using HomeMind.Common.Model.ViewModel.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HomeMind.Api.Controllers.AI;

/// <summary>每日知识条目的列表、添加与停用。Controller 仅处理 HTTP 与鉴权。</summary>
[Authorize]
[Route("api/v1")]
public sealed class KnowledgeItemsController : ApiControllerBase
{
    private readonly IKnowledgeItemServices _items;

    /// <summary>构造知识条目控制器。</summary>
    /// <param name="items">知识条目业务服务。</param>
    public KnowledgeItemsController(IKnowledgeItemServices items) => _items = items;

    /// <summary>列出当前租户启用的知识条目，可按分类筛选。</summary>
    /// <remarks>权限：<c>ai.read</c>。</remarks>
    /// <param name="category">知识分类，可空。</param>
    /// <returns>知识条目列表的统一响应。</returns>
    [Authorize(Policy = PermissionNames.AiRead)]
    [HttpGet("knowledge-items")]
    public async Task<ActionResult<ApiResponse<object>>> List(string? category) =>
        ToResponse(await WithUserAsync((user, token) => _items.ListAsync(user.UserId, user.TenantId, category, token)));

    /// <summary>新增一条知识条目（用户主动传入入口）。</summary>
    /// <remarks>权限：<c>ai.run</c>。</remarks>
    /// <param name="request">知识条目请求体。</param>
    /// <returns>新建条目统一响应。</returns>
    [Authorize(Policy = PermissionNames.AiRun)]
    [HttpPost("knowledge-items")]
    public async Task<ActionResult<ApiResponse<object>>> Create(KnowledgeItemRequest request) =>
        ToResponse(await WithUserAsync((user, token) => _items.CreateAsync(user.UserId, user.TenantId, request, token)));

    /// <summary>停用一条知识条目。</summary>
    /// <remarks>权限：<c>ai.run</c>。</remarks>
    /// <param name="id">条目主键。</param>
    /// <returns>停用结果统一响应。</returns>
    [Authorize(Policy = PermissionNames.AiRun)]
    [HttpDelete("knowledge-items/{id:long}")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id) =>
        ToResponse(await WithUserAsync((user, token) => _items.DeleteAsync(user.UserId, user.TenantId, id, token)));

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
