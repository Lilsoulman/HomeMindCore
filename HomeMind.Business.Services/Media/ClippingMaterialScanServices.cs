using System.Security.Cryptography;
using System.Text;
using HomeMind.Business.IServices.Media;
using HomeMind.Common.Model.Entities;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HomeMind.Business.Services.Media;

/// <summary>
/// 快速剪辑素材自动发现服务（B38）：后台 Worker 调用，扫描素材根目录第一级用户目录，
/// 登记最近修改时间窗内、扩展名白名单内的媒体文件（ffprobe 元数据失败不阻塞）；
/// 按 storage_path 精确查重（上传已登记文件不重复登记）并以 directory_key（路径 SHA-256）
/// 与数据库唯一索引兜底；目录不可达或用户不存在时静默降级，不抛异常、不写审计。
/// </summary>
public sealed class ClippingMaterialScanServices : IClippingMaterialScanServices
{
    private static readonly string[] DefaultAllowedExtensions = [".mp4", ".mov", ".mkv", ".avi", ".webm", ".mp3", ".wav", ".m4a", ".flac"];

    private readonly HomeMindDbContext _db;
    private readonly IFfprobeExtractor _ffprobe;
    private readonly ILogger<ClippingMaterialScanServices> _logger;
    private readonly string _storageRoot;
    private readonly bool _enabled;
    private readonly int _maxAgeHours;
    private readonly string[] _allowedExtensions;

    /// <summary>构造素材自动发现服务。</summary>
    /// <param name="db">数据库上下文。</param>
    /// <param name="ffprobe">ffprobe 元数据提取器。</param>
    /// <param name="config">配置：Clipping:Scan（Enabled 默认 true/MaxAgeHours 默认 24/AllowedExtensions 默认媒体扩展名白名单）、Clipping:StoragePath（素材根目录）。</param>
    /// <param name="logger">日志器。</param>
    public ClippingMaterialScanServices(HomeMindDbContext db, IFfprobeExtractor ffprobe, IConfiguration config, ILogger<ClippingMaterialScanServices> logger)
    {
        _db = db;
        _ffprobe = ffprobe;
        _logger = logger;
        _storageRoot = string.IsNullOrWhiteSpace(config["Clipping:StoragePath"]) ? "data/clipping/materials" : config["Clipping:StoragePath"]!;
        _enabled = config.GetValue<bool?>("Clipping:Scan:Enabled") ?? true;
        _maxAgeHours = config.GetValue<int?>("Clipping:Scan:MaxAgeHours") ?? 24;
        _allowedExtensions = config.GetSection("Clipping:Scan:AllowedExtensions").Get<string[]>()
            ?? DefaultAllowedExtensions;
    }

    /// <inheritdoc />
    public async Task<ServiceResult> ScanAsync(CancellationToken cancellationToken = default)
    {
        if (!_enabled) return new ServiceResult(200, "素材自动发现未启用。", 0);
        if (!Directory.Exists(_storageRoot))
        {
            _logger.LogWarning("素材根目录不可达，本轮自动发现跳过：{Root}", _storageRoot);
            return new ServiceResult(200, "素材根目录不可达，本轮扫描跳过。", 0);
        }

        var registered = 0;
        var oldest = DateTime.UtcNow.AddHours(-_maxAgeHours);
        var allowed = new HashSet<string>(_allowedExtensions, StringComparer.OrdinalIgnoreCase);
        foreach (var userDirectory in Directory.GetDirectories(_storageRoot))
        {
            var ownerId = ResolveOwnerId(userDirectory);
            if (ownerId is null) continue;
            // 租户关联在 tenant_members（users 表无 tenant 列）：取该用户首个 active 成员行；无归属跳过。
            var tenantId = await _db.TenantMembers.Where(x => x.UserId == ownerId.Value && x.Status == "active")
                .Select(x => x.TenantId).FirstOrDefaultAsync(cancellationToken);
            if (tenantId == 0) continue;

            var files = SafeEnumerateFiles(userDirectory);
            foreach (var filePath in files)
            {
                if (!allowed.Contains(Path.GetExtension(filePath))) continue;
                try
                {
                    if (File.GetLastWriteTimeUtc(filePath) < oldest) continue;
                }
                catch (IOException) { continue; }
                catch (UnauthorizedAccessException) { continue; }

                if (await TryRegisterAsync(ownerId.Value, tenantId, filePath, cancellationToken)) registered++;
            }
        }
        return new ServiceResult(200, "素材自动发现完成。", registered);
    }

    /// <summary>登记单个未收录的素材文件；已登记（storage_path 或 directory_key 命中）返回 false。</summary>
    private async Task<bool> TryRegisterAsync(long ownerUserId, long tenantId, string filePath, CancellationToken cancellationToken)
    {
        try
        {
            var fullPath = Path.GetFullPath(filePath);
            var directoryKey = ComputeDirectoryKey(fullPath);
            var exists = await _db.ClippingMaterials.AnyAsync(x => x.TenantId == tenantId && !x.IsDeleted
                && (x.StoragePath == fullPath || x.DirectoryKey == directoryKey), cancellationToken);
            if (exists) return false;

            var metadata = await _ffprobe.ExtractAsync(fullPath, cancellationToken);
            var material = new ClippingMaterial
            {
                TenantId = tenantId,
                OwnerUserId = ownerUserId,
                FileName = Path.GetFileName(fullPath),
                StoragePath = fullPath,
                FileSize = new FileInfo(fullPath).Length,
                DurationSeconds = metadata?.DurationSeconds,
                Width = metadata?.Width,
                Height = metadata?.Height,
                Fps = metadata?.Fps,
                SourceType = "scan",
                DirectoryKey = directoryKey,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.ClippingMaterials.Add(material);
            await _db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            // 并发扫描唯一键冲突：另一轮已登记，静默跳过。
            return false;
        }
        catch (OperationCanceledException) { throw; }
        catch (IOException error)
        {
            _logger.LogWarning(error, "素材登记文件不可访问，跳过：{Path}", filePath);
            return false;
        }
        catch (UnauthorizedAccessException error)
        {
            _logger.LogWarning(error, "素材登记文件无访问权限，跳过：{Path}", filePath);
            return false;
        }
    }

    /// <summary>递归枚举目录内文件，目录不可访问时返回空集合并记录警告（静默降级）。</summary>
    private string[] SafeEnumerateFiles(string root)
    {
        try { return Directory.GetFiles(root, "*", SearchOption.AllDirectories); }
        catch (IOException error)
        {
            // 目录不可达静默降级：仅日志，不使本轮扫描失败。
            _logger.LogWarning(error, "素材子目录不可访问，跳过：{Root}", root);
            return [];
        }
        catch (UnauthorizedAccessException error)
        {
            _logger.LogWarning(error, "素材子目录无访问权限，跳过：{Root}", root);
            return [];
        }
    }

    /// <summary>从用户目录名解析归属用户标识；非数字目录（如系统目录）返回 null 跳过。</summary>
    private static long? ResolveOwnerId(string directoryPath)
    {
        var name = Path.GetFileName(directoryPath.TrimEnd(Path.DirectorySeparatorChar));
        return long.TryParse(name, out var ownerId) ? ownerId : null;
    }

    /// <summary>计算素材路径的 SHA-256 十六进制小写去重键（64 字符，与迁移 directory_key 定长对齐）。</summary>
    private static string ComputeDirectoryKey(string fullPath) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fullPath))).ToLowerInvariant();
}
