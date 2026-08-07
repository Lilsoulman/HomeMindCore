using System;
using System.Threading;
using System.Threading.Tasks;
using HomeMind.Api.Controllers.Base;
using HomeMind.Api.Services;
using HomeMind.Business.IServices.Agent;
using HomeMind.Business.IServices.Conversation;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.AI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HomeMind.Api.Controllers.AI;

/// <summary>
/// 专家会话（对话框）控制器：会话 CRUD、消息历史与发送。
/// 会话为个人资源，租户与所有者均由 JWT 推导，归属校验在服务层完成，跨用户/跨租户一律 404。
/// 发送消息时服务层拼接会话上下文后复用 AgentRun 链路创建运行，终态由后台处理器追加 assistant 消息。
/// </summary>
[Authorize]
[Route("api/v1/conversations")]
public sealed class ConversationsController : ApiControllerBase
{
    private readonly IConversationServices _conversations;
    private readonly IAgentRunServices _agentRuns;

    /// <summary>构造会话控制器。</summary>
    /// <param name="conversations">专家会话业务服务。</param>
    /// <param name="agentRuns">AgentRun 业务服务。</param>
    public ConversationsController(IConversationServices conversations, IAgentRunServices agentRuns)
    {
        _conversations = conversations;
        _agentRuns = agentRuns;
    }

    /// <summary>按最近更新时间倒序列出本人未删除的会话。</summary>
    /// <remarks>权限：<c>conversation.read</c>。仅返回当前用户自己的会话。</remarks>
    /// <param name="limit">每页条数，钳制在 1-50，默认 20。</param>
    /// <param name="cursor">上次响应返回的游标，可空表示第一页。</param>
    /// <returns>会话列表的统一响应。</returns>
    [Authorize(Policy = PermissionNames.ConversationRead)]
    [HttpGet("")]
    public async Task<ActionResult<ApiResponse<object>>> ListConversations(int limit = 20, string? cursor = null) =>
        ToResponse(await WithUserAsync((user, token) => _conversations.ListAsync(user.UserId, user.TenantId, limit, cursor, token)));

    /// <summary>创建会话；可绑定专家与连接器，均非必填。</summary>
    /// <remarks>权限：<c>conversation.write</c>。绑定专家时校验可见性并解析最新已发布版本；连接器仅校验租户归属。</remarks>
    /// <param name="request">会话创建请求体。</param>
    /// <returns>已创建会话的统一响应。</returns>
    [Authorize(Policy = PermissionNames.ConversationWrite)]
    [HttpPost("")]
    public async Task<ActionResult<ApiResponse<object>>> CreateConversation(ConversationCreateRequest request) =>
        ToResponse(await WithUserAsync((user, token) => _conversations.CreateAsync(user.UserId, user.TenantId, request, token)));

    /// <summary>查询本人会话详情。</summary>
    /// <remarks>权限：<c>conversation.read</c>。跨用户/跨租户/已软删返回 404。</remarks>
    /// <param name="id">会话主键。</param>
    /// <returns>会话详情的统一响应。</returns>
    [Authorize(Policy = PermissionNames.ConversationRead)]
    [HttpGet("{id:long}")]
    public async Task<ActionResult<ApiResponse<object>>> GetConversation(long id) =>
        ToResponse(await WithUserAsync((user, token) => _conversations.GetAsync(user.UserId, user.TenantId, id, token)));

    /// <summary>全量更新会话（重命名/重绑专家与连接器）。</summary>
    /// <remarks>权限：<c>conversation.write</c>。携带 RowVersion 乐观锁，冲突返回 409/40903。</remarks>
    /// <param name="id">会话主键。</param>
    /// <param name="request">会话更新请求体。</param>
    /// <returns>更新后会话的统一响应。</returns>
    [Authorize(Policy = PermissionNames.ConversationWrite)]
    [HttpPut("{id:long}")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateConversation(long id, ConversationUpdateRequest request) =>
        ToResponse(await WithUserAsync((user, token) => _conversations.UpdateAsync(user.UserId, user.TenantId, id, request, token)));

    /// <summary>软删除本人会话；消息历史保留留档。</summary>
    /// <remarks>权限：<c>conversation.write</c>。重复删除返回 404。</remarks>
    /// <param name="id">会话主键。</param>
    /// <returns>删除结果的统一响应。</returns>
    [Authorize(Policy = PermissionNames.ConversationWrite)]
    [HttpDelete("{id:long}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteConversation(long id) =>
        ToResponse(await WithUserAsync((user, token) => _conversations.DeleteAsync(user.UserId, user.TenantId, id, token)));

    /// <summary>按主键倒序分页列出会话消息。</summary>
    /// <remarks>权限：<c>conversation.read</c>。游标由上次响应返回，非法游标按第一页处理。</remarks>
    /// <param name="id">会话主键。</param>
    /// <param name="limit">每页条数，钳制在 1-50，默认 20。</param>
    /// <param name="cursor">上次响应返回的游标，可空表示第一页。</param>
    /// <returns>消息列表的统一响应。</returns>
    [Authorize(Policy = PermissionNames.ConversationRead)]
    [HttpGet("{id:long}/messages")]
    public async Task<ActionResult<ApiResponse<object>>> ListMessages(long id, int limit = 20, string? cursor = null) =>
        ToResponse(await WithUserAsync((user, token) => _conversations.ListMessagesAsync(user.UserId, user.TenantId, id, limit, cursor, token)));

    /// <summary>发送一条消息：拼接会话上下文创建关联的 Expert Run 并落库 user 消息。</summary>
    /// <remarks>
    /// 权限：<c>conversation.write</c>。未绑定专家返回 422/42200；重复的幂等键重放既有运行（200）。
    /// 响应 Data 为 <c>{"RunId":..,"Status":"queued","MessageId":..}</c>，客户端应轮询运行状态，
    /// 终态后由后台处理器追加 assistant 消息。
    /// </remarks>
    /// <param name="id">会话主键。</param>
    /// <param name="request">消息发送请求体。</param>
    /// <returns>发送结果的统一响应（201 新建运行 / 200 幂等重放）。</returns>
    [Authorize(Policy = PermissionNames.ConversationWrite)]
    [HttpPost("{id:long}/messages")]
    public async Task<ActionResult<ApiResponse<object>>> SendMessage(long id, ConversationMessageSendRequest request)
    {
        if (!TryGetUser(out var user))
            return ToResponse(new ServiceResult(401, "未提供访问令牌，或访问令牌已过期。"));
        var token = HttpContext.RequestAborted;

        var prepared = await _conversations.PrepareMessageAsync(user.UserId, user.TenantId, id, request.Content, token);
        if (!prepared.Succeeded) return ToResponse(prepared);
        var context = (PreparedMessageContext)prepared.Data!;

        var run = await _agentRuns.CreateAsync(user.UserId, user.TenantId,
            new AgentRunCreateRequest("expert", context.ExpertId, context.InputJson, request.IdempotencyKey, id), token);
        if (!run.Succeeded) return ToResponse(run);
        var runView = (AgentRunView)run.Data!;

        var recorded = await _conversations.RecordUserMessageAsync(user.UserId, user.TenantId, id, runView.Id, request.Content, token);
        var messageId = recorded.Data is long value ? value : 0;

        return new ObjectResult(new ApiResponse<object>(0, run.Message, new ConversationSendResultView(runView.Id, runView.Status, messageId)))
        { StatusCode = run.StatusCode };
    }

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
