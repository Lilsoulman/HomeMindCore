using HomeMind.Common.Model.ViewModel.Common;

namespace HomeMind.Business.IServices.Media;

/// <summary>
/// 快速剪辑素材自动发现契约（B38）：后台 Worker 定时扫描素材根目录，自动登记用户放入的
/// 视频/音频文件（扩展名白名单、最近修改时间窗、路径哈希去重、ffprobe 元数据失败不阻塞）；
/// 目录不可达静默降级。扫描结果不写审计（后台自动行为），仅本人可见语义与上传素材一致。
/// </summary>
public interface IClippingMaterialScanServices
{
    /// <summary>
    /// 执行一轮素材目录扫描：遍历素材根目录第一级用户目录，登记时间窗内未登记的媒体文件。
    /// 根目录不可达或用户目录无归属时静默跳过；重复扫描同一文件不重复登记。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>统一响应，成功返回 200 且 Data 为本轮新登记数量。</returns>
    Task<ServiceResult> ScanAsync(CancellationToken cancellationToken = default);
}
