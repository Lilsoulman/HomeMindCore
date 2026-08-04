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
}
