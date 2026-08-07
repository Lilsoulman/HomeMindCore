using System.Text.Json;

namespace HomeMind.Common.Model.ViewModel.Data.SmartHome;

/// <summary>平台级场景模板视图；仅返回模板定义，不返回任何设备实例数据。</summary>
/// <param name="Id">模板主键。</param>
/// <param name="Code">模板业务键，全局唯一。</param>
/// <param name="Name">模板展示名。</param>
/// <param name="Summary">模板摘要，可为空。</param>
/// <param name="Status">模板状态。</param>
/// <param name="Steps">模板步骤列表（未解析设备）。</param>
public sealed record ScenarioTemplateView(long Id, string Code, string Name, string? Summary, string Status, IReadOnlyList<ScenarioTemplateStepView> Steps);

/// <summary>场景模板步骤视图；设备由家庭启用时按 device_type + room + capability 解析。</summary>
/// <param name="Id">步骤标识，模板内唯一，如 step_1。</param>
/// <param name="Name">步骤展示名。</param>
/// <param name="DeviceType">标准化设备类型，如 light / air_conditioner / switch。</param>
/// <param name="Room">目标空间类型，如 bedroom / living_room；"*" 表示不限房间。</param>
/// <param name="Capability">目标能力编码，如 power / temperature。</param>
/// <param name="Value">目标值 JSON，如 false 或 26。</param>
/// <param name="Optional">是否可选；可选步骤失败不阻塞场景，仍计为成功。</param>
public sealed record ScenarioTemplateStepView(string Id, string Name, string DeviceType, string Room, string Capability, JsonElement? Value, bool Optional);

/// <summary>家庭启用的场景实例视图；步骤已解析到具体设备并记录可用性。</summary>
/// <param name="Id">实例主键。</param>
/// <param name="TemplateCode">来源模板业务键。</param>
/// <param name="Name">实例展示名。</param>
/// <param name="Status">实例状态。</param>
/// <param name="Steps">解析后步骤列表。</param>
/// <param name="CreatedAt">启用时间（UTC）。</param>
public sealed record ScenarioInstanceView(long Id, string TemplateCode, string Name, string Status, IReadOnlyList<ScenarioInstanceStepView> Steps, DateTime CreatedAt);

/// <summary>场景实例步骤视图；unavailable 步骤携带原因，执行时跳过。</summary>
/// <param name="Id">步骤标识，模板内唯一。</param>
/// <param name="Name">步骤展示名。</param>
/// <param name="DeviceType">标准化设备类型。</param>
/// <param name="Room">目标空间类型。</param>
/// <param name="DeviceId">解析到的设备主键，unavailable 为空。</param>
/// <param name="Capability">目标能力编码。</param>
/// <param name="Optional">是否可选。</param>
/// <param name="StepStatus">步骤可用性：ready / unavailable。</param>
/// <param name="Reason">unavailable 原因，如 no matching device。</param>
public sealed record ScenarioInstanceStepView(string Id, string Name, string DeviceType, string Room, long? DeviceId, string Capability, bool Optional, string StepStatus, string? Reason);

/// <summary>场景实例运行请求体。</summary>
/// <param name="IdempotencyKey">幂等键，可为空；为空时由服务端生成。</param>
public sealed record ScenarioRunRequest(string? IdempotencyKey);

/// <summary>确认场景运行动作的请求体。</summary>
/// <param name="IdempotencyKey">UUID 幂等键，重复提交只返回首次执行结果。</param>
public sealed record ConfirmScenarioActionRequest(string IdempotencyKey);

/// <summary>场景运行视图；只展示展示安全字段，不包含任何步骤明细的原始 JSON。</summary>
/// <param name="Id">运行主键。</param>
/// <param name="Status">运行生命周期状态。</param>
/// <param name="ResultSummary">结果摘要，可为空。</param>
/// <param name="CreatedAt">创建时间（UTC）。</param>
/// <param name="FinishedAt">完成时间（UTC），可为空。</param>
/// <param name="Events">运行事件时间线。</param>
/// <param name="Actions">待确认动作列表。</param>
public sealed record ScenarioRunView(long Id, string Status, string? ResultSummary, DateTime CreatedAt, DateTime? FinishedAt, IReadOnlyList<ScenarioRunEventView> Events, IReadOnlyList<ScenarioActionView> Actions);

/// <summary>场景运行事件视图。</summary>
/// <param name="Sequence">事件序号。</param>
/// <param name="Type">事件类型。</param>
/// <param name="Message">事件说明。</param>
/// <param name="CreatedAt">事件时间（UTC）。</param>
public sealed record ScenarioRunEventView(int Sequence, string Type, string Message, DateTime CreatedAt);

/// <summary>场景运行动作视图（ActionType=scenario，承载全部步骤）。</summary>
/// <param name="Id">动作主键。</param>
/// <param name="ActionType">动作类型，scenario。</param>
/// <param name="Status">动作状态。</param>
/// <param name="Title">动作标题，如「晚安」。</param>
/// <param name="Description">动作说明。</param>
/// <param name="RiskLevel">场景风险等级，取各步骤风险最大值。</param>
public sealed record ScenarioActionView(long Id, string ActionType, string Status, string Title, string Description, string RiskLevel);
