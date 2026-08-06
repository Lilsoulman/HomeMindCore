using HomeMind.Business.IServices.AI;
using HomeMind.Common.Model.Entities;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;

namespace HomeMind.Business.Services.AI;

/// <summary>知识条目业务实现，按 JWT 租户隔离。</summary>
public sealed class KnowledgeItemServices : IKnowledgeItemServices
{
    private readonly HomeMindDbContext _db;

    public KnowledgeItemServices(HomeMindDbContext db) => _db = db;

    public async Task<ServiceResult> ListAsync(long userId, long tenantId, string? category, CancellationToken cancellationToken = default)
    {
        var items = await _db.KnowledgeItems
            .Where(x => x.TenantId == tenantId && x.IsActive && (category == null || x.Category == category))
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Take(100)
            .ToListAsync(cancellationToken);
        return new ServiceResult(200, "查询成功。", items.Select(ToView).ToArray());
    }

    public async Task<ServiceResult> CreateAsync(long userId, long tenantId, KnowledgeItemRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Content))
            return new ServiceResult(422, "标题与内容不能为空。");
        var now = DateTime.UtcNow;
        var item = new KnowledgeItem
        {
            TenantId = tenantId,
            Category = string.IsNullOrWhiteSpace(request.Category) ? "general" : request.Category.Trim(),
            Title = request.Title.Trim(),
            Content = request.Content.Trim(),
            Source = string.IsNullOrWhiteSpace(request.Source) ? null : request.Source.Trim(),
            IsActive = true,
            CreatedByUserId = userId,
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.KnowledgeItems.Add(item);
        await _db.SaveChangesAsync(cancellationToken);
        return new ServiceResult(201, "知识条目已添加。", ToView(item));
    }

    public async Task<ServiceResult> DeleteAsync(long userId, long tenantId, long id, CancellationToken cancellationToken = default)
    {
        var item = await _db.KnowledgeItems.SingleOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, cancellationToken);
        if (item is null) return new ServiceResult(404, "请求的知识条目不存在。");
        item.IsActive = false;
        item.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return new ServiceResult(200, "知识条目已停用。", new { id });
    }

    private static object ToView(KnowledgeItem item) => new { item.Id, item.Category, item.Title, item.Content, item.Source, item.IsActive, item.CreatedAt, item.UpdatedAt };
}
