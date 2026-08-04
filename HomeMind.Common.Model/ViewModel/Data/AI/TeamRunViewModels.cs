namespace HomeMind.Common.Model.ViewModel.Data.AI;

/// <summary>创建团队运行的请求。客户端必须显式声明 teamVersion、mode 与成员 ExpertVersion；不得携带任意 Prompt 或供应商参数。</summary>
public sealed record TeamRunCreateRequest(
    string TeamVersion,
    string Mode,
    long ParentAgentRunId,
    IReadOnlyList<TeamRunMemberRequest> Members,
    IReadOnlyList<long> FileIds,
    string? IdempotencyKey);

/// <summary>团队成员引用：固定为已发布的 ExpertVersion；权限交集由服务端计算并冻结。</summary>
public sealed record TeamRunMemberRequest(
    long ExpertVersionId,
    string DisplayName,
    int StageOrder);

/// <summary>团队运行详情视图，不返回成员 Prompt、模型思维链或厂商日志。</summary>
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
public sealed record TeamRunEvent(
    long Id,
    string EventType,
    string DisplayPayload,
    DateTime CreatedAt);

/// <summary>团队运行聚合结果，仅含展示字段，不含成员级中间输出。</summary>
public sealed record TeamRunSynthesis(
    long TeamRunId,
    string Status,
    string Summary,
    IReadOnlyList<string> Highlights,
    DateTime? CompletedAt);
