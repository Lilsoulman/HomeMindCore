using HomeMind.Business.IServices.Family;
using HomeMind.Business.IServices.Life;
using HomeMind.Common.Model.Entities.Family;
using HomeMind.Common.Model.Entities.Life;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.Life;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;

namespace HomeMind.Business.Services.Life;

/// <summary>
/// 个人偏好收藏服务实现。可见性规则：private 仅归属成员本人可读写；family 家庭内可读，
/// 写仍限本人或家庭管理员。全部写操作经 <see cref="IFamilyAuditLogger"/> 记录 favorite_* 审计。
/// </summary>
public sealed class FavoriteService : IFavoriteService
{
    private readonly HomeMindDbContext _db;
    private readonly IFamilyAuditLogger _audit;

    /// <summary>构造收藏服务。</summary>
    /// <param name="db">数据库上下文。</param>
    /// <param name="audit">家庭审计日志写入器。</param>
    public FavoriteService(HomeMindDbContext db, IFamilyAuditLogger audit)
    {
        _db = db;
        _audit = audit;
    }

    /// <inheritdoc />
    public async Task<ServiceResult> ListAsync(long homeId, long actorUserId, string? category, string? visibility, CancellationToken cancellationToken = default)
    {
        if (category is not null && !PersonalFavoriteCategory.IsValid(category))
            return new ServiceResult(422, "收藏分类仅支持 restaurant、travel 或 material。");
        if (visibility is not null && !PersonalFavoriteVisibility.IsValid(visibility))
            return new ServiceResult(422, "收藏可见性仅支持 private 或 family。");

        var owner = await ResolveOwnerMemberAsync(homeId, actorUserId, null, cancellationToken);
        if (owner is null) return new ServiceResult(422, "当前家庭尚无成员档案，请先创建家庭成员。");

        var query = _db.PersonalFavorites.Where(x => x.HomeId == homeId && x.DeletedAt == null);
        if (category is not null) query = query.Where(x => x.Category == category);
        if (visibility is not null) query = query.Where(x => x.Visibility == visibility);
        var items = await query.OrderByDescending(x => x.UpdatedAt).ToListAsync(cancellationToken);
        var visible = items.Where(x => x.Visibility == PersonalFavoriteVisibility.Family || x.OwnerMemberId == owner.Id).ToArray();
        return new ServiceResult(200, "查询成功。", visible.Select(ToView).ToArray());
    }

    /// <inheritdoc />
    public async Task<ServiceResult> GetAsync(long homeId, long actorUserId, long favoriteId, CancellationToken cancellationToken = default)
    {
        var favorite = await _db.PersonalFavorites.SingleOrDefaultAsync(x => x.Id == favoriteId && x.HomeId == homeId && x.DeletedAt == null, cancellationToken);
        if (favorite is null) return new ServiceResult(404, "请求的收藏不存在。");
        var owner = await ResolveOwnerMemberAsync(homeId, actorUserId, null, cancellationToken);
        if (owner is null || (favorite.Visibility == PersonalFavoriteVisibility.Private && favorite.OwnerMemberId != owner.Id))
            return new ServiceResult(404, "请求的收藏不存在。");
        return new ServiceResult(200, "查询成功。", ToView(favorite));
    }

