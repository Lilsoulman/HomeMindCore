using System.Threading;
using System.Threading.Tasks;

namespace HomeMind.Business.IServices.Expert;

/// <summary>服务端异步扫描抽象。本地占位实现只校验扩展名、MIME、大小、SHA-256；可替换为外部扫描器。</summary>
public interface IExpertFileScanner
{
    Task<ExpertFileScanResult> ScanAsync(long tenantId, long fileId, string objectKey, long sizeBytes, string sha256, string mimeType, string fileName, CancellationToken cancellationToken = default);
}

public sealed record ExpertFileScanResult(bool Ready, string? RejectionReason);
