namespace HomeMind.Common.Model.ViewModel.Data.AI;

/// <summary>由 Expert 或 Expert Group 发起的 AgentRun 请求。</summary>
/// <param name="SourceType">来源类型，例如"expert"或"expert_group"。</param>
/// <param name="SourceId">来源主键，对应 Expert 或 ExpertGroup 主键。</param>
/// <param name="InputJson">运行输入负载 JSON 字符串。</param>
/// <param name="IdempotencyKey">幂等键，可为空；为空时由服务端生成。</param>
/// <param name="ConversationId">所属专家会话主键（B20 起，可空表示非会话运行）。</param>
public sealed record AgentRunCreateRequest(string SourceType, long SourceId, string InputJson, string? IdempotencyKey, long? ConversationId = null);

/// <summary>由 AgentRun 生成并交由 Skill 执行的受控行动。</summary>
/// <param name="ActionType">动作类型，例如"smart_home_device"。</param>
/// <param name="RequestJson">动作请求负载 JSON 字符串，可为空表示无附加参数。</param>
/// <param name="IdempotencyKey">幂等键，避免重复触发同一动作。</param>
public sealed record AgentRunActionRequest(string ActionType, string? RequestJson, string? IdempotencyKey);

/// <summary>AgentRun 对外视图；B20 起取代匿名投影，字段形状与既有响应保持一致。</summary>
/// <param name="Id">运行主键。</param>
/// <param name="SourceType">运行来源类型。</param>
/// <param name="Status">运行状态。</param>
/// <param name="Input">输入负载 JSON。</param>
/// <param name="Result">结果负载 JSON，可空。</param>
/// <param name="ResultSummary">面向用户的结果摘要，可空。</param>
/// <param name="EstimatedCredits">预估积分。</param>
/// <param name="ActualCredits">实际扣减积分。</param>
/// <param name="CreatedAt">创建时间。</param>
/// <param name="StartedAt">实际开始时间，可空。</param>
/// <param name="FinishedAt">结束时间，可空。</param>
/// <param name="ConversationId">所属专家会话主键，可空表示非会话运行。</param>
public sealed record AgentRunView(
    long Id,
    string SourceType,
    string Status,
    string Input,
    string? Result,
    string? ResultSummary,
    decimal EstimatedCredits,
    decimal ActualCredits,
    DateTime CreatedAt,
    DateTime? StartedAt,
    DateTime? FinishedAt,
    long? ConversationId);
