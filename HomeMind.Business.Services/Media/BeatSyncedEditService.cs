using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using HomeMind.Business.IServices.Media;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HomeMind.Business.Services.Media;

/// <summary>
/// beat-synced-edit 适配器。它只负责分析和输出 EDL，不渲染 MP4；EDL 由剪映 MCP 装配为草稿。
/// </summary>
public sealed class BeatSyncedEditService : IBeatSyncedEditService
{
    private readonly BeatSyncedEditOptions _options;
    private readonly ILogger<BeatSyncedEditService> _logger;

    public BeatSyncedEditService(IConfiguration configuration, ILogger<BeatSyncedEditService> logger)
    {
        _options = configuration.GetSection("Clipping:BeatSync").Get<BeatSyncedEditOptions>() ?? new BeatSyncedEditOptions();
        _logger = logger;
    }

    public async Task<BeatSyncedEditPlan?> CreatePlanAsync(
        IReadOnlyList<string> videoLocations,
        string musicLocation,
        int targetDurationSeconds,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || videoLocations.Count == 0 || string.IsNullOrWhiteSpace(musicLocation)) return null;
        if (string.IsNullOrWhiteSpace(_options.WorkingDirectory) || !Directory.Exists(_options.WorkingDirectory))
            throw new BeatSyncedEditException("beat-synced-edit 工作目录不存在。");

        var root = string.IsNullOrWhiteSpace(_options.OutputPath)
            ? Path.Combine(Path.GetTempPath(), "homemind-beat-sync")
            : Path.GetFullPath(_options.OutputPath);
        var jobDirectory = Path.Combine(root, $"quick_edit_{Guid.NewGuid():N}");
        Directory.CreateDirectory(jobDirectory);
        var beatGridPath = Path.Combine(jobDirectory, "beat-grid.json");
        var clipsPath = Path.Combine(jobDirectory, "clips.json");
        var edlPath = Path.Combine(jobDirectory, "edit-decision-list.json");

        await RunScriptAsync(_options.BeatMapScript, musicLocation, "--output", beatGridPath, cancellationToken);

        var mergedClips = new JsonArray();
        var nextId = 1;
        foreach (var video in videoLocations)
        {
            var tagsPath = Path.Combine(jobDirectory, $"clips-{nextId}.json");
            await RunScriptAsync(_options.ClipTagScript, video, "--output", tagsPath, cancellationToken);
            using var tagsDocument = JsonDocument.Parse(await File.ReadAllTextAsync(tagsPath, cancellationToken));
            if (!tagsDocument.RootElement.TryGetProperty("clips", out var clips) || clips.ValueKind != JsonValueKind.Array) continue;
            foreach (var clip in clips.EnumerateArray())
            {
                var item = JsonNode.Parse(clip.GetRawText())?.AsObject() ?? new JsonObject();
                item["source"] = Path.GetFileName(video);
                AddClipWindows(mergedClips, item, ref nextId);
            }
        }
        if (mergedClips.Count == 0) throw new BeatSyncedEditException("beat-synced-edit 未检测到可用镜头。");
        await File.WriteAllTextAsync(clipsPath, JsonSerializer.Serialize(new
        {
            sources = videoLocations.Select(Path.GetFileName).ToArray(),
            clip_count = mergedClips.Count,
            clips = mergedClips
        }), cancellationToken);

        var stride = Math.Max(1, _options.BeatStride);
        await RunScriptAsync(_options.PlanEditScript, beatGridPath, clipsPath, "--output", edlPath,
            "--beat-stride", stride.ToString(CultureInfo.InvariantCulture), cancellationToken);

        using var beatDocument = JsonDocument.Parse(await File.ReadAllTextAsync(beatGridPath, cancellationToken));
        using var edlDocument = JsonDocument.Parse(await File.ReadAllTextAsync(edlPath, cancellationToken));
        var beatRoot = beatDocument.RootElement;
        var edlRoot = edlDocument.RootElement;
        var sourceMap = videoLocations.ToDictionary(location => Path.GetFileName(location) ?? location, StringComparer.OrdinalIgnoreCase);
        var segments = new List<BeatSyncedEditSegment>();
        if (edlRoot.TryGetProperty("edits", out var edits) && edits.ValueKind == JsonValueKind.Array)
        {
            foreach (var edit in edits.EnumerateArray())
            {
                var sourceName = ReadString(edit, "clip_source");
                if (sourceName is null || !sourceMap.TryGetValue(sourceName, out var sourcePath)) continue;
                var timelineStart = Math.Max(0, ReadDouble(edit, "timeline_start"));
                if (timelineStart >= targetDurationSeconds) continue;
                var duration = Math.Min(ReadDouble(edit, "clip_duration"), targetDurationSeconds - timelineStart);
                if (duration <= 0) continue;
                segments.Add(new BeatSyncedEditSegment(
                    sourcePath,
                    Math.Max(0, ReadDouble(edit, "clip_start")),
                    duration,
                    timelineStart,
                    ReadString(edit, "beat_type") ?? "beat"));
            }
        }
        if (segments.Count == 0) throw new BeatSyncedEditException("beat-synced-edit 未生成有效 EDL。");

