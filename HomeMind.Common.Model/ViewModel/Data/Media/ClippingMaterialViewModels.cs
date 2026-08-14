namespace HomeMind.Common.Model.ViewModel.Data.Media;

/// <summary>快速剪辑素材登记请求：浏览器上传（Content 非空）与路径模式（FilePath 非空）二选一。</summary>
/// <param name="FilePath">路径模式：素材在服务端允许目录内的路径，越界返回 403。</param>
/// <param name="FileName">上传文件名（浏览器上传模式）。</param>
/// <param name="ContentType">上传 MIME 类型（浏览器上传模式）。</param>
/// <param name="FileSize">上传文件大小（字节，浏览器上传模式）。</param>
/// <param name="Content">上传文件流（浏览器上传模式）。</param>
public sealed record ClippingMaterialUploadRequest(string? FilePath, string? FileName, string? ContentType, long FileSize, Stream? Content);

/// <summary>快速剪辑素材视图；只含展示安全字段，StoragePath 为服务端可访问路径（供回填 media_location），不暴露目录遍历信息。</summary>
/// <param name="Id">素材主键。</param>
/// <param name="FileName">素材文件名。</param>
/// <param name="SourceType">素材来源：upload（浏览器上传或路径登记）/scan（素材根目录自动发现）。</param>
/// <param name="ContentType">素材 MIME 类型，可为空。</param>
/// <param name="FileSize">文件大小（字节）。</param>
/// <param name="DurationSeconds">时长（秒），ffprobe 未解析为空。</param>
/// <param name="Width">画面宽度（像素），可为空。</param>
/// <param name="Height">画面高度（像素），可为空。</param>
/// <param name="StoragePath">服务端可访问的素材路径，供回填 media_location。</param>
/// <param name="CreatedAt">登记时间（UTC）。</param>
public sealed record ClippingMaterialView(long Id, string FileName, string SourceType, string? ContentType, long FileSize, int? DurationSeconds, int? Width, int? Height, string StoragePath, DateTime CreatedAt);
