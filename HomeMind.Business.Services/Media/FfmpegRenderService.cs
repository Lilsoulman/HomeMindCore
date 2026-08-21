using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using HomeMind.Business.IServices.Media;
using Microsoft.Extensions.Configuration;

namespace HomeMind.Business.Services.Media;

/// <summary>使用受控 ffmpeg 进程将已确认的多素材方案拼接为 mp4 的服务；未启用或输入不可访问时绝不伪造产物。</summary>
public sealed class FfmpegRenderService : IClippingRenderService
{
    private readonly IConfiguration _configuration;
    private readonly ClippingRenderOptions _options;

    /// <summary>构造 ffmpeg 粗剪渲染服务。</summary>
    /// <param name="configuration">应用配置，读取 Clipping:Render 节。</param>
    public FfmpegRenderService(IConfiguration configuration)
    {
        _configuration = configuration;
        _options = new ClippingRenderOptions
        {
            Enabled = configuration.GetValue<bool>("Clipping:Render:Enabled"),
            FfmpegPath = configuration["Clipping:Render:FfmpegPath"] ?? string.Empty,
            OutputPath = configuration["Clipping:Render:OutputPath"] ?? string.Empty,
            TimeoutSeconds = configuration.GetValue<int?>("Clipping:Render:TimeoutSeconds") ?? 300
        };
    }

    /// <inheritdoc />
    public bool IsEnabled => IsRenderEnabled() && !string.IsNullOrWhiteSpace(_options.FfmpegPath) && File.Exists(_options.FfmpegPath);

    /// <inheritdoc />
    public async Task<ClippingRenderResult> RenderAsync(string planJson, CancellationToken cancellationToken = default)
    {
        if (!IsRenderEnabled() || string.IsNullOrWhiteSpace(_options.FfmpegPath))
            return new ClippingRenderResult(false, "粗剪渲染尚未启用。");
        if (!File.Exists(_options.FfmpegPath))
            return new ClippingRenderResult(false, "粗剪渲染服务不可用。");
        if (!TryReadPlan(planJson, out var segments))
            return new ClippingRenderResult(false, "剪辑方案缺少可渲染的本地素材。");
        if (segments.Any(segment => !File.Exists(segment.MediaLocation)))
            return new ClippingRenderResult(false, "剪辑素材当前不可访问。");

        var outputRoot = string.IsNullOrWhiteSpace(_options.OutputPath) ? Path.GetTempPath() : _options.OutputPath;
        var outputPath = Path.Combine(outputRoot, $"quick_edit_{Guid.NewGuid():N}.mp4");
        try
        {
            Directory.CreateDirectory(outputRoot);
            var startInfo = new ProcessStartInfo
            {
                FileName = _options.FfmpegPath,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("-y");
            startInfo.ArgumentList.Add("-hide_banner");
            startInfo.ArgumentList.Add("-loglevel");
            startInfo.ArgumentList.Add("error");
            foreach (var segment in segments)
            {
                startInfo.ArgumentList.Add("-ss");
                startInfo.ArgumentList.Add(segment.StartSeconds.ToString(CultureInfo.InvariantCulture));
                startInfo.ArgumentList.Add("-t");
                startInfo.ArgumentList.Add(segment.DurationSeconds.ToString(CultureInfo.InvariantCulture));
                startInfo.ArgumentList.Add("-i");
                startInfo.ArgumentList.Add(segment.MediaLocation);
            }
            startInfo.ArgumentList.Add("-filter_complex");
            startInfo.ArgumentList.Add(BuildFilterGraph(segments.Count));
            startInfo.ArgumentList.Add("-map");
            startInfo.ArgumentList.Add("[v]");
            startInfo.ArgumentList.Add("-map");
            startInfo.ArgumentList.Add("[a]");
            startInfo.ArgumentList.Add("-c:v");
            startInfo.ArgumentList.Add("libx264");
            startInfo.ArgumentList.Add("-c:a");
            startInfo.ArgumentList.Add("aac");
            startInfo.ArgumentList.Add("-movflags");
            startInfo.ArgumentList.Add("+faststart");
            startInfo.ArgumentList.Add(outputPath);
            using var process = Process.Start(startInfo);
            if (process is null) return new ClippingRenderResult(false, "粗剪渲染服务无法启动。");

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _options.TimeoutSeconds)));
            var stdOutTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var stdErrTask = process.StandardError.ReadToEndAsync(timeout.Token);
            await Task.WhenAll(stdOutTask, stdErrTask, process.WaitForExitAsync(timeout.Token));
            if (process.ExitCode != 0 || !File.Exists(outputPath))
            {
                var stderr = stdErrTask.Result ?? string.Empty;
                var trimmed = stderr.Length > 200 ? stderr[..200] : stderr;
                var detail = string.IsNullOrWhiteSpace(trimmed) ? "请稍后重试。" : $"：{StripStderr(trimmed)}";
                return new ClippingRenderResult(false, $"粗剪渲染失败{detail}");
            }

