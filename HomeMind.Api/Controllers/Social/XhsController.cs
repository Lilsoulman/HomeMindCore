using System;
using System.Threading;
using System.Threading.Tasks;
using HomeMind.Api.Controllers.Base;
using HomeMind.Api.Services;
using HomeMind.Business.IServices.Connector;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.Connectors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HomeMind.Api.Controllers.Social;

/// <summary>小红书（xhs）个人级 Connector 工具执行：笔记搜索与详情（只读 L1）、登录状态查询与笔记发布（L2 确认后执行）；不返回凭据引用、cookie 或 MCP 内部路径。</summary>
[Authorize]
[Route("api/v1/connector-providers/xhs")]
public sealed class XhsController : ApiControllerBase
{
    private readonly IXhsConnectorServices _xhs;
    private readonly IXhsPublishServices _publish;

    /// <summary>构造小红书连接器控制器。</summary>
    /// <param name="xhs">小红书连接器工具执行服务。</param>
    /// <param name="publish">小红书笔记发布服务（L2 确认链路）。</param>
    public XhsController(IXhsConnectorServices xhs, IXhsPublishServices publish)
    {
        _xhs = xhs;
        _publish = publish;
    }

    /// <summary>按关键词搜索小红书笔记；连接器未授权返回 404。</summary>
    /// <remarks>权限：<c>connector.read</c>。只读 L1 操作；响应不含登录态与凭据。</remarks>
    /// <param name="query">搜索关键词，必填。</param>
    /// <param name="limit">返回条数上限（1-50），默认 10。</param>
    /// <returns>笔记摘要列表统一响应。</returns>
    [Authorize(Policy = PermissionNames.ConnectorRead)]
    [HttpGet("notes/search")]
    public async Task<ActionResult<ApiResponse<object>>> SearchNotes([FromQuery] string query, [FromQuery] int limit = 0) =>
        ToResponse(await WithUserAsync((user, token) => _xhs.SearchNotesAsync(user.UserId, user.TenantId, query, limit, token)));

    /// <summary>获取小红书笔记详情；连接器未授权返回 404。</summary>
    /// <remarks>权限：<c>connector.read</c>。只读 L1 操作；响应不含登录态与凭据。</remarks>
    /// <param name="url">笔记链接，必填。</param>
    /// <returns>笔记详情统一响应。</returns>
    [Authorize(Policy = PermissionNames.ConnectorRead)]
    [HttpGet("notes/detail")]
    public async Task<ActionResult<ApiResponse<object>>> GetNoteDetail([FromQuery] string url) =>
        ToResponse(await WithUserAsync((user, token) => _xhs.GetNoteDetailAsync(user.UserId, user.TenantId, url, token)));

    /// <summary>查询本人小红书连接器登录状态；连接器未授权返回 404。</summary>
    /// <remarks>权限：<c>connector.read</c>。响应不含登录态明文或凭据。</remarks>
    /// <returns>登录状态统一响应。</returns>
    [Authorize(Policy = PermissionNames.ConnectorRead)]
    [HttpGet("auth-status")]
    public async Task<ActionResult<ApiResponse<object>>> GetAuthStatus() =>
        ToResponse(await WithUserAsync((user, token) => _xhs.GetAuthStatusAsync(user.UserId, user.TenantId, token)));

    /// <summary>创建小红书笔记发布动作（L2，等待确认）；连接器未授权返回 404。</summary>
    /// <remarks>权限：<c>ai.run</c> + <c>connector.write</c>。发布为对外动作，经确认中心逐项确认后执行；成功 201 返回动作视图。</remarks>
    /// <param name="request">发布请求体（类型/标题/正文/媒体路径/标签与可选幂等键）。</param>
    /// <returns>发布动作视图统一响应。</returns>
    [Authorize(Policy = PermissionNames.AiRun)]
    [Authorize(Policy = PermissionNames.ConnectorWrite)]
    [HttpPost("notes/publish")]
    public async Task<ActionResult<ApiResponse<object>>> CreatePublish(XhsPublishRequest request) =>
        ToResponse(await WithUserAsync((user, token) => _publish.CreateAsync(user.UserId, user.TenantId, request, token)));

    /// <summary>确认并执行小红书发布动作；同键重复确认重放首次结果。</summary>
    /// <remarks>权限：<c>ai.run</c> + <c>connector.write</c>。非法幂等键 422/动作不存在或非本人 404/已终态换键 409/发布失败 502。</remarks>
    /// <param name="actionId">发布动作主键。</param>
    /// <param name="request">确认请求体（UUID 幂等键必填）。</param>
    /// <returns>发布执行结果统一响应。</returns>
    [Authorize(Policy = PermissionNames.AiRun)]
    [Authorize(Policy = PermissionNames.ConnectorWrite)]
    [HttpPost("publish-actions/{actionId:long}/confirm")]
    public async Task<ActionResult<ApiResponse<object>>> ConfirmPublish(long actionId, XhsPublishConfirmRequest request) =>
        ToResponse(await WithUserAsync((user, token) => _publish.ConfirmActionAsync(user.UserId, user.TenantId, actionId, request, token)));

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
