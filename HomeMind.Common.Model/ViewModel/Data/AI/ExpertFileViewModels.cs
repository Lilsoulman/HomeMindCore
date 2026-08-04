namespace HomeMind.Common.Model.ViewModel.Data.AI;

/// <summary>创建 Expert File 上传会话的请求。文件二进制不进入请求体，仅声明元数据。</summary>
public sealed record ExpertFileUploadRequest(
    string Name,
    string MimeType,
    long SizeBytes,
    string Sha256,
    long? QuotaBytes,
    string? IdempotencyKey);

/// <summary>创建上传会话后返回的最小视图，仅包含 fileId、状态、短期 uploadToken 与 uploadUrl（不含内部对象路径）。</summary>
public sealed record ExpertFileUploadResponse(
    long FileId,
    string Status,
    string UploadToken,
    string UploadUrl,
    long ExpiresAtUnixTime);

/// <summary>提交已扫描对象分片的元数据请求。</summary>
public sealed record ExpertFileObjectRequest(
    string ObjectKey,
    long OffsetBytes,
    long SizeBytes,
    string Sha256,
    string? IdempotencyKey);

/// <summary>列表与详情使用的最小文件视图；不包含内部对象路径、存储凭据或厂商标识。</summary>
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
public sealed record ExpertFileAttachmentRequest(long FileId, string? IdempotencyKey);

/// <summary>短期、按用途限制的读取令牌与下载端点，不返回内部对象路径。</summary>
public sealed record ExpertFileReadTokenResponse(
    long FileId,
    string Purpose,
    string ReadToken,
    string ReadUrl,
    long ExpiresAtUnixTime);