            var content = await File.ReadAllBytesAsync(outputPath, cancellationToken);
            return content.Length == 0
                ? new ClippingRenderResult(false, "粗剪渲染未生成有效视频，请稍后重试。")
                : new ClippingRenderResult(true, "粗剪视频已生成。", $"quick_edit_{Guid.NewGuid():N}.mp4", content);
        }
        catch (OperationCanceledException) { return new ClippingRenderResult(false, "粗剪渲染超时或已取消。"); }
        catch { return new ClippingRenderResult(false, "粗剪渲染失败，请稍后重试。"); }
        finally
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);
        }
    }

    /// <summary>读取方案片段的本地位置、起始点和时长；兼容历史单素材方案。</summary>
    private static bool TryReadPlan(string planJson, out IReadOnlyList<RenderSegment> segments)
    {
        segments = [];
        try
        {
            using var document = JsonDocument.Parse(planJson);
            var root = document.RootElement;
            var parsed = new List<RenderSegment>();
            if (root.TryGetProperty("segments", out var segmentArray) && segmentArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var segment in segmentArray.EnumerateArray())
                {
                    // B24-B37 历史方案的 segments 只有 source/duration，下面回退到根级 media_location。
                    if (segment.ValueKind != JsonValueKind.Object || !segment.TryGetProperty("media_location", out var location))
                    {
                        parsed.Clear();
                        break;
                    }
                    if (location.ValueKind != JsonValueKind.String
                        || string.IsNullOrWhiteSpace(location.GetString())
                        || !segment.TryGetProperty("duration", out var duration)
                        || !duration.TryGetDouble(out var durationSeconds)
                        || durationSeconds <= 0)
                        return false;
                    var startSeconds = segment.TryGetProperty("start", out var start) && start.TryGetDouble(out var parsedStart) ? parsedStart : 0;
                    var mediaLocation = location.GetString()!.Trim();
                    if (startSeconds < 0 || mediaLocation.IndexOfAny(['\r', '\n']) >= 0) return false;
                    parsed.Add(new RenderSegment(mediaLocation, startSeconds, durationSeconds));
                }
            }

            if (parsed.Count == 0
                && root.TryGetProperty("media_location", out var legacyLocation)
                && legacyLocation.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(legacyLocation.GetString())
                && root.TryGetProperty("total_duration", out var legacyDuration)
                && legacyDuration.TryGetDouble(out var legacyDurationSeconds)
                && legacyDurationSeconds > 0)
                parsed.Add(new RenderSegment(legacyLocation.GetString()!.Trim(), 0, legacyDurationSeconds));

            segments = parsed;
            return segments.Count > 0;
        }
        catch (JsonException) { return false; }
    }

    /// <summary>构造逐输入精确裁切后的音视频 concat 滤镜，重置每个片段的时间戳以保证连续时间线。</summary>
    private static string BuildFilterGraph(int segmentCount)
    {
        var filters = new List<string>();
        for (var index = 0; index < segmentCount; index++)
        {
            filters.Add($"[{index}:v]setpts=PTS-STARTPTS[v{index}]");
            filters.Add($"[{index}:a]asetpts=PTS-STARTPTS[a{index}]");
        }
        var inputs = string.Concat(Enumerable.Range(0, segmentCount).Select(index => $"[v{index}][a{index}]"));
        filters.Add($"{inputs}concat=n={segmentCount}:v=1:a=1[v][a]");
        return string.Join(';', filters);
    }

    /// <summary>直接读取渲染开关，确保环境变量和部署配置覆盖绑定对象的默认值。</summary>
    private bool IsRenderEnabled() => _configuration.GetValue<bool?>("Clipping:Render:Enabled") ?? _options.Enabled;

    /// <summary>将 ffmpeg stderr 折叠为单行可展示片段，剥离控制字符与多余换行。</summary>
    private static string StripStderr(string text)
    {
        var collapsed = text.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');
        return System.Text.RegularExpressions.Regex.Replace(collapsed, @"\s+", " ").Trim();
    }

    /// <summary>单个可渲染片段的内部描述。</summary>
    private sealed record RenderSegment(string MediaLocation, double StartSeconds, double DurationSeconds);
}

/// <summary>粗剪视频渲染配置；默认关闭，输出目录只作为短暂中间文件位置。</summary>
public sealed class ClippingRenderOptions
{
    /// <summary>是否允许实际启动 ffmpeg 渲染。</summary>
    public bool Enabled { get; set; }
    /// <summary>ffmpeg 可执行文件路径。</summary>
    public string FfmpegPath { get; set; } = string.Empty;
    /// <summary>渲染中间文件目录，不对外暴露。</summary>
    public string OutputPath { get; set; } = string.Empty;
    /// <summary>单次渲染的最大等待秒数。</summary>
    public int TimeoutSeconds { get; set; } = 300;
}
