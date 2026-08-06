using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HomeMind.Business.IServices.Expert;
using HomeMind.Common.Model.Entities;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.AI;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HomeMind.Business.Services.Expert;

/// <summary>Expert File 业务实现。上传、扫描、附件、删除均按 JWT 租户隔离；返回视图不包含内部对象路径或厂商标识。</summary>
public sealed class ExpertFileServices : IExpertFileServices
{
    private static readonly TimeSpan ReadTokenLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan DefaultRetention = TimeSpan.FromDays(30);

    private readonly HomeMindDbContext _db;
    private readonly IExpertFileStorage _storage;
    private readonly IExpertFileScanner _scanner;
    private readonly ILogger<ExpertFileServices> _logger;

    public ExpertFileServices(HomeMindDbContext db, IExpertFileStorage storage, IExpertFileScanner scanner, ILogger<ExpertFileServices> logger)
    {
        _db = db;
        _storage = storage;
        _scanner = scanner;
        _logger = logger;
    }

    public async Task<ServiceResult> CreateUploadAsync(long userId, long tenantId, ExpertFileUploadRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.MimeType))
        {
            return new ServiceResult(422, "文件名和 MIME 类型不能为空。");
        }
        if (request.SizeBytes <= 0 || string.IsNullOrWhiteSpace(request.Sha256) || request.Sha256.Length != 64)
        {
            return new ServiceResult(422, "文件大小与 SHA-256 必须为合法的非空值。");
        }

        var now = DateTime.UtcNow;
        var file = new ExpertFile
        {
            TenantId = tenantId,
            OwnerUserId = userId,
            Name = request.Name.Trim(),
            MimeType = request.MimeType.Trim().ToLowerInvariant(),
            SizeBytes = request.SizeBytes,
            Sha256 = request.Sha256.ToLowerInvariant(),
            Status = ExpertFileStatus.PendingUpload,
            QuotaBytes = request.QuotaBytes ?? 0,
            ExpiresAt = now.Add(DefaultRetention),
            CreatedAt = now,
            UpdatedAt = now,
            RowVersion = 1,
            SyncVersion = 1
        };
        _db.ExpertFiles.Add(file);
        await _db.SaveChangesAsync(cancellationToken);

        string objectKey;
        try
        {
            objectKey = await _storage.CreateUploadSessionAsync(tenantId, file.Id, file.Name, file.SizeBytes, file.MimeType, cancellationToken);
        }
        catch (Exception error)
        {
            _logger.LogWarning(error, "Expert file upload session failed for file {FileId}", file.Id);
            return new ServiceResult(503, "对象存储暂不可用，请稍后重试。");
        }

        var token = await _storage.GenerateReadTokenAsync(tenantId, file.Id, objectKey, "upload", cancellationToken);
        var expires = DateTimeOffset.UtcNow.Add(ReadTokenLifetime).ToUnixTimeSeconds();
        var uploadUrl = $"api/v1/expert-files/{file.Id}/objects/{Uri.EscapeDataString(objectKey)}?uploadToken={Uri.EscapeDataString(token)}";
        _db.TeamRunAudits.Add(new TeamRunAudit
        {
            TenantId = tenantId,
            ActorUserId = userId,
            ExpertFileId = file.Id,
            Action = "file_upload_session",
            Result = "success",
            CreatedAt = now
        });
        await _db.SaveChangesAsync(cancellationToken);
        return new ServiceResult(200, "上传会话已创建。", new ExpertFileUploadResponse(file.Id, file.Status, token, uploadUrl, expires));
    }

    public async Task<ServiceResult> CommitObjectAsync(long userId, long tenantId, long fileId, ExpertFileObjectRequest request, CancellationToken cancellationToken = default)
    {
        var file = await _db.ExpertFiles.SingleOrDefaultAsync(x => x.Id == fileId && x.TenantId == tenantId && x.SoftDeletedAt == null, cancellationToken);
        if (file is null) return new ServiceResult(404, "请求的文件不存在。");
        if (file.Status is ExpertFileStatus.Rejected or ExpertFileStatus.Deleted)
        {
            return new ServiceResult(409, "文件当前不可提交对象。");
        }

        try
        {
            await _storage.CommitObjectAsync(tenantId, file.Id, request.ObjectKey, request.OffsetBytes, request.SizeBytes, request.Sha256, cancellationToken);
        }
        catch (Exception error)
        {
            _logger.LogWarning(error, "Expert file object commit failed for file {FileId}", file.Id);
            return new ServiceResult(503, "对象存储校验失败，请确认元数据后再试。");
        }

        _db.ExpertFileObjects.Add(new ExpertFileObject
        {
            ExpertFileId = file.Id,
            ObjectKey = request.ObjectKey,
            SizeBytes = request.SizeBytes,
            OffsetBytes = request.OffsetBytes,
            UploadedAt = DateTime.UtcNow
        });
        file.Status = ExpertFileStatus.Scanning;
        file.UpdatedAt = DateTime.UtcNow;
        file.RowVersion += 1;
        await _db.SaveChangesAsync(cancellationToken);

        var scan = await _scanner.ScanAsync(tenantId, file.Id, request.ObjectKey, file.SizeBytes, file.Sha256, file.MimeType, file.Name, cancellationToken);
        file.ScanCompletedAt = DateTime.UtcNow;
        file.ScanProvider = "local";
        if (scan.Ready)
        {
            file.Status = ExpertFileStatus.Ready;
            file.RejectionReason = null;
        }
        else
        {
            file.Status = ExpertFileStatus.Rejected;
            file.RejectionReason = scan.RejectionReason;
        }
        file.UpdatedAt = DateTime.UtcNow;
        file.RowVersion += 1;
        _db.TeamRunAudits.Add(new TeamRunAudit
        {
            TenantId = tenantId,
            ActorUserId = userId,
            ExpertFileId = file.Id,
            Action = scan.Ready ? "file_scan" : "file_scan",
            Result = scan.Ready ? "success" : "failure",
            ErrorCode = scan.RejectionReason,
            CreatedAt = file.ScanCompletedAt.Value
        });
        _db.TeamRunAudits.Add(new TeamRunAudit
        {
            TenantId = tenantId,
            ActorUserId = userId,
            ExpertFileId = file.Id,
            Action = "file_object_commit",
            Result = "success",
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken);
        return new ServiceResult(200, scan.Ready ? "文件已就绪。" : "文件被扫描器拒绝。", ToSummary(file));
    }

    public async Task<ServiceResult> ListAsync(long userId, long tenantId, CancellationToken cancellationToken = default)
    {
        var files = await _db.ExpertFiles
            .Where(x => x.TenantId == tenantId && x.SoftDeletedAt == null)
            .OrderByDescending(x => x.UpdatedAt)
            .Take(100)
            .ToListAsync(cancellationToken);
        return new ServiceResult(200, "操作成功", files.Select(ToSummary).ToArray());
    }

    public async Task<ServiceResult> DeleteAsync(long userId, long tenantId, long fileId, CancellationToken cancellationToken = default)
    {
        var file = await _db.ExpertFiles.SingleOrDefaultAsync(x => x.Id == fileId && x.TenantId == tenantId && x.SoftDeletedAt == null, cancellationToken);
        if (file is null) return new ServiceResult(404, "请求的文件不存在。");
        file.SoftDeletedAt = DateTime.UtcNow;
        file.Status = ExpertFileStatus.Deleted;
        file.UpdatedAt = file.SoftDeletedAt.Value;
        file.RowVersion += 1;
        var attachments = await _db.ExpertFileAttachments.Where(x => x.ExpertFileId == file.Id).ToListAsync(cancellationToken);
        _db.ExpertFileAttachments.RemoveRange(attachments);
        _db.TeamRunAudits.Add(new TeamRunAudit
        {
            TenantId = tenantId,
            ActorUserId = userId,
            ExpertFileId = file.Id,
            Action = "file_delete",
            Result = "success",
            CreatedAt = file.SoftDeletedAt.Value
        });
        await _db.SaveChangesAsync(cancellationToken);
        try { await _storage.DeleteAsync(tenantId, file.Id, string.Empty, cancellationToken); }
        catch (Exception error) { _logger.LogWarning(error, "Expert file storage cleanup failed for file {FileId}", file.Id); }
        return new ServiceResult(200, "文件已删除。", ToSummary(file));
    }

    public async Task<ServiceResult> AttachToExpertAsync(long userId, long tenantId, long expertId, ExpertFileAttachmentRequest request, CancellationToken cancellationToken = default)
    {
        var file = await _db.ExpertFiles.SingleOrDefaultAsync(x => x.Id == request.FileId && x.TenantId == tenantId, cancellationToken);
        if (file is null || file.Status != ExpertFileStatus.Ready) return new ServiceResult(404, "请求的文件不存在或尚未就绪。");
        var expert = await _db.Experts.SingleOrDefaultAsync(x => x.Id == expertId && x.TenantId == tenantId, cancellationToken);
        if (expert is null) return new ServiceResult(404, "请求的专家不存在。");
        _db.ExpertFileAttachments.Add(new ExpertFileAttachment
        {
            TenantId = tenantId,
            ExpertFileId = file.Id,
            ExpertId = expert.Id,
            AgentRunId = null,
            AttachedByUserId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        _db.TeamRunAudits.Add(new TeamRunAudit
        {
            TenantId = tenantId,
            ActorUserId = userId,
            ExpertFileId = file.Id,
            Action = "file_attach",
            Result = "success",
            PayloadJson = JsonSerializer.Serialize(new { expertId }),
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken);
        return new ServiceResult(200, "文件已附加。", ToSummary(file));
    }

    public async Task<ServiceResult> AttachToRunAsync(long userId, long tenantId, long runId, ExpertFileAttachmentRequest request, CancellationToken cancellationToken = default)
    {
        var file = await _db.ExpertFiles.SingleOrDefaultAsync(x => x.Id == request.FileId && x.TenantId == tenantId, cancellationToken);
        if (file is null || file.Status != ExpertFileStatus.Ready) return new ServiceResult(404, "请求的文件不存在或尚未就绪。");
        var run = await _db.AgentRuns.SingleOrDefaultAsync(x => x.Id == runId && x.TenantId == tenantId, cancellationToken);
        if (run is null) return new ServiceResult(404, "请求的 AgentRun 不存在。");
        _db.ExpertFileAttachments.Add(new ExpertFileAttachment
        {
            TenantId = tenantId,
            ExpertFileId = file.Id,
            ExpertId = null,
            AgentRunId = run.Id,
            AttachedByUserId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        _db.TeamRunAudits.Add(new TeamRunAudit
        {
            TenantId = tenantId,
            ActorUserId = userId,
            ExpertFileId = file.Id,
            Action = "file_attach",
            Result = "success",
            PayloadJson = JsonSerializer.Serialize(new { runId }),
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken);
        return new ServiceResult(200, "文件已附加。", ToSummary(file));
    }

    public async Task<ServiceResult> GenerateReadTokenAsync(long userId, long tenantId, long fileId, string purpose, CancellationToken cancellationToken = default)
    {
        var file = await _db.ExpertFiles.SingleOrDefaultAsync(x => x.Id == fileId && x.TenantId == tenantId && x.SoftDeletedAt == null, cancellationToken);
        if (file is null || file.Status != ExpertFileStatus.Ready) return new ServiceResult(404, "请求的文件不存在或尚未就绪。");
        var objectKey = await _db.ExpertFileObjects.Where(x => x.ExpertFileId == file.Id).Select(x => x.ObjectKey).FirstOrDefaultAsync(cancellationToken);
        if (string.IsNullOrEmpty(objectKey)) return new ServiceResult(404, "文件对象尚未提交。");
        var token = await _storage.GenerateReadTokenAsync(tenantId, file.Id, objectKey, purpose, cancellationToken);
        var expires = DateTimeOffset.UtcNow.Add(ReadTokenLifetime).ToUnixTimeSeconds();
        var readUrl = $"api/v1/expert-files/{file.Id}/content?readToken={Uri.EscapeDataString(token)}";
        _db.TeamRunAudits.Add(new TeamRunAudit
        {
            TenantId = tenantId,
            ActorUserId = userId,
            ExpertFileId = file.Id,
            Action = "file_read",
            Result = "success",
            PayloadJson = JsonSerializer.Serialize(new { purpose }),
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken);
        return new ServiceResult(200, "读取令牌已签发。", new ExpertFileReadTokenResponse(file.Id, purpose, token, readUrl, expires));
    }

    public async Task<ServiceResult> RegisterGeneratedFileAsync(long userId, long tenantId, string name, string mimeType, byte[] content, long? attachRunId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(mimeType) || content.Length == 0)
        {
            return new ServiceResult(422, "文件名、MIME 类型与文件内容均不能为空。");
        }

        var now = DateTime.UtcNow;
        var sha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(content)).ToLowerInvariant();
        var file = new ExpertFile
        {
            TenantId = tenantId,
            OwnerUserId = userId,
            Name = name.Trim(),
            MimeType = mimeType.Trim().ToLowerInvariant(),
            SizeBytes = content.Length,
            Sha256 = sha256,
            Status = ExpertFileStatus.Ready,
            QuotaBytes = 0,
            ExpiresAt = now.Add(DefaultRetention),
            CreatedAt = now,
            UpdatedAt = now,
            RowVersion = 1,
            SyncVersion = 1
        };
        _db.ExpertFiles.Add(file);
        await _db.SaveChangesAsync(cancellationToken);

        string objectKey;
        try
        {
            objectKey = await _storage.WriteGeneratedAsync(tenantId, file.Id, file.Name, content, cancellationToken);
        }
        catch (Exception error)
        {
            _logger.LogWarning(error, "Generated expert file write failed for file {FileId}", file.Id);
            file.SoftDeletedAt = DateTime.UtcNow;
            file.Status = ExpertFileStatus.Deleted;
            await _db.SaveChangesAsync(cancellationToken);
            return new ServiceResult(503, "对象存储暂不可用，请稍后重试。");
        }

        _db.ExpertFileObjects.Add(new ExpertFileObject
        {
            ExpertFileId = file.Id,
            ObjectKey = objectKey,
            SizeBytes = content.Length,
            OffsetBytes = 0,
            UploadedAt = now
        });
        if (attachRunId is not null)
        {
            _db.ExpertFileAttachments.Add(new ExpertFileAttachment
            {
                TenantId = tenantId,
                ExpertFileId = file.Id,
                ExpertId = null,
                AgentRunId = attachRunId,
                AttachedByUserId = userId,
                CreatedAt = now,
                UpdatedAt = now
            });
        }
        _db.TeamRunAudits.Add(new TeamRunAudit
        {
            TenantId = tenantId,
            ActorUserId = userId,
            ExpertFileId = file.Id,
            Action = "file_generated",
            Result = "success",
            PayloadJson = JsonSerializer.Serialize(new { attachRunId }),
            CreatedAt = now
        });
        await _db.SaveChangesAsync(cancellationToken);
        return new ServiceResult(201, "生成文件已就绪。", new { fileId = file.Id, file.Status, file.Name, file.MimeType, file.SizeBytes });
    }

    public async Task<ServiceResult> GetContentAsync(long userId, long tenantId, long fileId, CancellationToken cancellationToken = default)
    {
        var file = await _db.ExpertFiles.SingleOrDefaultAsync(x => x.Id == fileId && x.TenantId == tenantId && x.SoftDeletedAt == null, cancellationToken);
        if (file is null || file.Status != ExpertFileStatus.Ready) return new ServiceResult(404, "请求的文件不存在或尚未就绪。");
        var objectKey = await _db.ExpertFileObjects.Where(x => x.ExpertFileId == file.Id).Select(x => x.ObjectKey).FirstOrDefaultAsync(cancellationToken);
        if (string.IsNullOrEmpty(objectKey)) return new ServiceResult(404, "文件对象尚未提交。");
        byte[] bytes;
        try
        {
            bytes = await _storage.ReadAllBytesAsync(tenantId, file.Id, objectKey, cancellationToken);
        }
        catch (Exception error)
        {
            _logger.LogWarning(error, "Expert file content read failed for file {FileId}", file.Id);
            return new ServiceResult(503, "对象存储读取失败，请稍后重试。");
        }
        _db.TeamRunAudits.Add(new TeamRunAudit
        {
            TenantId = tenantId,
            ActorUserId = userId,
            ExpertFileId = file.Id,
            Action = "file_read",
            Result = "success",
            PayloadJson = JsonSerializer.Serialize(new { purpose = "content" }),
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken);
        return new ServiceResult(200, "读取成功。", new GeneratedFileContent(bytes, file.MimeType, file.Name));
    }

    private static ExpertFileSummary ToSummary(ExpertFile file) => new(
        file.Id, file.Name, file.MimeType, file.SizeBytes, file.Status,
        file.ScanProvider, file.ScanCompletedAt, file.RejectionReason,
        file.ExpiresAt, file.SoftDeletedAt, file.CreatedAt, file.UpdatedAt, file.RowVersion);
}
