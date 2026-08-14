using HomeMind.Business.IServices.Media;
using HomeMind.Business.Services.Media;
using HomeMind.Common.Model.Entities;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HomeMind.Business.Services.Tests;

/// <summary>
/// 素材自动发现定向测试（B38）：扫描登记与 owner/tenant 推导、storage_path 与 directory_key 双重去重、
/// ffprobe 元数据失败不阻塞、目录不可达静默降级、扩展名白名单、最近修改时间窗过滤、用户/目录隔离。
/// </summary>
public class ClippingMaterialScanServicesTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"hm-b38-{Guid.NewGuid():N}");

    public ClippingMaterialScanServicesTests()
    {
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, true); } catch (IOException) { }
    }

    /// <summary>扫描登记：用户目录内白名单文件登记，owner/tenant 正确、source_type=scan、ffprobe 元数据落库。</summary>
    [Fact]
    public async Task Scan_Registers_NewFile_With_Metadata_And_Ownership()
    {
        await using var db = NewDb("register");
        SeedUser(db, 10, 1);
        var file = NewFile(10, "素材.mp4");
        var services = NewServices(db, new FakeFfprobe(new MediaMetadata(30, 1920, 1080, 30)));

        var result = await services.ScanAsync();

        Assert.Equal(200, result.StatusCode);
        Assert.Equal(1, result.Data);
        var material = await db.ClippingMaterials.SingleAsync();
        Assert.Equal(10, material.OwnerUserId);
        Assert.Equal(1, material.TenantId);
        Assert.Equal("scan", material.SourceType);
        Assert.Equal(30, material.DurationSeconds);
        Assert.Equal(1920, material.Width);
        Assert.Equal(file, material.StoragePath);
        Assert.NotNull(material.DirectoryKey);
        Assert.Equal(64, material.DirectoryKey!.Length);
    }

    /// <summary>重复扫描同一文件不重复登记（directory_key 去重）。</summary>
    [Fact]
    public async Task Scan_SecondRun_DoesNotDuplicate()
    {
        await using var db = NewDb("dedup");
        SeedUser(db, 10, 1);
        NewFile(10, "a.mp4");
        var services = NewServices(db, new FakeFfprobe(null));

        var first = await services.ScanAsync();
        var second = await services.ScanAsync();

        Assert.Equal(1, first.Data);
        Assert.Equal(0, second.Data);
        Assert.Equal(1, await db.ClippingMaterials.CountAsync());
    }

    /// <summary>上传已登记的同路径文件（upload 行）扫描时跳过，不重复登记。</summary>
    [Fact]
    public async Task Scan_Skips_AlreadyRegistered_UploadPath()
    {
        await using var db = NewDb("upload-row");
        SeedUser(db, 10, 1);
        var file = NewFile(10, "b.mp4");
        db.ClippingMaterials.Add(new ClippingMaterial
        {
            TenantId = 1, OwnerUserId = 10, FileName = "b.mp4", StoragePath = file, FileSize = 1,
            SourceType = "upload", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var services = NewServices(db, new FakeFfprobe(null));

        var result = await services.ScanAsync();

        Assert.Equal(0, result.Data);
        Assert.Single(await db.ClippingMaterials.ToListAsync());
    }

    /// <summary>ffprobe 元数据解析失败不阻塞登记，元数据字段为空。</summary>
    [Fact]
    public async Task Scan_MetadataFailure_DoesNotBlock()
    {
        await using var db = NewDb("meta-fail");
        SeedUser(db, 10, 1);
        NewFile(10, "c.mp4");
        var services = NewServices(db, new FakeFfprobe(null));

        var result = await services.ScanAsync();

        Assert.Equal(1, result.Data);
        var material = await db.ClippingMaterials.SingleAsync();
        Assert.Null(material.DurationSeconds);
        Assert.Null(material.Width);
    }

    /// <summary>素材根目录不可达（不存在）时静默降级返回成功，不抛异常。</summary>
    [Fact]
    public async Task Scan_RootUnreachable_SilentlyDegrades()
    {
        await using var db = NewDb("unreachable");
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Clipping:StoragePath"] = Path.Combine(Path.GetTempPath(), $"hm-b38-missing-{Guid.NewGuid():N}")
        }).Build();
        var services = new ClippingMaterialScanServices(db, new FakeFfprobe(null), config, NullLogger<ClippingMaterialScanServices>.Instance);

        var result = await services.ScanAsync();

        Assert.Equal(200, result.StatusCode);
        Assert.Equal(0, result.Data);
        Assert.Equal(0, await db.ClippingMaterials.CountAsync());
    }

    /// <summary>扩展名白名单外的文件（如 .txt/.exe）不登记。</summary>
    [Fact]
    public async Task Scan_Ignores_NonAllowedExtensions()
    {
        await using var db = NewDb("ext");
        SeedUser(db, 10, 1);
        NewFile(10, "notes.txt");
        NewFile(10, "app.exe");
        var services = NewServices(db, new FakeFfprobe(null));

        var result = await services.ScanAsync();

        Assert.Equal(0, result.Data);
        Assert.Equal(0, await db.ClippingMaterials.CountAsync());
    }

    /// <summary>最近修改时间窗口外的文件不登记（LastWriteTimeUtc 早于窗口起点）。</summary>
    [Fact]
    public async Task Scan_Skips_FilesOutside_TimeWindow()
    {
        await using var db = NewDb("window");
        SeedUser(db, 10, 1);
        var old = NewFile(10, "old.mp4");
        File.SetLastWriteTimeUtc(old, DateTime.UtcNow.AddDays(-7));
        var services = NewServices(db, new FakeFfprobe(null));

        var result = await services.ScanAsync();

        Assert.Equal(0, result.Data);
        Assert.Equal(0, await db.ClippingMaterials.CountAsync());
    }

    /// <summary>owner 隔离：非数字目录与 users 表不存在的用户目录跳过；两用户文件各归其主。</summary>
    [Fact]
    public async Task Scan_Ownership_Isolation_BetweenUsers()
    {
        await using var db = NewDb("isolation");
        SeedUser(db, 10, 1);
        SeedUser(db, 11, 2);
        NewFile(10, "mine.mp4");
        NewFile(11, "theirs.mov");
        Directory.CreateDirectory(Path.Combine(_tempRoot, "unknown-folder"));
        await File.WriteAllTextAsync(Path.Combine(_tempRoot, "unknown-folder", "x.mp4"), "bytes");
        var services = NewServices(db, new FakeFfprobe(null));

        var result = await services.ScanAsync();

        Assert.Equal(2, result.Data);
        var materials = await db.ClippingMaterials.OrderBy(x => x.OwnerUserId).ToListAsync();
        Assert.Equal(2, materials.Count);
        Assert.Equal(10, materials[0].OwnerUserId);
        Assert.Equal(1, materials[0].TenantId);
        Assert.Equal(11, materials[1].OwnerUserId);
        Assert.Equal(2, materials[1].TenantId);
    }

    private static HomeMindDbContext NewDb(string name) =>
        new(new DbContextOptionsBuilder<HomeMindDbContext>()
            .UseInMemoryDatabase($"hm-b38-{name}-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private ClippingMaterialScanServices NewServices(HomeMindDbContext db, FakeFfprobe? ffprobe = null)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Clipping:StoragePath"] = _tempRoot
        }).Build();
        return new ClippingMaterialScanServices(db, ffprobe ?? new FakeFfprobe(null), config, NullLogger<ClippingMaterialScanServices>.Instance);
    }

    /// <summary>在用户目录内创建一个媒体占位文件并返回绝对路径。</summary>
    private string NewFile(long userId, string fileName)
    {
        var directory = Path.Combine(_tempRoot, userId.ToString());
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileName);
        File.WriteAllText(path, "bytes");
        return path;
    }

    /// <summary>Seed 一个 tenant_members 行供租户推导（users 表无租户列，租户关联在成员表）。</summary>
    private static void SeedUser(HomeMindDbContext db, long userId, long tenantId)
    {
        db.TenantMembers.Add(new TenantMember { TenantId = tenantId, UserId = userId, Status = "active", JoinedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        db.SaveChanges();
    }

    /// <summary>ffprobe 提取器测试替身：返回固定元数据或 null（解析失败）。</summary>
    private sealed class FakeFfprobe : IFfprobeExtractor
    {
        private readonly MediaMetadata? _metadata;

        public FakeFfprobe(MediaMetadata? metadata) => _metadata = metadata;

        public Task<MediaMetadata?> ExtractAsync(string filePath, CancellationToken cancellationToken = default) =>
            Task.FromResult(_metadata);
    }
}
