using HomeMind.Common.Model.ViewModel.Data.SmartHome;

namespace HomeMind.Common.Model.ViewModel.Data.Dashboard;

/// <summary>仪表板模块统一外壳，单模块失败不影响其他模块返回。</summary>
/// <typeparam name="T">模块负载类型。</typeparam>
/// <param name="Status">模块状态，参见 <see cref="DashboardModuleStatus"/>。</param>
/// <param name="Data">模块负载数据，可为默认值。</param>
/// <param name="UpdatedAt">模块最近一次成功更新时间（UTC）。</param>
/// <param name="Message">模块可读消息；不可用时填写原因。</param>
public sealed record DashboardModule<T>(string Status, T Data, DateTime? UpdatedAt, string? Message);

/// <summary>仪表板空间摘要。</summary>
/// <param name="Id">空间主键。</param>
/// <param name="Name">空间名称。</param>
/// <param name="SpaceType">空间类型。</param>
/// <param name="Summary">空间摘要，可为空。</param>
/// <param name="DeviceCount">设备总数。</param>
/// <param name="OnlineDeviceCount">在线设备数。</param>
/// <param name="OfflineDeviceCount">离线设备数。</param>
/// <param name="StateUpdatedAt">最近一次状态更新时间（UTC）。</param>
/// <param name="UpdatedAt">空间元数据更新时间（UTC）。</param>
public sealed record DashboardSpaceSummaryView(long Id, string Name, string SpaceType, string? Summary, int DeviceCount, int OnlineDeviceCount, int OfflineDeviceCount, DateTime? StateUpdatedAt, DateTime UpdatedAt);

/// <summary>仪表板 Home 模块聚合数据。</summary>
/// <param name="SpaceCount">空间总数。</param>
/// <param name="DeviceCount">设备总数。</param>
/// <param name="OnlineDeviceCount">在线设备数。</param>
/// <param name="OfflineDeviceCount">离线设备数。</param>
/// <param name="Spaces">空间摘要列表。</param>
public sealed record DashboardHomeView(int SpaceCount, int DeviceCount, int OnlineDeviceCount, int OfflineDeviceCount, IReadOnlyList<DashboardSpaceSummaryView> Spaces);

/// <summary>仪表板待办摘要。</summary>
/// <param name="Id">待办主键。</param>
/// <param name="Title">标题。</param>
/// <param name="Status">状态。</param>
/// <param name="Priority">优先级，可为空。</param>
/// <param name="DueAt">截止时间（UTC）。</param>
/// <param name="Pinned">是否置顶。</param>
/// <param name="UpdatedAt">更新时间（UTC）。</param>
public sealed record DashboardTodoView(long Id, string Title, string Status, string? Priority, DateTime? DueAt, bool Pinned, DateTime UpdatedAt);

/// <summary>仪表板日历摘要。</summary>
/// <param name="Id">事件主键。</param>
/// <param name="Title">事件标题。</param>
/// <param name="StartAt">开始时间（UTC）。</param>
/// <param name="EndAt">结束时间（UTC）。</param>
/// <param name="AllDay">是否全天。</param>
/// <param name="UpdatedAt">更新时间（UTC）。</param>
public sealed record DashboardCalendarEventView(long Id, string Title, DateTime StartAt, DateTime? EndAt, bool AllDay, DateTime UpdatedAt);

/// <summary>仪表板待确认事项摘要；只含未过期且仍待处理的确认项。</summary>
/// <param name="Id">确认项主键。</param>
/// <param name="RiskLevel">风险等级，L1/L2/L3。</param>
/// <param name="Title">确认项标题。</param>
/// <param name="ImpactSummary">影响摘要，可为空。</param>
/// <param name="Status">确认项状态。</param>
/// <param name="ExpiresAt">到期时间（UTC），可为空。</param>
/// <param name="UpdatedAt">更新时间（UTC）。</param>
public sealed record DashboardConfirmationView(long Id, string RiskLevel, string Title, string? ImpactSummary, string Status, DateTime? ExpiresAt, DateTime UpdatedAt);

/// <summary>仪表板管家动态摘要；最近产生的活动展示。</summary>
/// <param name="Id">活动主键。</param>
/// <param name="Category">活动分类。</param>
/// <param name="Title">活动标题。</param>
/// <param name="RiskLevel">风险等级，L1/L2/L3。</param>
/// <param name="Status">活动状态。</param>
/// <param name="ResultSummary">结果摘要，可为空。</param>
/// <param name="CreatedAt">创建时间（UTC）。</param>
public sealed record DashboardStewardActivityView(long Id, string Category, string Title, string RiskLevel, string Status, string? ResultSummary, DateTime CreatedAt);

/// <summary>仪表板建议摘要。</summary>
/// <param name="RunId">关联的 AgentRun 主键。</param>
/// <param name="Summary">建议摘要，可为空。</param>
/// <param name="Status">运行状态。</param>
/// <param name="CreatedAt">创建时间（UTC）。</param>
public sealed record DashboardSuggestionView(long RunId, string? Summary, string Status, DateTime CreatedAt);

/// <summary>仪表板总视图，由各模块按降级策略组合而成。</summary>
/// <remarks>
/// <c>Home</c> 模块对应产品契约的 <c>homeSummary</c>（家庭概览）；<c>quickActions</c>
/// 为前端静态快捷入口，不经过后端，本视图不包含该字段。
/// </remarks>
/// <param name="GeneratedAt">生成时间（UTC）。</param>
/// <param name="PartialFailure">是否存在任一模块不可用。</param>
/// <param name="Home">Home 模块（家庭概览，产品契约 homeSummary）。</param>
/// <param name="PendingConfirmations">待确认事项模块；任一模块失败时前端仍优先展示该字段。</param>
/// <param name="StewardActivities">管家动态模块。</param>
/// <param name="Scenes">Scenes 模块。</param>
/// <param name="Todos">Todos 模块。</param>
/// <param name="Calendar">Calendar 模块。</param>
/// <param name="Suggestion">Suggestion 模块。</param>
public sealed record DashboardView(
    DateTime GeneratedAt,
    bool PartialFailure,
    DashboardModule<DashboardHomeView> Home,
    DashboardModule<IReadOnlyList<DashboardConfirmationView>> PendingConfirmations,
    DashboardModule<IReadOnlyList<DashboardStewardActivityView>> StewardActivities,
    DashboardModule<IReadOnlyList<SceneView>> Scenes,
    DashboardModule<IReadOnlyList<DashboardTodoView>> Todos,
    DashboardModule<IReadOnlyList<DashboardCalendarEventView>> Calendar,
    DashboardModule<DashboardSuggestionView?> Suggestion);
