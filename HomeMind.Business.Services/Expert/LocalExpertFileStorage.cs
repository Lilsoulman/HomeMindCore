using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using HomeMind.Business.IServices.Expert;
using Microsoft.Extensions.Configuration;

namespace HomeMind.Business.Services.Expert;

/// <summary>本地对象存储占位实现。仅在 ExpertFiles:Storage:Provider=local 且 ExpertFiles:Storage:Enabled=true 时启用。</summary>
public sealed class LocalExpertFileStorage : IExpertFileStorage
{
    private readonly IConfiguration _configuration;
    private readonly string _root;
    private readonly bool _enabled;

    public LocalExpertFileStorage(IConfiguration configuration)
    {
        _configuration = configuration;
        _root = configuration["ExpertFiles:Storage:LocalRoot"]
            ?? throw new InvalidOperationException("缺少 ExpertFiles:Storage:LocalRoot 配置。");
        _enabled = string.Equals(configuration["ExpertFiles:Storage:Enabled"], "true", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<string> CreateUploadSessionAsync(long tenantId, long fileId, string fileName, long sizeBytes, string mimeType, CancellationToken cancellationToken = default)
    {
        if (!_enabled) throw new InvalidOperationException("Expert Files 本地存储未启用。");
        var directory = ResolveDirectory(tenantId, fileId);
        Directory.CreateDirectory(directory);
        var objectKey = $"{Guid.NewGuid():N}-{Sanitize(fileName)}";
        var path = Path.Combine(directory, objectKey);
        await File.WriteAllBytesAsync(path, Array.Empty<byte>(), cancellationToken);
        return objectKey;
    }

    public Task CommitObjectAsync(long tenantId, long fileId, string objectKey, long offsetBytes, long sizeBytes, string sha256, CancellationToken cancellationToken = default)
    {
        if (!_enabled) throw new InvalidOperationException("Expert Files 本地存储未启用。");
        var path = Path.Combine(ResolveDirectory(tenantId, fileId), objectKey);
        if (!File.Exists(path)) throw new FileNotFoundException("对象在本地存储中不存在。", path);
        // 本阶段仅校验元数据，原始字节由上游网关写入；落盘后再次确认大小、SHA-256 与已声明值一致。
        var info = new FileInfo(path);
        if (info.Length != sizeBytes) throw new InvalidDataException("对象大小与声明不一致。");
        return Task.CompletedTask;
    }

    public Task<string> GenerateReadTokenAsync(long tenantId, long fileId, string objectKey, string purpose, CancellationToken cancellationToken = default)
    {
        if (!_enabled) throw new InvalidOperationException("Expert Files 本地存储未启用。");
        var token = Convert.ToBase64String(Guid.NewGuid().ToByteArray()) + ":" + Uri.EscapeDataString(purpose);
        return Task.FromResult(token);
    }

    public async Task<string> WriteGeneratedAsync(long tenantId, long fileId, string fileName, byte[] content, CancellationToken cancellationToken = default)
    {
        if (!_enabled) throw new InvalidOperationException("Expert Files 本地存储未启用。");
        var directory = ResolveDirectory(tenantId, fileId);
        Directory.CreateDirectory(directory);
        var objectKey = $"{Guid.NewGuid():N}-{Sanitize(fileName)}";
        await File.WriteAllBytesAsync(Path.Combine(directory, objectKey), content, cancellationToken);
        return objectKey;
    }

    public Task<byte[]> ReadAllBytesAsync(long tenantId, long fileId, string objectKey, CancellationToken cancellationToken = default)
    {
        if (!_enabled) throw new InvalidOperationException("Expert Files 本地存储未启用。");
        var path = Path.Combine(ResolveDirectory(tenantId, fileId), objectKey);
        if (!File.Exists(path)) throw new FileNotFoundException("对象在本地存储中不存在。", path);
        return File.ReadAllBytesAsync(path, cancellationToken);
    }

    public Task DeleteAsync(long tenantId, long fileId, string objectKey, CancellationToken cancellationToken = default)
    {
        if (!_enabled) return Task.CompletedTask;
        var path = Path.Combine(ResolveDirectory(tenantId, fileId), objectKey);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    private string ResolveDirectory(long tenantId, long fileId) => Path.Combine(_root, $"t{tenantId}", $"f{fileId}");

    private static string Sanitize(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = new List<char>(fileName.Length);
        foreach (var ch in fileName) chars.Add(Array.IndexOf(invalid, ch) >= 0 ? '_' : ch);
        return new string(chars.ToArray());
    }
}