        var audioSegment = edlRoot.TryGetProperty("audio_segment", out var audio) && audio.ValueKind == JsonValueKind.Object ? audio : default;
        var musicStart = audio.ValueKind == JsonValueKind.Object ? ReadDouble(audio, "start") : 0;
        var durationTotal = Math.Min(targetDurationSeconds, ReadDouble(edlRoot, "timeline_duration"));
        var tempo = ReadDouble(edlRoot, "tempo");
        _logger.LogInformation("Beat-synced edit generated {SegmentCount} segments at {Tempo} BPM: {BeatGridPath}", segments.Count, tempo, beatGridPath);
        return new BeatSyncedEditPlan(musicLocation, beatGridPath, tempo, Math.Max(0, musicStart), durationTotal > 0 ? durationTotal : targetDurationSeconds, segments);
    }

    private async Task RunScriptAsync(string script, params object[] values)
    {
        var cancellationToken = (CancellationToken)values[^1];
        var args = values[..^1].Select(value => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "").ToArray();
        var psi = new ProcessStartInfo
        {
            FileName = _options.CommandFileName,
            WorkingDirectory = _options.WorkingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        psi.Environment["PYTHONUTF8"] = "1";
        psi.Environment["PYTHONIOENCODING"] = "utf-8";
        foreach (var prefix in (_options.CommandArguments ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries)) psi.ArgumentList.Add(prefix);
        psi.ArgumentList.Add(Path.Combine(_options.WorkingDirectory, script));
        foreach (var arg in args) psi.ArgumentList.Add(arg);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(30, _options.TimeoutSeconds)));
        using var process = Process.Start(psi) ?? throw new BeatSyncedEditException($"无法启动 beat-synced-edit：{_options.CommandFileName}");
        var stdoutTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var stderrTask = process.StandardError.ReadToEndAsync(timeout.Token);
        await process.WaitForExitAsync(timeout.Token);
        var stderr = await stderrTask;
        await stdoutTask;
        if (process.ExitCode != 0)
            throw new BeatSyncedEditException($"beat-synced-edit 执行失败（{script}）：{TrimError(stderr)}");
    }

    private static string TrimError(string value) => string.IsNullOrWhiteSpace(value) ? "无错误详情" : value.Trim()[..Math.Min(value.Trim().Length, 500)];
    private void AddClipWindows(JsonArray mergedClips, JsonObject clip, ref int nextId)
    {
        var start = ReadNumber(clip, "start");
        var duration = ReadNumber(clip, "duration");
        var windowSeconds = Math.Max(0.25, _options.ClipWindowSeconds);
        if (duration <= windowSeconds)
        {
            clip["id"] = nextId++;
            mergedClips.Add(clip);
            return;
        }

        var end = start + duration;
        // 只取完整窗口；末尾不足一个窗口的片段在 upstream 规划后会将 source_start
        // 推到素材边界外，剪映 MCP 因其额外的时长安全余量会拒绝该片段。
        for (var windowStart = start; windowStart + windowSeconds <= end + 0.001; windowStart += windowSeconds)
        {
            var window = clip.DeepClone().AsObject();
            window["id"] = nextId++;
            window["start"] = Math.Round(windowStart, 3);
            window["end"] = Math.Round(windowStart + windowSeconds, 3);
            window["duration"] = Math.Round(windowSeconds, 3);
            mergedClips.Add(window);
        }

        // 增加一个带安全余量的尾部窗口，既保留临近结尾的可选镜头，也不会触发剪映
        // media parser 的 0.2 秒保守裁剪。该窗口可能与前一个窗口重叠，但拥有独立 ID，
        // 可在节拍数量多于素材窗口时作为另一个候选镜头。
        var safeTailStart = Math.Max(start, end - windowSeconds - 0.25);
        if (safeTailStart > start + 0.001)
        {
            var tail = clip.DeepClone().AsObject();
            tail["id"] = nextId++;
            tail["start"] = Math.Round(safeTailStart, 3);
            tail["end"] = Math.Round(safeTailStart + windowSeconds, 3);
            tail["duration"] = Math.Round(windowSeconds, 3);
            mergedClips.Add(tail);
        }
    }

    private static double ReadNumber(JsonObject value, string name) => value[name] is JsonValue number && number.TryGetValue<double>(out var result) ? result : 0;
    private static string? ReadString(JsonElement element, string name) => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static double ReadDouble(JsonElement element, string name) => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var result) ? result : 0;
}

public sealed class BeatSyncedEditException : Exception
{
    public BeatSyncedEditException(string message) : base(message) { }
}
