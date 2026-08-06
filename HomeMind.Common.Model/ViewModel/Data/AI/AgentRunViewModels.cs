namespace HomeMind.Common.Model.ViewModel.Data.AI;

/// <summary>由 Expert 或 Expert Group 发起的 AgentRun 请求。</summary>
/// <param name="SourceType">来源类型，例如"expert"或"expert_group"。</param>
/// <param name="SourceId">来源主键，对应 Expert 或 ExpertGroup 主键。</param>
/// <param name="InputJson">运行输入负载 JSON 字符串。</param>
/// <param name="IdempotencyKey">幂等键，可为空；为空时由服务端生成。</param>
public sealed record AgentRunCreateRequest(string SourceType, long SourceId, string InputJson, string? IdempotencyKey);

/// <summary>由 AgentRun 生成并交由 Skill 执行的受控行动。</summary>
/// <param name="ActionType">动作类型，例如"smart_home_device"。</param>
/// <param name="RequestJson">动作请求负载 JSON 字符串，可为空表示无附加参数。</param>
/// <param name="IdempotencyKey">幂等键，避免重复触发同一动作。</param>
public sealed record AgentRunActionRequest(string ActionType, string? RequestJson, string? IdempotencyKey);
