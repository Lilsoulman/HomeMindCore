namespace HomeMind.Common.Model.ViewModel.Data.AI;

/// <summary>创建家庭管家运行（兼容旧 housekeeper-runs 路由）的请求。</summary>
/// <param name="Intent">意图，可选值为"arrive""away""sleep""environment_review"。</param>
/// <param name="SpaceId">可选的目标空间主键，限制分析范围。</param>
/// <param name="IdempotencyKey">幂等键，避免重复创建运行。</param>
public sealed record HousekeeperRunRequest(string Intent, long? SpaceId, string? IdempotencyKey);

/// <summary>确认一个待执行管家动作的请求。</summary>
/// <param name="IdempotencyKey">幂等键，必填 UUID 字符串。</param>
public sealed record ConfirmHousekeeperActionRequest(string IdempotencyKey);

/// <summary>管家运行事件视图，仅展示字段。</summary>
/// <param name="Sequence">事件序号。</param>
/// <param name="Type">事件类型。</param>
/// <param name="Message">事件展示消息。</param>
/// <param name="CreatedAt">事件创建时间（UTC）。</param>
public sealed record HousekeeperRunEventView(int Sequence, string Type, string Message, DateTime CreatedAt);

/// <summary>管家运行动作视图，展示安全的待执行动作。</summary>
/// <param name="Id">动作主键。</param>
/// <param name="ActionType">动作类型。</param>
/// <param name="Status">动作状态。</param>
/// <param name="Title">动作标题。</param>
/// <param name="Description">动作描述。</param>
/// <param name="DeviceId">归一化设备主键，非设备类动作为 0。</param>
/// <param name="DeviceName">归一化设备名。</param>
/// <param name="Capability">目标能力名。</param>
/// <param name="TargetValue">目标值对象。</param>
/// <param name="Segments">剪辑方案片段序列（B30，draft_generate 动作输出，供 Web 渲染方案时间线）。</param>
/// <param name="Audio">剪辑方案配乐信息，当前方案为 null。</param>
/// <param name="TotalDuration">剪辑方案总时长（秒），无方案时为空。</param>
public sealed record HousekeeperRunActionView(
    long Id,
    string ActionType,
    string Status,
    string Title,
    string Description,
    long DeviceId,
    string DeviceName,
    string Capability,
    object TargetValue,
    IReadOnlyList<SkillPlanSegmentView>? Segments = null,
    object? Audio = null,
    int? TotalDuration = null);

/// <summary>管家运行汇总视图。</summary>
/// <param name="Id">运行主键。</param>
/// <param name="Status">运行状态。</param>
/// <param name="ResultSummary">结果摘要，可为空。</param>
/// <param name="CreatedAt">创建时间（UTC）。</param>
/// <param name="FinishedAt">结束时间（UTC）。</param>
/// <param name="Events">运行事件列表。</param>
/// <param name="Actions">运行动作列表。</param>
/// <param name="Mode">运行模式，可为空。</param>
/// <param name="AutoConfirmPolicy">自动确认策略，可为空。</param>
public sealed record HousekeeperRunView(
    long Id,
    string Status,
    string? ResultSummary,
    DateTime CreatedAt,
    DateTime? FinishedAt,
    IReadOnlyList<HousekeeperRunEventView> Events,
    IReadOnlyList<HousekeeperRunActionView> Actions,
    string? Mode = null,
    string? AutoConfirmPolicy = null);

/// <summary>管家动作执行结果视图。</summary>
/// <param name="ActionId">动作主键。</param>
/// <param name="Status">执行状态。</param>
/// <param name="Message">可读消息。</param>
/// <param name="UpdatedAt">更新时间（UTC）。</param>
public sealed record HousekeeperActionExecutionView(long ActionId, string Status, string Message, DateTime UpdatedAt);
