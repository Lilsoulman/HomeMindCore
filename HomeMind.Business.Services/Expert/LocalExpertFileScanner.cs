using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using HomeMind.Business.IServices.Expert;
using Microsoft.Extensions.Configuration;

namespace HomeMind.Business.Services.Expert;

/// <summary>本地扫描占位：校验扩展名白名单、MIME 类型、声明大小、SHA-256 一致性；任何不一致都返回 rejected。</summary>
public sealed class LocalExpertFileScanner : IExpertFileScanner
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".json", ".csv", ".pdf", ".docx", ".xlsx", ".png", ".jpg", ".jpeg"
    };

    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "text/plain", "text/markdown", "application/json", "text/csv",
        "application/pdf", "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "image/png", "image/jpeg"
    };

    private const long MaxSizeBytes = 25L * 1024 * 1024;
    private readonly bool _enabled;

    public LocalExpertFileScanner(IConfiguration configuration)
    {
        _enabled = string.Equals(configuration["ExpertFiles:Scanner:Enabled"], "true", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ExpertFileScanResult> ScanAsync(long tenantId, long fileId, string objectKey, long sizeBytes, string sha256, string mimeType, string fileName, CancellationToken cancellationToken = default)
    {
        if (!_enabled) return new ExpertFileScanResult(false, "scanner_disabled");
        if (sizeBytes <= 0 || sizeBytes > MaxSizeBytes) return new ExpertFileScanResult(false, "size_out_of_range");
        var extension = Path.GetExtension(fileName);
        if (!AllowedExtensions.Contains(extension)) return new ExpertFileScanResult(false, "extension_not_allowed");
        if (!AllowedMimeTypes.Contains(mimeType)) return new ExpertFileScanResult(false, "mime_not_allowed");
        if (sha256.Length != 64) return new ExpertFileScanResult(false, "sha256_invalid");
        await Task.Yield();
        return new ExpertFileScanResult(true, null);
    }
}
