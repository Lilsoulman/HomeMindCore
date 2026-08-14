using System.Diagnostics;
using System.Text.Json;
using HomeMind.Business.IServices.Media;
using Microsoft.Extensions.Configuration;

namespace HomeMind.Business.Services.Media;

/// <summary>使用受控 ffmpeg 进程将单素材方案渲染为 mp4 的服务；未启用或输入不可访问时绝不伪造产物。</summary>
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
        if (!TryReadPlan(planJson, out var inputPath, out var durationSeconds))
            return new ClippingRenderResult(false, "剪辑方案缺少可渲染的本地素材。");
        if (!File.Exists(inputPath))
            return new ClippingRenderResult(false, "剪辑素材当前不可访问。");

        var outputRoot = string.IsNullOrWhiteSpace(_options.OutputPath) ? Path.GetTempPath() : _options.OutputPath;
        var outputPath = Path.Combine(outputRoot, $"quick_edit_{Guid.NewGuid():N}.mp4");
        try
        {
            Directory.CreateDirectory(outputRoot);
            var arguments = $"-y -hide_banner -loglevel error -i {Quote(inputPath)} -t {durationSeconds} -c:v libx264 -c:a aac -movflags +faststart {Quote(outputPath)}";
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = _options.FfmpegPath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            });
            if (process is null) return new ClippingRenderResult(false, "粗剪渲染服务无法启动。");

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _options.TimeoutSeconds)));
            await Task.WhenAll(process.StandardOutput.ReadToEndAsync(timeout.Token), process.StandardError.ReadToEndAsync(timeout.Token), process.WaitForExitAsync(timeout.Token));
            if (process.ExitCode != 0 || !File.Exists(outputPath)) return new ClippingRenderResult(false, "粗剪渲染失败，请稍后重试。");

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

    /// <summary>读取单素材首版方案的本地位置和目标时长。</summary>
    private static bool TryReadPlan(string planJson, out string inputPath, out int durationSeconds)
    {
        inputPath = string.Empty;
        durationSeconds = 0;
        try
        {
            using var document = JsonDocument.Parse(planJson);
            var root = document.RootElement;
            if (!root.TryGetProperty("media_location", out var location) || location.ValueKind != JsonValueKind.String) return false;
            inputPath = location.GetString()?.Trim() ?? string.Empty;
            if (!root.TryGetProperty("total_duration", out var duration) || !duration.TryGetInt32(out durationSeconds)) return false;
            return !string.IsNullOrWhiteSpace(inputPath) && durationSeconds > 0;
        }
        catch (JsonException) { return false; }
    }

    /// <summary>直接读取渲染开关，确保环境变量和部署配置覆盖绑定对象的默认值。</summary>
    private bool IsRenderEnabled() => _configuration.GetValue<bool?>("Clipping:Render:Enabled") ?? _options.Enabled;

    /// <summary>将文件路径转为进程参数中的单一安全参数。</summary>
    private static string Quote(string value) => $"\"{value.Replace("\"", "\\\"")}\"";
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
