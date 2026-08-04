namespace HomeMind.Common.Model.ViewModel.Data.AI;

/// <summary>由 Expert 或 Expert Group 发起的 AgentRun 请求。</summary>
public sealed record AgentRunCreateRequest(string SourceType, long SourceId, string InputJson, string? IdempotencyKey);

/// <summary>由 AgentRun 生成并交由 Skill 执行的受控行动。</summary>
public sealed record AgentRunActionRequest(string ActionType, string? RequestJson, string? IdempotencyKey);
