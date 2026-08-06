using System.Threading;
using System.Threading.Tasks;

namespace HomeMind.Business.IServices.Expert;

/// <summary>对象存储抽象，仅由 Expert File 服务内部调用。客户端响应不返回内部对象路径或存储凭据。</summary>
public interface IExpertFileStorage
{
    Task<string> CreateUploadSessionAsync(long tenantId, long fileId, string fileName, long sizeBytes, string mimeType, CancellationToken cancellationToken = default);
    Task CommitObjectAsync(long tenantId, long fileId, string objectKey, long offsetBytes, long sizeBytes, string sha256, CancellationToken cancellationToken = default);
    Task<string> GenerateReadTokenAsync(long tenantId, long fileId, string objectKey, string purpose, CancellationToken cancellationToken = default);
    Task DeleteAsync(long tenantId, long fileId, string objectKey, CancellationToken cancellationToken = default);

    /// <summary>服务端直接写入生成文件字节流，返回对象键。</summary>
    Task<string> WriteGeneratedAsync(long tenantId, long fileId, string fileName, byte[] content, CancellationToken cancellationToken = default);

    /// <summary>读取对象完整字节流，供内容下载端点使用。</summary>
    Task<byte[]> ReadAllBytesAsync(long tenantId, long fileId, string objectKey, CancellationToken cancellationToken = default);
}
