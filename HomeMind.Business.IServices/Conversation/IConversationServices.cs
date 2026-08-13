using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.AI;

namespace HomeMind.Business.IServices.Conversation;

/// <summary>
/// 专家会话服务：会话 CRUD、消息历史与发送准备。
/// 会话与消息按 <c>tenant_id + owner_user_id</c> 隔离，跨用户/跨租户资源一律 404。
/// 发送消息不在此服务内创建 Run，由控制器编排 <see cref="AgentRunServices"/> 完成。
/// </summary>
public interface IConversationServices
{
    /// <summary>按最近更新时间倒序列出本人未删除的会话。</summary>
    /// <param name="userId">当前用户标识。</param>
    /// <param name="tenantId">当前租户标识。</param>
    /// <param name="limit">每页条数，钳制在 1-50，默认 20。</param>
    /// <param name="cursor">上次响应返回的游标，可空表示第一页。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>200 + <see cref="ConversationListView"/>。</returns>
    Task<ServiceResult> ListAsync(long userId, long tenantId, int limit, string? cursor, CancellationToken cancellationToken = default, long? expertId = null, string? expertCode = null);

    /// <summary>创建会话；绑定专家时校验可见性并解析最新已发布版本。</summary>
    /// <param name="userId">当前用户标识。</param>
    /// <param name="tenantId">当前租户标识。</param>
    /// <param name="request">创建请求。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>201 + <see cref="ConversationView"/>；专家/连接器不可见 404，标题非法 422。</returns>
    Task<ServiceResult> CreateAsync(long userId, long tenantId, ConversationCreateRequest request, CancellationToken cancellationToken = default);

    /// <summary>查询本人会话详情。</summary>
    /// <param name="userId">当前用户标识。</param>
    /// <param name="tenantId">当前租户标识。</param>
    /// <param name="conversationId">会话主键。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>200 + <see cref="ConversationView"/>；非本人/已软删 404。</returns>
    Task<ServiceResult> GetAsync(long userId, long tenantId, long conversationId, CancellationToken cancellationToken = default);

    /// <summary>全量更新会话（重命名/重绑专家与连接器），乐观锁冲突返回 409。</summary>
    /// <param name="userId">当前用户标识。</param>
    /// <param name="tenantId">当前租户标识。</param>
    /// <param name="conversationId">会话主键。</param>
    /// <param name="request">更新请求，携带 RowVersion。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>200 + <see cref="ConversationView"/>；非本人 404，RowVersion 不符 409/40903。</returns>
    Task<ServiceResult> UpdateAsync(long userId, long tenantId, long conversationId, ConversationUpdateRequest request, CancellationToken cancellationToken = default);

    /// <summary>软删除本人会话；消息历史保留留档。</summary>
    /// <param name="userId">当前用户标识。</param>
    /// <param name="tenantId">当前租户标识。</param>
    /// <param name="conversationId">会话主键。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>200；非本人/已软删 404。</returns>
    Task<ServiceResult> DeleteAsync(long userId, long tenantId, long conversationId, CancellationToken cancellationToken = default);

    /// <summary>按主键倒序分页列出会话消息。</summary>
    /// <param name="userId">当前用户标识。</param>
    /// <param name="tenantId">当前租户标识。</param>
    /// <param name="conversationId">会话主键。</param>
    /// <param name="limit">每页条数，钳制在 1-50，默认 20。</param>
    /// <param name="cursor">上次响应返回的游标，可空表示第一页。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>200 + <see cref="ConversationMessageListView"/>；非本人 404。</returns>
    Task<ServiceResult> ListMessagesAsync(long userId, long tenantId, long conversationId, int limit, string? cursor, CancellationToken cancellationToken = default);

    /// <summary>发送前的上下文准备：校验归属与专家绑定，按会话历史拼接输入上下文。</summary>
    /// <param name="userId">当前用户标识。</param>
    /// <param name="tenantId">当前租户标识。</param>
    /// <param name="conversationId">会话主键。</param>
    /// <param name="content">本次用户输入。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>200 + <see cref="PreparedMessageContext"/>；非本人 404，未绑定专家 422/42200。</returns>
    Task<ServiceResult> PrepareMessageAsync(long userId, long tenantId, long conversationId, string content, CancellationToken cancellationToken = default);

    /// <summary>记录已创建 Run 的 user 消息；按 (conversation_id, run_id) 幂等，重复调用不新增。</summary>
    /// <param name="userId">当前用户标识。</param>
    /// <param name="tenantId">当前租户标识。</param>
    /// <param name="conversationId">会话主键。</param>
    /// <param name="runId">已创建的运行主键。</param>
    /// <param name="content">用户消息内容。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>201 + 消息主键；非本人 404。</returns>
    Task<ServiceResult> RecordUserMessageAsync(long userId, long tenantId, long conversationId, long runId, string content, CancellationToken cancellationToken = default);

    /// <summary>Run 终态后追加 assistant 消息（由 AgentRunProcessor 调用，系统行为不校验 owner）；异常仅记日志不抛出。</summary>
    /// <param name="tenantId">运行所属租户标识。</param>
    /// <param name="conversationId">会话主键。</param>
    /// <param name="runId">运行主键。</param>
    /// <param name="content">展示用摘要内容。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>200；会话不存在时仍返回 200（仅记日志）。</returns>
    Task<ServiceResult> AppendAssistantMessageAsync(long tenantId, long conversationId, long runId, string content, CancellationToken cancellationToken = default);
}
