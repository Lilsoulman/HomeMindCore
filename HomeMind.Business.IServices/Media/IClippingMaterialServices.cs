using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.Media;

namespace HomeMind.Business.IServices.Media;

/// <summary>
/// 快速剪辑素材登记服务契约（B29）：浏览器上传或路径模式登记视频/音频素材，
/// 上传落盘服务端素材目录并经 ffprobe 提取时长/分辨率/帧率元数据（失败不阻塞）；
/// 素材仅本人可见可删，上传/删除写 media_file_* 审计。
/// </summary>
public interface IClippingMaterialServices
{
    /// <summary>
    /// 登记素材：上传模式（Content 非空）落盘服务端素材目录并 ffprobe 提取元数据；
    /// 路径模式（FilePath 非空）校验路径位于配置的素材根目录内（未配置或越界返回 403）。
    /// </summary>
    /// <param name="userId">登记用户标识，由 JWT 推导。</param>
    /// <param name="tenantId">当前租户标识，由 JWT 推导。</param>
    /// <param name="request">素材登记请求，上传与路径二选一。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>素材视图统一响应；二选一不满足返回 422，路径越界返回 403。</returns>
    Task<ServiceResult> UploadAsync(long userId, long tenantId, ClippingMaterialUploadRequest request, CancellationToken cancellationToken = default);

    /// <summary>按登记时间倒序列出本人未删除素材；跨用户数据不返回。</summary>
    /// <param name="userId">查询用户标识，由 JWT 推导。</param>
    /// <param name="tenantId">当前租户标识，由 JWT 推导。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>素材列表统一响应。</returns>
    Task<ServiceResult> ListAsync(long userId, long tenantId, CancellationToken cancellationToken = default);

    /// <summary>软删除本人素材并写 media_file_deleted 审计；他人素材或不存在返回 404。</summary>
    /// <param name="userId">删除用户标识，由 JWT 推导。</param>
    /// <param name="tenantId">当前租户标识，由 JWT 推导。</param>
    /// <param name="materialId">素材主键。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>删除结果统一响应；不可见返回 404。</returns>
    Task<ServiceResult> DeleteAsync(long userId, long tenantId, long materialId, CancellationToken cancellationToken = default);
}
