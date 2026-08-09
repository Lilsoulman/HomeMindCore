using HomeMind.Business.IServices.Family;
using HomeMind.Business.IServices.Media;
using HomeMind.Common.Model.Entities;
using HomeMind.Common.Model.Entities.Family;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.Media;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HomeMind.Business.Services.Media;

/// <summary>
/// 快速剪辑素材登记服务（B29）：浏览器上传落盘服务端素材目录并经 ffprobe 提取
/// 时长/分辨率/帧率元数据（失败不阻塞），或路径模式登记配置目录内的既有文件（越界 403）；
/// 素材仅本人可见可删，上传/删除写 media_file_* 审计。上传返回服务端可访问路径
/// （<c>storage_path</c>）由 Web 端回填 Skill 输入 <c>media_location</c>（B24 契约零改动）。
/// </summary>
public sealed class ClippingMaterialServices : IClippingMaterialServices
{
    private const long MaxUploadBytes = 2L * 1024 * 1024 * 1024;
    private readonly HomeMindDbContext _db;
    private readonly IFfprobeExtractor _ffprobe;
    private readonly IFamilyAuditLogger _audit;
    private readonly string _storageRoot;
    private readonly string? _allowedRootPath;

    /// <summary>构造素材登记服务。</summary>
    /// <param name="db">数据库上下文。</param>
    /// <param name="ffprobe">ffprobe 元数据提取器。</param>
    /// <param name="audit">家庭域审计日志写入器。</param>
    /// <param name="config">配置：Clipping:StoragePath（素材目录，默认 data/clipping/materials）、Clipping:AllowedRootPath（路径模式允许根目录，未配置禁用路径模式）。</param>
    public ClippingMaterialServices(HomeMindDbContext db, IFfprobeExtractor ffprobe, IFamilyAuditLogger audit, IConfiguration config)
    {
        _db = db;
        _ffprobe = ffprobe;
        _audit = audit;
        _storageRoot = string.IsNullOrWhiteSpace(config["Clipping:StoragePath"]) ? "data/clipping/materials" : config["Clipping:StoragePath"]!;
        _allowedRootPath = string.IsNullOrWhiteSpace(config["Clipping:AllowedRootPath"]) ? null : config["Clipping:AllowedRootPath"]!;
    }

    /// <inheritdoc />
    public async Task<ServiceResult> UploadAsync(long userId, long tenantId, ClippingMaterialUploadRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Content is not null && request.Content.Length == 0) request = request with { Content = null };
        var isUpload = request.Content is not null;
        var isPath = !string.IsNullOrWhiteSpace(request.FilePath);
        if (isUpload == isPath) return new ServiceResult(422, "素材登记必须且只能提供一种输入：浏览器文件或服务端路径。");

        var now = DateTime.UtcNow;
        var material = new ClippingMaterial
        {
            TenantId = tenantId,
            OwnerUserId = userId,
            FileName = "",
            StoragePath = "",
            FileSize = 0,
            CreatedAt = now,
            UpdatedAt = now
        };

        if (isUpload)
        {
            if (request.FileSize > MaxUploadBytes) return new ServiceResult(422, "素材文件超过 2GB 上限。");
            var safeName = Path.GetFileName(request.FileName ?? "media");
            if (string.IsNullOrWhiteSpace(safeName)) safeName = "media";
            var directory = Path.Combine(_storageRoot, userId.ToString(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var targetPath = Path.Combine(directory, safeName);
            try
            {
                await using (var fileStream = File.Create(targetPath))
                {
                    await request.Content!.CopyToAsync(fileStream, cancellationToken);
                }
                material.FileName = safeName;
                material.StoragePath = targetPath;
                material.ContentType = string.IsNullOrWhiteSpace(request.ContentType) ? null : request.ContentType;
                material.FileSize = new FileInfo(targetPath).Length;
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                TryDeleteFile(targetPath);
                return new ServiceResult(500, "素材存储失败，请稍后重试。");
            }
        }
        else
        {
            if (_allowedRootPath is null) return new ServiceResult(403, "路径模式未启用，请使用浏览器上传素材。");
            var fullPath = Path.GetFullPath(request.FilePath!);
            var allowedRoot = Path.GetFullPath(_allowedRootPath);
            if (!fullPath.Equals(allowedRoot, StringComparison.OrdinalIgnoreCase)
                && !fullPath.StartsWith(allowedRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                return new ServiceResult(403, "素材路径不在允许的素材根目录内。");
            if (!File.Exists(fullPath)) return new ServiceResult(422, "素材路径不存在或不可访问。");
            material.FileName = Path.GetFileName(fullPath);
            material.StoragePath = fullPath;
            material.FileSize = new FileInfo(fullPath).Length;
        }

        var metadata = await _ffprobe.ExtractAsync(material.StoragePath, cancellationToken);
        if (metadata is not null)
        {
            material.DurationSeconds = metadata.DurationSeconds;
            material.Width = metadata.Width;
            material.Height = metadata.Height;
            material.Fps = metadata.Fps;
        }

        _db.ClippingMaterials.Add(material);
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync(tenantId, userId, FamilyAuditActions.MediaFileUploaded, FamilyAuditTargetTypes.ClippingMaterial,
            material.Id, null, new { file_name = material.FileName, size_bytes = material.FileSize }, null, null, cancellationToken);

        return new ServiceResult(201, "素材已登记。", ToView(material));
    }

    /// <inheritdoc />
    public async Task<ServiceResult> ListAsync(long userId, long tenantId, CancellationToken cancellationToken = default)
    {
        var items = await _db.ClippingMaterials
            .Where(x => x.TenantId == tenantId && x.OwnerUserId == userId && !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => ToView(x))
            .ToListAsync(cancellationToken);
        return new ServiceResult(200, "查询成功。", items);
    }

    /// <inheritdoc />
    public async Task<ServiceResult> DeleteAsync(long userId, long tenantId, long materialId, CancellationToken cancellationToken = default)
    {
        var material = await _db.ClippingMaterials.SingleOrDefaultAsync(
            x => x.Id == materialId && x.TenantId == tenantId && x.OwnerUserId == userId && !x.IsDeleted, cancellationToken);
        if (material is null) return new ServiceResult(404, "素材不存在或已删除。");

        material.IsDeleted = true;
        material.Status = ClippingMaterialStatus.Deleted;
        material.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync(tenantId, userId, FamilyAuditActions.MediaFileDeleted, FamilyAuditTargetTypes.ClippingMaterial,
            material.Id, null, new { file_name = material.FileName }, null, null, cancellationToken);
        return new ServiceResult(200, "素材已删除。");
    }

    private static ClippingMaterialView ToView(ClippingMaterial material) => new(
        material.Id, material.FileName, material.ContentType, material.FileSize,
        material.DurationSeconds, material.Width, material.Height, material.StoragePath, material.CreatedAt);

    private static void TryDeleteFile(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
