using System.Text.Json;
using HomeMind.Business.IServices.AI;
using HomeMind.Common.Model.Entities;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.AI;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;

namespace HomeMind.Business.Services.AI;

/// <summary>AI 技能业务实现，负责租户隔离和技能范围校验。</summary>
public sealed class AiSkillServices : IAiSkillServices
{
    private readonly HomeMindDbContext _db;
    public AiSkillServices(HomeMindDbContext db) => _db = db;

    public async Task<ServiceResult> ListAsync(long userId, long tenantId, CancellationToken cancellationToken = default)
    {
        var items = await _db.AiSkills.Where(x => x.TenantId == tenantId && x.UserId == userId && x.DeletedAt == null).OrderByDescending(x => x.IsBuiltin).ThenBy(x => x.Name).ToListAsync(cancellationToken);
        return new ServiceResult(200, "查询成功。", items.Select(ToView));
    }

    public async Task<ServiceResult> CreateAsync(long userId, long tenantId, SkillRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Prompt)) return new ServiceResult(422, "请填写技能名称和提示词。");
        if (!IsJsonArray(request.Scopes)) return new ServiceResult(422, "技能适用范围格式无效。");
        var item = new AiSkill { TenantId = tenantId, UserId = userId, Name = request.Name.Trim(), Prompt = request.Prompt, Scopes = request.Scopes ?? "[]", IsBuiltin = false, IsActive = request.IsActive ?? true };
        _db.AiSkills.Add(item);
        await _db.SaveChangesAsync(cancellationToken);
        return new ServiceResult(201, "创建成功。", ToView(item));
    }

    public async Task<ServiceResult> UpdateAsync(long userId, long tenantId, long id, SkillRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Scopes is not null && !IsJsonArray(request.Scopes)) return new ServiceResult(422, "技能适用范围格式无效。");
        var item = await _db.AiSkills.SingleOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId && x.UserId == userId && x.DeletedAt == null, cancellationToken);
        if (item is null) return new ServiceResult(404, "请求的资源不存在。");
        item.Name = request.Name ?? item.Name; item.Prompt = request.Prompt ?? item.Prompt; item.Scopes = request.Scopes ?? item.Scopes; item.IsActive = request.IsActive ?? item.IsActive;
        await _db.SaveChangesAsync(cancellationToken);
        return new ServiceResult(200, "更新成功。", new { id });
    }

    public async Task<ServiceResult> DeleteAsync(long userId, long tenantId, long id, CancellationToken cancellationToken = default)
    {
        var item = await _db.AiSkills.SingleOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId && x.UserId == userId && x.DeletedAt == null, cancellationToken);
        if (item is null) return new ServiceResult(404, "请求的资源不存在。");
        item.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return new ServiceResult(200, "删除成功。", new { id });
    }

    private static bool IsJsonArray(string? value) { if (string.IsNullOrWhiteSpace(value)) return true; try { return JsonDocument.Parse(value).RootElement.ValueKind == JsonValueKind.Array; } catch (JsonException) { return false; } }
    private static object ToView(AiSkill x) => new { x.Id, x.Name, x.Prompt, x.Scopes, x.IsBuiltin, x.IsActive, x.CreatedAt, x.UpdatedAt };
}
