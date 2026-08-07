using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace HomeMind.Common.Model.ViewModel.Data.AI;

/// <summary>专家会话视图；会话为个人资源，仅所有者本人可见。</summary>
public sealed record ConversationView(
    long Id,
    string Title,
    long? ExpertId,
    long? ExpertVersionId,
    long? WorkspaceConnectorId,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    long RowVersion);

/// <summary>专家会话列表分页视图；按最近更新时间倒序，cursor 由上次响应返回。</summary>
public sealed record ConversationListView(
    IReadOnlyList<ConversationView> Items,
    string? Cursor);

/// <summary>会话内对话消息视图；不包含 Prompt 或模型思考链。</summary>
public sealed record ConversationMessageView(
    long Id,
    string Role,
    string Content,
    long? RunId,
    DateTime CreatedAt);

/// <summary>会话消息列表分页视图；按主键倒序，cursor 由上次响应返回。</summary>
public sealed record ConversationMessageListView(
    IReadOnlyList<ConversationMessageView> Items,
    string? Cursor);

/// <summary>发送消息前的服务端准备结果；承载已解析的专家与拼接好的上下文 JSON，供控制器编排 Run 创建。</summary>
public sealed record PreparedMessageContext(
    long ExpertId,
    string InputJson);

/// <summary>专家会话创建请求；专家与连接器均可选，未绑定专家时仅能维护会话，发送消息返回 422。</summary>
public sealed class ConversationCreateRequest
{
    /// <summary>会话标题。</summary>
    [Required, StringLength(64), Description("会话标题，最长 64 字符。")]
    public string Title { get; init; } = null!;

    /// <summary>绑定的专家主键；可空表示暂不绑定。</summary>
    [Description("绑定的专家主键；可空表示暂不绑定。")]
    public long? ExpertId { get; init; }

    /// <summary>绑定的连接器实例主键（单值）；可空。</summary>
    [Description("绑定的连接器实例主键（单值）；可空。")]
    public long? WorkspaceConnectorId { get; init; }
}

/// <summary>专家会话更新请求；全量替换语义，ExpertId 传 null 即解绑专家。</summary>
public sealed class ConversationUpdateRequest
{
    /// <summary>会话标题。</summary>
    [Required, StringLength(64), Description("会话标题，最长 64 字符。")]
    public string Title { get; init; } = null!;

    /// <summary>绑定的专家主键；传 null 表示解绑。</summary>
    [Description("绑定的专家主键；传 null 表示解绑。")]
    public long? ExpertId { get; init; }

    /// <summary>绑定的连接器实例主键（单值）；传 null 表示解绑。</summary>
    [Description("绑定的连接器实例主键（单值）；传 null 表示解绑。")]
    public long? WorkspaceConnectorId { get; init; }

    /// <summary>乐观锁版本号；与服务端不一致返回 409。</summary>
    [Required, Description("乐观锁版本号，与服务端不一致返回 409/40903。")]
    public long RowVersion { get; init; }
}

/// <summary>会话消息发送请求；发送后创建关联会话的 Expert Run，客户端应轮询运行状态。</summary>
public sealed class ConversationMessageSendRequest
{
    /// <summary>消息内容，即用户输入。</summary>
    [Required, StringLength(8000), Description("消息内容，最长 8000 字符。")]
    public string Content { get; init; } = null!;

    /// <summary>幂等键（UUID）；可空，为空时由服务端生成。</summary>
    [Description("幂等键（UUID）；可空，为空时由服务端生成。")]
    public string? IdempotencyKey { get; init; }
}

/// <summary>发送消息的响应视图；携带创建的运行主键与已落库的用户消息主键。</summary>
public sealed record ConversationSendResultView(
    long RunId,
    string Status,
    long MessageId);
