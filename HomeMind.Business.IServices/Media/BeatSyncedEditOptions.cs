namespace HomeMind.Business.IServices.Media;

/// <summary>beat-synced-edit 本地管线配置。命令必须是受控的本地可执行文件。</summary>
public sealed class BeatSyncedEditOptions
{
    public bool Enabled { get; init; }
    public string CommandFileName { get; init; } = "python";
    public string CommandArguments { get; init; } = "";
    public string WorkingDirectory { get; init; } = "";
    public string BeatMapScript { get; init; } = "beat_map.py";
    public string ClipTagScript { get; init; } = "clip_tag.py";
    public string PlanEditScript { get; init; } = "plan_edit.py";
    public string OutputPath { get; init; } = "";
    public int TimeoutSeconds { get; init; } = 600;
    public int BeatStride { get; init; } = 1;
    public double ClipWindowSeconds { get; init; } = 1;
}

/// <summary>自动卡点 EDL 的一段。</summary>
public sealed record BeatSyncedEditSegment(
    string MediaLocation,
    double SourceStart,
    double Duration,
    double TimelineStart,
    string BeatType);

/// <summary>beat-synced-edit 输出及其可审计文件。</summary>
public sealed record BeatSyncedEditPlan(
    string MusicLocation,
    string BeatGridPath,
    double Tempo,
    double MusicSourceStart,
    double Duration,
    IReadOnlyList<BeatSyncedEditSegment> Segments);

public interface IBeatSyncedEditService
{
    Task<BeatSyncedEditPlan?> CreatePlanAsync(
        IReadOnlyList<string> videoLocations,
        string musicLocation,
        int targetDurationSeconds,
        CancellationToken cancellationToken = default);
}
