namespace HomeMind.Common.Model.ViewModel.Data.AI;

/// <summary>Skill 运行创建请求体。</summary>
/// <param name="IdempotencyKey">幂等键，可为空；为空时由服务端生成。</param>
/// <param name="InputJson">Skill 输入参数 JSON，如 {"media_location":"...","instruction":"..."}。</param>
public sealed record SkillRunCreateRequest(string? IdempotencyKey, string InputJson);

/// <summary>确认 Skill 运行动作（draft_generate）的请求体。</summary>
/// <param name="IdempotencyKey">UUID 幂等键，重复提交只返回首次执行结果，不重复登记草稿文件。</param>
public sealed record ConfirmSkillRunActionRequest(string IdempotencyKey);

/// <summary>Skill 运行视图（SourceType=skill）；只展示展示安全字段，不包含素材绝对路径、草稿路径或 Prompt。</summary>
/// <param name="Id">运行主键。</param>
/// <param name="Status">运行生命周期状态。</param>
/// <param name="ResultSummary">结果摘要，可为空。</param>
/// <param name="CreatedAt">创建时间（UTC）。</param>
/// <param name="FinishedAt">完成时间（UTC），可为空。</param>
/// <param name="Events">运行事件时间线。</param>
/// <param name="Actions">待确认动作列表。</param>
public sealed record SkillRunView(long Id, string Status, string? ResultSummary, DateTime CreatedAt, DateTime? FinishedAt, IReadOnlyList<SkillRunEventView> Events, IReadOnlyList<SkillRunActionView> Actions);

/// <summary>Skill 运行事件视图。</summary>
/// <param name="Sequence">事件序号。</param>
/// <param name="Type">事件类型。</param>
/// <param name="Message">事件说明。</param>
/// <param name="CreatedAt">事件时间（UTC）。</param>
public sealed record SkillRunEventView(int Sequence, string Type, string Message, DateTime CreatedAt);

/// <summary>Skill 运行动作视图（ActionType=draft_generate，承载剪辑方案）。</summary>
/// <param name="Id">动作主键。</param>
/// <param name="ActionType">动作类型，draft_generate。</param>
/// <param name="Status">动作状态。</param>
/// <param name="Title">动作标题，如「快速剪辑方案」。</param>
/// <param name="Description">动作说明。</param>
/// <param name="RiskLevel">动作风险等级，快速剪辑为 L1。</param>
public sealed record SkillRunActionView(long Id, string ActionType, string Status, string Title, string Description, string RiskLevel);
