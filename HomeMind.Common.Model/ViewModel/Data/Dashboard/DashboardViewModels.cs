using HomeMind.Common.Model.ViewModel.Data.SmartHome;

namespace HomeMind.Common.Model.ViewModel.Data.Dashboard;

public sealed record DashboardModule<T>(string Status, T Data, DateTime? UpdatedAt, string? Message);

public sealed record DashboardSpaceSummaryView(long Id, string Name, string SpaceType, string? Summary, int DeviceCount, int OnlineDeviceCount, int OfflineDeviceCount, DateTime? StateUpdatedAt, DateTime UpdatedAt);
public sealed record DashboardHomeView(int SpaceCount, int DeviceCount, int OnlineDeviceCount, int OfflineDeviceCount, IReadOnlyList<DashboardSpaceSummaryView> Spaces);
public sealed record DashboardTodoView(long Id, string Title, string Status, string? Priority, DateTime? DueAt, bool Pinned, DateTime UpdatedAt);
public sealed record DashboardCalendarEventView(long Id, string Title, DateTime StartAt, DateTime? EndAt, bool AllDay, DateTime UpdatedAt);
public sealed record DashboardSuggestionView(long RunId, string? Summary, string Status, DateTime CreatedAt);

public sealed record DashboardView(
    DateTime GeneratedAt,
    bool PartialFailure,
    DashboardModule<DashboardHomeView> Home,
    DashboardModule<IReadOnlyList<SceneView>> Scenes,
    DashboardModule<IReadOnlyList<DashboardTodoView>> Todos,
    DashboardModule<IReadOnlyList<DashboardCalendarEventView>> Calendar,
    DashboardModule<DashboardSuggestionView?> Suggestion);
