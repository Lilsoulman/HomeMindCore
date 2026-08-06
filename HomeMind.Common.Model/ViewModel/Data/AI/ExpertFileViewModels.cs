namespace HomeMind.Common.Model.ViewModel.Data.AI;

/// <summary>创建 Expert File 上传会话的请求。文件二进制不进入请求体，仅声明元数据。</summary>
/// <param name="Name">文件展示名。</param>
/// <param name="MimeType">MIME 类型，扫描器与展示使用。</param>
/// <param name="SizeBytes">文件字节数。</param>
/// <param name="Sha256">文件 SHA-256 摘要的十六进制字符串。</param>
/// <param name="QuotaBytes">可选的额外额度覆盖（字节）。</param>
/// <param name="IdempotencyKey">幂等键，避免重复创建上传会话。</param>
public sealed record ExpertFileUploadRequest(
    string Name,
    string MimeType,
    long SizeBytes,
    string Sha256,
    long? QuotaBytes,
    string? IdempotencyKey);

/// <summary>创建上传会话后返回的最小视图，仅包含 fileId、状态、短期 uploadToken 与 uploadUrl（不含内部对象路径）。</summary>
/// <param name="FileId">文件主键。</param>
/// <param name="Status">文件状态，成功创建时为"pending_upload"。</param>
/// <param name="UploadToken">短期上传凭证。</param>
/// <param name="UploadUrl">客户端应将文件分片 PUT 到此 URL。</param>
/// <param name="ExpiresAtUnixTime">上传会话到期 Unix 时间戳（秒）。</param>
public sealed record ExpertFileUploadResponse(
    long FileId,
    string Status,
    string UploadToken,
    string UploadUrl,
    long ExpiresAtUnixTime);

/// <summary>提交已扫描对象分片的元数据请求。</summary>
/// <param name="ObjectKey">对象内部键，客户端必须按上传会话返回的 uploadUrl 顺序提交。</param>
/// <param name="OffsetBytes">相对文件起始的偏移字节。</param>
/// <param name="SizeBytes">分片字节大小。</param>
/// <param name="Sha256">分片 SHA-256 摘要的十六进制字符串。</param>
/// <param name="IdempotencyKey">幂等键，可为空。</param>
public sealed record ExpertFileObjectRequest(
    string ObjectKey,
    long OffsetBytes,
    long SizeBytes,
    string Sha256,
    string? IdempotencyKey);

/// <summary>列表与详情使用的最小文件视图；不包含内部对象路径、存储凭据或厂商标识。</summary>
/// <param name="Id">文件主键。</param>
/// <param name="Name">文件展示名。</param>
/// <param name="MimeType">MIME 类型。</param>
/// <param name="SizeBytes">文件字节数。</param>
/// <param name="Status">文件状态。</param>
/// <param name="ScanProvider">扫描提供方名称，可为空。</param>
/// <param name="ScanCompletedAt">扫描完成时间（UTC）。</param>
/// <param name="RejectionReason">拒绝原因，仅在状态为"rejected"时填写。</param>
/// <param name="ExpiresAt">过期时间（UTC）。</param>
/// <param name="SoftDeletedAt">软删除时间戳。</param>
/// <param name="CreatedAt">创建时间（UTC）。</param>
/// <param name="UpdatedAt">更新时间（UTC）。</param>
/// <param name="RowVersion">乐观锁版本号。</param>
public sealed record ExpertFileSummary(
    long Id,
    string Name,
    string MimeType,
    long SizeBytes,
    string Status,
    string? ScanProvider,
    DateTime? ScanCompletedAt,
    string? RejectionReason,
    DateTime? ExpiresAt,
    DateTime? SoftDeletedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    long RowVersion);

/// <summary>将文件附加到 Expert 或 AgentRun 的请求。目标由路由决定，二选一。</summary>
/// <param name="FileId">被附加的文件主键，必须同租户且处于"ready"状态。</param>
/// <param name="IdempotencyKey">幂等键，可为空。</param>
public sealed record ExpertFileAttachmentRequest(long FileId, string? IdempotencyKey);

/// <summary>短期、按用途限制的读取令牌与下载端点，不返回内部对象路径。</summary>
/// <param name="FileId">文件主键。</param>
/// <param name="Purpose">读取用途，例如"preview"或"download"。</param>
/// <param name="ReadToken">短期读取凭证。</param>
/// <param name="ReadUrl">客户端应使用此 URL 拉取文件内容。</param>
/// <param name="ExpiresAtUnixTime">读取令牌到期 Unix 时间戳（秒），通常在 10 分钟内。</param>
public sealed record ExpertFileReadTokenResponse(
    long FileId,
    string Purpose,
    string ReadToken,
    string ReadUrl,
    long ExpiresAtUnixTime);
