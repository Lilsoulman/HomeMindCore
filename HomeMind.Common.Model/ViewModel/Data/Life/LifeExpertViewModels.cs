namespace HomeMind.Common.Model.ViewModel.Data.Life;

/// <summary>个人生活专家运行请求。</summary>
/// <param name="Intent">运行意图：recommend（探店翻牌）或 plan（行程规划）。</param>
/// <param name="InputJson">运行输入 JSON：recommend 支持 time/location/taste；plan 支持 destination/days/preferences。</param>
/// <param name="IdempotencyKey">幂等键，可为空；为空时由服务端生成。</param>
public sealed record LifeExpertRunRequest(string Intent, string InputJson, string? IdempotencyKey);

/// <summary>翻牌建议项视图。</summary>
/// <param name="FavoriteId">来源收藏主键。</param>
/// <param name="Name">店铺名称。</param>
/// <param name="Reason">推荐理由，包含口味/位置/时段匹配说明。</param>
/// <param name="Tags">命中标签，可为空。</param>
public sealed record LifeExpertRecommendationView(long FavoriteId, string Name, string Reason, IReadOnlyList<string> Tags);

/// <summary>个人生活专家运行视图；只展示展示安全字段，不包含提示或模型思考链。</summary>
/// <param name="Id">运行主键。</param>
/// <param name="Status">运行状态。</param>
/// <param name="ResultSummary">结果摘要，可为空。</param>
/// <param name="CreatedAt">创建时间（UTC）。</param>
/// <param name="FinishedAt">完成时间（UTC），可为空。</param>
/// <param name="Events">运行事件时间线。</param>
/// <param name="Recommendations">翻牌建议，可为空。</param>
/// <param name="Actions">待确认动作，可为空。</param>
public sealed record LifeExpertRunView(long Id, string Status, string? ResultSummary, DateTime CreatedAt, DateTime? FinishedAt, IReadOnlyList<LifeExpertRunEventView> Events, IReadOnlyList<LifeExpertRecommendationView>? Recommendations = null, IReadOnlyList<LifeExpertActionView>? Actions = null);

/// <summary>个人生活专家运行事件视图。</summary>
/// <param name="Sequence">事件序号。</param>
/// <param name="Type">事件类型。</param>
/// <param name="Message">事件说明。</param>
/// <param name="CreatedAt">事件时间（UTC）。</param>
public sealed record LifeExpertRunEventView(int Sequence, string Type, string Message, DateTime CreatedAt);

/// <summary>个人生活专家动作视图（行程同步日历，B17 发布）。</summary>
/// <param name="Id">动作主键。</param>
/// <param name="ActionType">动作类型，calendar_create_event。</param>
/// <param name="Status">动作状态。</param>
/// <param name="Title">动作标题。</param>
/// <param name="Description">动作说明。</param>
/// <param name="RiskLevel">风险等级。</param>
public sealed record LifeExpertActionView(long Id, string ActionType, string Status, string Title, string Description, string RiskLevel);

/// <summary>确认行程同步动作的请求体。</summary>
/// <param name="IdempotencyKey">UUID 幂等键，重复提交不产生重复日历事件。</param>
public sealed record ConfirmLifeExpertActionRequest(string IdempotencyKey);
