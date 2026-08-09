using System.Text;
using HomeMind.Business.IServices.Family;
using HomeMind.Business.IServices.Media;
using HomeMind.Business.Services.Family;
using HomeMind.Business.Services.Media;
using HomeMind.Common.Model.Entities.Family;
using HomeMind.Common.Model.ViewModel.Data.Media;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HomeMind.Business.Services.Tests;

/// <summary>
/// 快速剪辑素材登记定向测试（B29）：上传落盘 + ffprobe 元数据（失败不阻塞）、
/// 路径模式越界 403 / 未启用 403 / 不存在 422、二选一校验 422、
/// 列表仅本人、软删除审计与跨用户 404。
/// </summary>
public class ClippingMaterialServicesTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"hm-b29-{Guid.NewGuid():N}");
    private readonly string _allowedRoot = Path.Combine(Path.GetTempPath(), $"hm-b29-allowed-{Guid.NewGuid():N}");

    public ClippingMaterialServicesTests()
    {
        Directory.CreateDirectory(_allowedRoot);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, true); } catch (IOException) { }
        try { if (Directory.Exists(_allowedRoot)) Directory.Delete(_allowedRoot, true); } catch (IOException) { }
    }

    /// <summary>上传模式：文件落盘素材目录、元数据落库、media_file_uploaded 审计、视图含 storage_path。</summary>
    [Fact]
    public async Task Upload_UploadMode_StoresFile_And_Metadata()
    {
        await using var db = NewDb("upload");
        var services = NewServices(db, new FakeFfprobe(new MediaMetadata(30, 1920, 1080, 30)));

        using var content = new MemoryStream(Encoding.UTF8.GetBytes("fake video bytes"));
        var result = await services.UploadAsync(10, 1, new ClippingMaterialUploadRequest(null, "探店.mp4", "video/mp4", content.Length, content), default);

        Assert.Equal(201, result.StatusCode);
        var view = Assert.IsType<ClippingMaterialView>(result.Data);
        Assert.Equal("探店.mp4", view.FileName);
        Assert.Equal(30, view.DurationSeconds);
        Assert.Equal(1920, view.Width);
        Assert.Equal(1080, view.Height);
        Assert.True(File.Exists(view.StoragePath));

        var material = await db.ClippingMaterials.SingleAsync();
        Assert.Equal(10, material.OwnerUserId);
        Assert.False(material.IsDeleted);
        var audit = await db.FamilyAuditLogs.SingleAsync();
        Assert.Equal(FamilyAuditActions.MediaFileUploaded, audit.Action);
        Assert.Equal(FamilyAuditTargetTypes.ClippingMaterial, audit.TargetType);
    }

    /// <summary>ffprobe 解析失败（返回 null）不阻塞素材登记，元数据为空。</summary>
    [Fact]
    public async Task Upload_MetadataFailure_DoesNotBlock()
    {
        await using var db = NewDb("meta-fail");
        var services = NewServices(db, new FakeFfprobe(null));

        using var content = new MemoryStream(Encoding.UTF8.GetBytes("bytes"));
        var result = await services.UploadAsync(10, 1, new ClippingMaterialUploadRequest(null, "a.mp4", "video/mp4", content.Length, content), default);

        Assert.Equal(201, result.StatusCode);
        var view = Assert.IsType<ClippingMaterialView>(result.Data);
        Assert.Null(view.DurationSeconds);
        Assert.Null(view.Width);
    }

    /// <summary>路径模式越出允许根目录返回 403，不落库。</summary>
    [Fact]
    public async Task Upload_PathMode_OutOfAllowedRoot_Returns403()
    {
        await using var db = NewDb("path-out");
        var services = NewServices(db);

        var outside = Path.Combine(Path.GetTempPath(), $"hm-b29-outside-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outside);
        try
        {
            var target = Path.Combine(outside, "a.mp4");
            await File.WriteAllTextAsync(target, "bytes");
            var result = await services.UploadAsync(10, 1, new ClippingMaterialUploadRequest(target, null, null, 0, null), default);

            Assert.Equal(403, result.StatusCode);
            Assert.Equal(0, await db.ClippingMaterials.CountAsync());
        }
        finally
        {
            try { Directory.Delete(outside, true); } catch (IOException) { }
        }
    }

    /// <summary>路径模式未配置允许根目录（默认禁用）返回 403。</summary>
    [Fact]
    public async Task Upload_PathMode_Disabled_Returns403()
    {
        await using var db = NewDb("path-disabled");
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Clipping:StoragePath"] = _tempRoot,
            ["Clipping:AllowedRootPath"] = null,
            ["Clipping:FfprobePath"] = "ffprobe"
        }).Build();
        var services = new ClippingMaterialServices(db, new FakeFfprobe(null), new FamilyAuditLogger(db, NullLogger<FamilyAuditLogger>.Instance), config);

        var result = await services.UploadAsync(10, 1, new ClippingMaterialUploadRequest("/nas/videos/a.mp4", null, null, 0, null), default);

        Assert.Equal(403, result.StatusCode);
    }

    /// <summary>路径模式在允许根目录内且文件存在：登记成功并提取元数据。</summary>
    [Fact]
    public async Task Upload_PathMode_InsideAllowedRoot_Succeeds()
    {
        await using var db = NewDb("path-in");
        var services = NewServices(db, new FakeFfprobe(new MediaMetadata(15, null, null, null)));

        var target = Path.Combine(_allowedRoot, "素材.mp4");
        await File.WriteAllTextAsync(target, "bytes");
        var result = await services.UploadAsync(10, 1, new ClippingMaterialUploadRequest(target, null, null, 0, null), default);

        Assert.Equal(201, result.StatusCode);
        var view = Assert.IsType<ClippingMaterialView>(result.Data);
        Assert.Equal("素材.mp4", view.FileName);
        Assert.Equal(15, view.DurationSeconds);
        Assert.Equal(target, view.StoragePath);
    }

    /// <summary>路径模式文件不存在返回 422。</summary>
    [Fact]
    public async Task Upload_PathMode_FileNotExists_Returns422()
    {
        await using var db = NewDb("path-missing");
        var services = NewServices(db);

        var result = await services.UploadAsync(10, 1, new ClippingMaterialUploadRequest(Path.Combine(_allowedRoot, "missing.mp4"), null, null, 0, null), default);

        Assert.Equal(422, result.StatusCode);
    }

    /// <summary>上传与路径同时提供（或都未提供）返回 422。</summary>
    [Fact]
    public async Task Upload_BothOrNeither_Returns422()
    {
        await using var db = NewDb("both");
        var services = NewServices(db);

        using var content = new MemoryStream(Encoding.UTF8.GetBytes("bytes"));
        var both = await services.UploadAsync(10, 1, new ClippingMaterialUploadRequest("/nas/videos/a.mp4", "a.mp4", "video/mp4", content.Length, content), default);
        var neither = await services.UploadAsync(10, 1, new ClippingMaterialUploadRequest(null, null, null, 0, null), default);

        Assert.Equal(422, both.StatusCode);
        Assert.Equal(422, neither.StatusCode);
    }

    /// <summary>列表仅返回本人未删除素材，他人素材不出现。</summary>
    [Fact]
    public async Task List_OnlyOwnMaterials()
    {
        await using var db = NewDb("list");
        var services = NewServices(db, new FakeFfprobe(null));

        using var content = new MemoryStream(Encoding.UTF8.GetBytes("bytes"));
        await services.UploadAsync(10, 1, new ClippingMaterialUploadRequest(null, "mine.mp4", "video/mp4", content.Length, content), default);
        await services.UploadAsync(11, 1, new ClippingMaterialUploadRequest(null, "other.mp4", "video/mp4", content.Length, content), default);

        var result = await services.ListAsync(10, 1, default);

        Assert.True(result.Succeeded);
        var items = Assert.IsType<List<ClippingMaterialView>>(result.Data);
        var own = Assert.Single(items);
        Assert.Equal("mine.mp4", own.FileName);
    }

    /// <summary>删除本人素材：软删除 + media_file_deleted 审计；再次删除 404。</summary>
    [Fact]
    public async Task Delete_OwnMaterial_Succeeds_And_Audits()
    {
        await using var db = NewDb("delete");
        var services = NewServices(db, new FakeFfprobe(null));

        using var content = new MemoryStream(Encoding.UTF8.GetBytes("bytes"));
        var created = await services.UploadAsync(10, 1, new ClippingMaterialUploadRequest(null, "a.mp4", "video/mp4", content.Length, content), default);
        var id = Assert.IsType<ClippingMaterialView>(created.Data).Id;

        var result = await services.DeleteAsync(10, 1, id, default);

        Assert.Equal(200, result.StatusCode);
        var material = await db.ClippingMaterials.SingleAsync();
        Assert.True(material.IsDeleted);
        var audit = await db.FamilyAuditLogs.SingleAsync(x => x.Action == FamilyAuditActions.MediaFileDeleted);
        Assert.Equal(id, audit.TargetId);

        var again = await services.DeleteAsync(10, 1, id, default);
        Assert.Equal(404, again.StatusCode);
    }

    /// <summary>删除他人素材或不存在返回 404，不写删除审计。</summary>
    [Fact]
    public async Task Delete_OtherUsersMaterial_Returns404()
    {
        await using var db = NewDb("delete-other");
        var services = NewServices(db, new FakeFfprobe(null));

        using var content = new MemoryStream(Encoding.UTF8.GetBytes("bytes"));
        var created = await services.UploadAsync(10, 1, new ClippingMaterialUploadRequest(null, "a.mp4", "video/mp4", content.Length, content), default);
        var id = Assert.IsType<ClippingMaterialView>(created.Data).Id;

        var result = await services.DeleteAsync(11, 1, id, default);

        Assert.Equal(404, result.StatusCode);
        Assert.DoesNotContain(await db.FamilyAuditLogs.ToListAsync(), x => x.Action == FamilyAuditActions.MediaFileDeleted);
    }

    private static HomeMindDbContext NewDb(string name) =>
        new(new DbContextOptionsBuilder<HomeMindDbContext>()
            .UseInMemoryDatabase($"hm-b29-{name}-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private ClippingMaterialServices NewServices(HomeMindDbContext db, FakeFfprobe? ffprobe = null)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Clipping:StoragePath"] = _tempRoot,
            ["Clipping:AllowedRootPath"] = _allowedRoot,
            ["Clipping:FfprobePath"] = "ffprobe"
        }).Build();
        return new ClippingMaterialServices(db, ffprobe ?? new FakeFfprobe(null), new FamilyAuditLogger(db, NullLogger<FamilyAuditLogger>.Instance), config);
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