    /// <inheritdoc />
    public async Task<ServiceResult> CreateAsync(long homeId, long actorUserId, FavoriteCreateRequest request, CancellationToken cancellationToken = default)
    {
        var validation = Validate(request.Category, request.Name, request.Visibility);
        if (validation is not null) return validation;
        var owner = await ResolveOwnerMemberAsync(homeId, actorUserId, request.OwnerMemberId, cancellationToken);
        if (owner is null) return new ServiceResult(422, "当前家庭尚无成员档案，请先创建家庭成员。");

        var now = DateTime.UtcNow;
        var favorite = new PersonalFavorite
        {
            HomeId = homeId,
            OwnerMemberId = owner.Id,
            Category = request.Category.Trim(),
            Name = request.Name.Trim(),
            DetailJson = string.IsNullOrWhiteSpace(request.DetailJson) ? null : request.DetailJson,
            Visibility = request.Visibility.Trim(),
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.PersonalFavorites.Add(favorite);
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync(homeId, actorUserId, FamilyAuditActions.FavoriteCreate, FamilyAuditTargetTypes.PersonalFavorite, favorite.Id, null, ToView(favorite), null, null, cancellationToken);
        return new ServiceResult(201, "收藏已创建。", ToView(favorite));
    }

    /// <inheritdoc />
    public async Task<ServiceResult> UpdateAsync(long homeId, long actorUserId, long favoriteId, FavoriteUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var favorite = await _db.PersonalFavorites.SingleOrDefaultAsync(x => x.Id == favoriteId && x.HomeId == homeId && x.DeletedAt == null, cancellationToken);
        if (favorite is null) return new ServiceResult(404, "请求的收藏不存在。");
        var permission = await ResolveWritePermissionAsync(homeId, actorUserId, favorite.OwnerMemberId, cancellationToken);
        if (!permission) return new ServiceResult(403, "仅归属成员本人或家庭管理员可修改该收藏。");
        var validation = Validate(favorite.Category, request.Name, request.Visibility);
        if (validation is not null) return validation;

        var before = ToView(favorite);
        favorite.Name = request.Name.Trim();
        favorite.DetailJson = string.IsNullOrWhiteSpace(request.DetailJson) ? null : request.DetailJson;
        favorite.Visibility = request.Visibility.Trim();
        favorite.UpdatedAt = DateTime.UtcNow;
        favorite.RowVersion++;
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync(homeId, actorUserId, FamilyAuditActions.FavoriteUpdate, FamilyAuditTargetTypes.PersonalFavorite, favorite.Id, before, ToView(favorite), null, null, cancellationToken);
        return new ServiceResult(200, "收藏已更新。", ToView(favorite));
    }

    /// <inheritdoc />
    public async Task<ServiceResult> DeleteAsync(long homeId, long actorUserId, long favoriteId, CancellationToken cancellationToken = default)
    {
        var favorite = await _db.PersonalFavorites.SingleOrDefaultAsync(x => x.Id == favoriteId && x.HomeId == homeId && x.DeletedAt == null, cancellationToken);
        if (favorite is null) return new ServiceResult(404, "请求的收藏不存在。");
        var permission = await ResolveWritePermissionAsync(homeId, actorUserId, favorite.OwnerMemberId, cancellationToken);
        if (!permission) return new ServiceResult(403, "仅归属成员本人或家庭管理员可删除该收藏。");

        var before = ToView(favorite);
        favorite.DeletedAt = DateTime.UtcNow;
        favorite.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync(homeId, actorUserId, FamilyAuditActions.FavoriteDelete, FamilyAuditTargetTypes.PersonalFavorite, favorite.Id, before, null, null, null, cancellationToken);
        return new ServiceResult(200, "收藏已删除。");
    }

    /// <inheritdoc />
    public async Task<ServiceResult> ImportAsync(long homeId, long actorUserId, FavoriteImportRequest request, CancellationToken cancellationToken = default)
    {
        var validation = Validate(request.Category, request.Name, request.Visibility);
        if (validation is not null) return validation;
        var owner = await ResolveOwnerMemberAsync(homeId, actorUserId, null, cancellationToken);
        if (owner is null) return new ServiceResult(422, "当前家庭尚无成员档案，请先创建家庭成员。");

        var now = DateTime.UtcNow;
        var favorite = new PersonalFavorite
        {
            HomeId = homeId,
            OwnerMemberId = owner.Id,
            Category = request.Category.Trim(),
            Name = request.Name.Trim(),
            DetailJson = string.IsNullOrWhiteSpace(request.DetailJson) ? null : request.DetailJson,
            Visibility = request.Visibility.Trim(),
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.PersonalFavorites.Add(favorite);
        await _db.SaveChangesAsync(cancellationToken);
        var source = string.IsNullOrWhiteSpace(request.Source) ? "对话" : request.Source.Trim();
        await _audit.LogAsync(homeId, actorUserId, FamilyAuditActions.FavoriteImport, FamilyAuditTargetTypes.PersonalFavorite, favorite.Id, null, ToView(favorite), $"来源：{source}", null, cancellationToken);
        return new ServiceResult(201, "收藏已导入。", ToView(favorite));
    }

    /// <summary>解析收藏归属成员：显式指定时校验同家庭；否则默认取当前用户创建的成员，其次家庭主用户，再取首位 active 成员。</summary>
    private async Task<FamilyMember?> ResolveOwnerMemberAsync(long homeId, long actorUserId, long? ownerMemberId, CancellationToken cancellationToken)
    {
        if (ownerMemberId is not null)
            return await _db.FamilyMembers.SingleOrDefaultAsync(x => x.Id == ownerMemberId && x.HomeId == homeId && x.DeletedAt == null, cancellationToken);
        return await _db.FamilyMembers
            .Where(x => x.HomeId == homeId && x.DeletedAt == null)
            .OrderByDescending(x => x.CreatedByUserId == actorUserId ? 1 : 0)
            .ThenByDescending(x => x.IsPrimary)
            .ThenByDescending(x => x.MemberStatus == FamilyMemberStatus.Active ? 1 : 0)
            .ThenBy(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>写权限：归属成员本人，或当前用户在家庭内具备 owner/admin 角色。</summary>
    private async Task<bool> ResolveWritePermissionAsync(long homeId, long actorUserId, long ownerMemberId, CancellationToken cancellationToken)
    {
        var isAdmin = await _db.TenantMembers.AnyAsync(x => x.TenantId == homeId && x.UserId == actorUserId && x.Status == "active" && (x.Role == "owner" || x.Role == "admin"), cancellationToken);
        if (isAdmin) return true;
        return await _db.FamilyMembers.AnyAsync(x => x.Id == ownerMemberId && x.HomeId == homeId && x.CreatedByUserId == actorUserId && x.DeletedAt == null, cancellationToken);
    }

    /// <summary>校验分类、名称与可见性；通过返回 null。</summary>
    private static ServiceResult? Validate(string category, string name, string visibility)
    {
        if (!PersonalFavoriteCategory.IsValid(category)) return new ServiceResult(422, "收藏分类仅支持 restaurant、travel 或 material。");
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 128) return new ServiceResult(422, "收藏名称长度需为 1-128。");
        if (!PersonalFavoriteVisibility.IsValid(visibility)) return new ServiceResult(422, "收藏可见性仅支持 private 或 family。");
        return null;
    }

    private static FavoriteView ToView(PersonalFavorite favorite) =>
        new(favorite.Id, favorite.OwnerMemberId, favorite.Category, favorite.Name, favorite.DetailJson, favorite.Visibility, favorite.CreatedAt, favorite.UpdatedAt);
}
