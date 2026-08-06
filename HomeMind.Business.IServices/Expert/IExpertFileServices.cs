using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.AI;

namespace HomeMind.Business.IServices.Expert;

/// <summary>Expert File 上传、扫描、附件、删除的最小闭环。文件二进制不经数据库，由 <see cref="IExpertFileStorage"/> 持有。</summary>
public interface IExpertFileServices
{
    Task<ServiceResult> CreateUploadAsync(long userId, long tenantId, ExpertFileUploadRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult> CommitObjectAsync(long userId, long tenantId, long fileId, ExpertFileObjectRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult> ListAsync(long userId, long tenantId, CancellationToken cancellationToken = default);
    Task<ServiceResult> DeleteAsync(long userId, long tenantId, long fileId, CancellationToken cancellationToken = default);
    Task<ServiceResult> AttachToExpertAsync(long userId, long tenantId, long expertId, ExpertFileAttachmentRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult> AttachToRunAsync(long userId, long tenantId, long runId, ExpertFileAttachmentRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult> GenerateReadTokenAsync(long userId, long tenantId, long fileId, string purpose, CancellationToken cancellationToken = default);

    /// <summary>登记服务端生成的 Ready 文件（跳过用户上传流程），可选附件到 AgentRun。</summary>
    Task<ServiceResult> RegisterGeneratedFileAsync(long userId, long tenantId, string name, string mimeType, byte[] content, long? attachRunId, CancellationToken cancellationToken = default);

    /// <summary>读取文件字节流与下载元数据，供 content 下载端点使用。</summary>
    Task<ServiceResult> GetContentAsync(long userId, long tenantId, long fileId, CancellationToken cancellationToken = default);
}

/// <summary>服务端生成文件的下载内容。</summary>
/// <param name="Bytes">文件字节流。</param>
/// <param name="MimeType">MIME 类型。</param>
/// <param name="Name">对外文件名。</param>
public sealed record GeneratedFileContent(byte[] Bytes, string MimeType, string Name);
