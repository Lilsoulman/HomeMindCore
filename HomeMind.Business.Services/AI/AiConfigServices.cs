using HomeMind.Business.IServices.AI;
using HomeMind.Common.Infrastructure;
using HomeMind.Common.Model.Entities;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.AI;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;

namespace HomeMind.Business.Services.AI;

/// <summary>AI 配置业务实现：按用户隔离，API 密钥经 SecretProtector 加密存储，永不回传明文。</summary>
public sealed class AiConfigServices : IAiConfigServices
{
    private readonly HomeMindDbContext _db;
    private readonly SecretProtector _secretProtector;

    public AiConfigServices(HomeMindDbContext db, SecretProtector secretProtector)
    {
        _db = db;
        _secretProtector = secretProtector;
    }

    public async Task<ServiceResult> GetAsync(long userId, CancellationToken cancellationToken = default)
    {
        var item = await _db.AiConfigs.FindAsync(new object[] { userId }, cancellationToken);
        return item is null
            ? new ServiceResult(200, "查询成功。", new { Endpoint = (string?)null, Model = (string?)null, Temperature = 0.7, HasApiKey = false })
            : new ServiceResult(200, "查询成功。", ToView(item));
    }

    public async Task<ServiceResult> SaveAsync(long userId, AiConfigRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Endpoint) || !Uri.TryCreate(request.Endpoint.Trim(), UriKind.Absolute, out var endpoint) || endpoint.Scheme is not ("http" or "https"))
            return new ServiceResult(422, "API 端点必须是合法的 http/https 地址。");
        if (string.IsNullOrWhiteSpace(request.Model)) return new ServiceResult(422, "请填写模型名称。");
        if (request.Temperature is < 0 or > 1) return new ServiceResult(422, "温度参数必须在 0~1 之间。");

        var item = await _db.AiConfigs.FindAsync(new object[] { userId }, cancellationToken);
        if (item is null)
        {
            item = new AiConfig { UserId = userId };
            _db.AiConfigs.Add(item);
        }

        item.Endpoint = endpoint.ToString();
        item.Model = request.Model.Trim();
        item.Temperature = request.Temperature;
        if (!string.IsNullOrWhiteSpace(request.ApiKey)) item.ApiKeyEncrypted = _secretProtector.Encrypt(request.ApiKey.Trim());
        item.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return new ServiceResult(200, "保存成功。", ToView(item));
    }

    private static object ToView(AiConfig x) => new { x.Endpoint, x.Model, x.Temperature, HasApiKey = x.ApiKeyEncrypted is { Length: > 0 } };
}
