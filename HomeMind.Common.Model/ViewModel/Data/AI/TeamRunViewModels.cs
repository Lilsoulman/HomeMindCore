namespace HomeMind.Common.Model.ViewModel.Data.AI;

/// <summary>创建团队运行的请求。客户端必须显式声明 teamVersion、mode 与成员 ExpertVersion；不得携带任意 Prompt 或供应商参数。</summary>
/// <param name="TeamVersion">团队协议版本号，V1 时为"1"。</param>
/// <param name="Mode">运行模式，可选"sequential""parallel""synthesis"。</param>
/// <param name="ParentAgentRunId">触发本次团队运行的父 AgentRun 主键。</param>
/// <param name="Members">成员定义列表，每个成员绑定一个已发布的 ExpertVersion。</param>
/// <param name="FileIds">要附件到团队运行的文件主键列表。</param>
/// <param name="IdempotencyKey">幂等键，可为空。</param>
public sealed record TeamRunCreateRequest(
    string TeamVersion,
    string Mode,
    long ParentAgentRunId,
    IReadOnlyList<TeamRunMemberRequest> Members,
    IReadOnlyList<long> FileIds,
    string? IdempotencyKey);

/// <summary>团队成员引用：固定为已发布的 ExpertVersion；权限交集由服务端计算并冻结。</summary>
/// <param name="ExpertVersionId">所引用的已发布专家版本主键。</param>
/// <param name="DisplayName">成员在前端展示的名称。</param>
/// <param name="StageOrder">执行阶段序号，越小越靠前。</param>
public sealed record TeamRunMemberRequest(
    long ExpertVersionId,
    string DisplayName,
    int StageOrder);

/// <summary>团队运行详情视图，不返回成员 Prompt、模型思维链或厂商日志。</summary>
/// <param name="Id">团队运行主键。</param>
/// <param name="Status">运行状态。</param>
/// <param name="Mode">运行模式。</param>
/// <param name="TeamVersion">团队协议版本号。</param>
/// <param name="ParentAgentRunId">父 AgentRun 主键。</param>
/// <param name="CreatedAt">创建时间（UTC）。</param>
/// <param name="UpdatedAt">更新时间（UTC）。</param>
/// <param name="RowVersion">乐观锁版本号。</param>
public sealed record TeamRunSummary(
    long Id,
    string Status,
    string Mode,
    string TeamVersion,
    long ParentAgentRunId,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    long RowVersion);

/// <summary>成员视图：仅展示名、状态、阶段序号、关联子 Run、错误码与权限交集摘要。</summary>
/// <param name="Id">成员主键。</param>
/// <param name="DisplayName">成员展示名。</param>
/// <param name="StageOrder">阶段序号。</param>
/// <param name="ExpertVersionId">冻结的专家版本主键。</param>
/// <param name="ChildAgentRunId">子 AgentRun 主键，可为空。</param>
/// <param name="Status">成员状态。</param>
/// <param name="LastErrorCode">最近一次失败的错误码，可为空。</param>
/// <param name="PermissionIntersectionSummary">权限交集的展示摘要，逗号分隔的作用域名。</param>
public sealed record TeamRunMemberSummary(
    long Id,
    string DisplayName,
    int StageOrder,
    long ExpertVersionId,
    long? ChildAgentRunId,
    string Status,
    string? LastErrorCode,
    string PermissionIntersectionSummary);

/// <summary>面向 UI 的团队运行事件载荷，仅含显示字段。</summary>
/// <param name="Id">事件主键。</param>
/// <param name="EventType">事件类型。</param>
/// <param name="DisplayPayload">展示安全的负载 JSON 字符串。</param>
/// <param name="CreatedAt">事件创建时间（UTC）。</param>
public sealed record TeamRunEvent(
    long Id,
    string EventType,
    string DisplayPayload,
    DateTime CreatedAt);

/// <summary>团队运行聚合结果，仅含展示字段，不含成员级中间输出。</summary>
/// <param name="TeamRunId">团队运行主键。</param>
/// <param name="Status">运行状态。</param>
/// <param name="Summary">聚合摘要文本。</param>
/// <param name="Highlights">聚合高亮条目列表。</param>
/// <param name="CompletedAt">完成时间（UTC），可为空。</param>
public sealed record TeamRunSynthesis(
    long TeamRunId,
    string Status,
    string Summary,
    IReadOnlyList<string> Highlights,
    DateTime? CompletedAt);
