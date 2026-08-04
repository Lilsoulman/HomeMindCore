namespace HomeMind.Common.Model.ViewModel.Data.AI;

public sealed record HousekeeperRunRequest(string Intent, long? SpaceId, string? IdempotencyKey);

public sealed record ConfirmHousekeeperActionRequest(string IdempotencyKey);

public sealed record HousekeeperRunEventView(int Sequence, string Type, string Message, DateTime CreatedAt);

public sealed record HousekeeperRunActionView(
    long Id,
    string ActionType,
    string Status,
    string Title,
    string Description,
    long DeviceId,
    string DeviceName,
    string Capability,
    object TargetValue);

public sealed record HousekeeperRunView(
    long Id,
    string Status,
    string? ResultSummary,
    DateTime CreatedAt,
    DateTime? FinishedAt,
    IReadOnlyList<HousekeeperRunEventView> Events,
    IReadOnlyList<HousekeeperRunActionView> Actions);

public sealed record HousekeeperActionExecutionView(long ActionId, string Status, string Message, DateTime UpdatedAt);
