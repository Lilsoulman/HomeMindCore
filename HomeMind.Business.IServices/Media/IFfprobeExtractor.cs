namespace HomeMind.Business.IServices.Media;

/// <summary>媒体元数据（ffprobe 提取结果）。</summary>
/// <param name="DurationSeconds">时长（秒），解析失败为空。</param>
/// <param name="Width">画面宽度（像素），解析失败为空。</param>
/// <param name="Height">画面高度（像素），解析失败为空。</param>
/// <param name="Fps">帧率，解析失败为空。</param>
public sealed record MediaMetadata(int? DurationSeconds, int? Width, int? Height, double? Fps);

/// <summary>
/// ffprobe 元数据提取契约（B29）：调用系统 ffprobe 解析素材时长/分辨率/帧率。
/// 提取失败（ffprobe 不可用、非法媒体、超时）返回 null，不阻塞素材登记。
/// </summary>
public interface IFfprobeExtractor
{
    /// <summary>解析指定文件路径的媒体元数据；不可解析返回 null，不抛异常。</summary>
    /// <param name="filePath">服务端素材文件路径。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>媒体元数据；不可解析返回 null。</returns>
    Task<MediaMetadata?> ExtractAsync(string filePath, CancellationToken cancellationToken = default);
}
