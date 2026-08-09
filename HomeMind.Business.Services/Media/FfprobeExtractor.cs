using System.Diagnostics;
using System.Text.Json;
using HomeMind.Business.IServices.Media;
using Microsoft.Extensions.Configuration;

namespace HomeMind.Business.Services.Media;

/// <summary>ffprobe 元数据提取器：调用系统 ffprobe（-show_format -show_streams）解析时长/分辨率/帧率；任何失败返回 null 不抛异常。</summary>
public sealed class FfprobeExtractor : IFfprobeExtractor
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);
    private readonly string _ffprobePath;

    /// <summary>构造 ffprobe 提取器。</summary>
    /// <param name="config">配置，读取 Clipping:FfprobePath（默认 ffprobe）。</param>
    public FfprobeExtractor(IConfiguration config) =>
        _ffprobePath = string.IsNullOrWhiteSpace(config["Clipping:FfprobePath"]) ? "ffprobe" : config["Clipping:FfprobePath"]!;

    /// <inheritdoc />
    public async Task<MediaMetadata?> ExtractAsync(string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(Timeout);
            var psi = new ProcessStartInfo
            {
                FileName = _ffprobePath,
                Arguments = $"-v quiet -print_format json -show_format -show_streams \"{filePath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = Process.Start(psi);
            if (process is null) return null;
            var stdout = await process.StandardOutput.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token);
            if (process.ExitCode != 0) return null;

            using var document = JsonDocument.Parse(stdout);
            var root = document.RootElement;
            int? duration = null;
            if (root.TryGetProperty("format", out var format) && format.TryGetProperty("duration", out var durationElement) && durationElement.TryGetDouble(out var durationSeconds))
                duration = (int)Math.Round(durationSeconds);

            int? width = null, height = null;
            double? fps = null;
            if (root.TryGetProperty("streams", out var streams) && streams.GetArrayLength() > 0)
            {
                var first = streams[0];
                if (first.TryGetProperty("width", out var widthElement) && widthElement.TryGetInt32(out var parsedWidth)) width = parsedWidth;
                if (first.TryGetProperty("height", out var heightElement) && heightElement.TryGetInt32(out var parsedHeight)) height = parsedHeight;
                if (first.TryGetProperty("r_frame_rate", out var frameRateElement)) fps = ParseFrameRate(frameRateElement.GetString());
            }

            if (duration is null && width is null && height is null && fps is null) return null;
            return new MediaMetadata(duration, width, height, fps);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            return null;
        }
    }

    /// <summary>解析 ffprobe 帧率字符串（如 "30/1" 或 "29.97"）；不可解析返回 null。</summary>
    private static double? ParseFrameRate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var parts = value.Split('/');
        if (parts.Length == 2 && double.TryParse(parts[0], out var numerator) && double.TryParse(parts[1], out var denominator) && denominator != 0)
            return numerator / denominator;
        return double.TryParse(value, out var parsed) ? parsed : null;
    }
}
