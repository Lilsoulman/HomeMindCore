using HomeMind.Business.IServices.Family;
using HomeMind.Business.IServices.Pet;
using HomeMind.Common.Model.Entities.Family;
using HomeMind.Common.Model.Entities.Pet;
using HomeMind.Common.Model.Entities.Steward;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.Pet;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;

namespace HomeMind.Business.Services.Pet;

/// <summary>家庭宠物管家服务，管理档案、照护日历及用品消耗预测。</summary>
public sealed class PetServices : IPetServices
{
    private readonly HomeMindDbContext _db;
    private readonly IFamilyAuditLogger _audit;

    /// <summary>构造宠物管家服务。</summary>
    public PetServices(HomeMindDbContext db, IFamilyAuditLogger audit)
    {
        _db = db;
        _audit = audit;
    }

    /// <inheritdoc />
    public async Task<ServiceResult> CreateAsync(long homeId, long actorUserId, PetCreateRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Species) || request.Name.Trim().Length > 64 || request.Species.Trim().Length > 32)
            return new ServiceResult(422, "宠物名称和种类不能为空且长度受限。");
        var now = DateTime.UtcNow;
        var pet = new PetProfile { HomeId = homeId, CreatedByUserId = actorUserId, Name = request.Name.Trim(), Species = request.Species.Trim(), Breed = Trim(request.Breed, 64), BirthDate = request.BirthDate?.Date, Notes = Trim(request.Notes, 512), CreatedAt = now, UpdatedAt = now };
        _db.PetProfiles.Add(pet);
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync(homeId, actorUserId, FamilyAuditActions.PetProfileCreate, FamilyAuditTargetTypes.PetProfile, pet.Id, null, new { pet.Id, pet.Name, pet.Species, pet.Breed, pet.BirthDate }, "创建宠物档案", null, cancellationToken);
        return new ServiceResult(201, "宠物档案已创建。", ToView(pet));
    }

    /// <inheritdoc />
    public async Task<ServiceResult> ListAsync(long homeId, CancellationToken cancellationToken = default) =>
        new(200, "查询成功。", await _db.PetProfiles.Where(item => item.HomeId == homeId && item.IsActive).OrderBy(item => item.Id).Select(item => new PetView(item.Id, item.Name, item.Species, item.Breed, item.BirthDate, item.Notes, item.IsActive, item.CreatedAt, item.UpdatedAt)).ToListAsync(cancellationToken));

    /// <inheritdoc />
    public async Task<ServiceResult> AddCareEventAsync(long homeId, long actorUserId, long petId, PetCareEventCreateRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null || !PetCareTypes.All.Contains(request.CareType) || string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length > 128)
            return new ServiceResult(422, "careType 必须为 vaccine 或 deworming，且标题不能为空。");
        if (!await _db.PetProfiles.AnyAsync(item => item.Id == petId && item.HomeId == homeId && item.IsActive, cancellationToken)) return new ServiceResult(404, "宠物档案不存在。");
        var item = new PetCareEvent { PetId = petId, HomeId = homeId, CreatedByUserId = actorUserId, CareType = request.CareType, Title = request.Title.Trim(), DueDate = request.DueDate.Date, Notes = Trim(request.Notes, 512), CreatedAt = DateTime.UtcNow };
        _db.PetCareEvents.Add(item);
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync(homeId, actorUserId, FamilyAuditActions.PetCareEventCreate, FamilyAuditTargetTypes.PetCareEvent, item.Id, null, new { item.Id, item.PetId, item.CareType, item.Title, item.DueDate }, "创建宠物照护日历记录", null, cancellationToken);
        return new ServiceResult(201, "照护日历已创建。", ToView(item));
    }

    /// <inheritdoc />
    public async Task<ServiceResult> ListCareEventsAsync(long homeId, long petId, CancellationToken cancellationToken = default) =>
        !await _db.PetProfiles.AnyAsync(item => item.Id == petId && item.HomeId == homeId && item.IsActive, cancellationToken)
            ? new ServiceResult(404, "宠物档案不存在.")
            : new ServiceResult(200, "查询成功。", await _db.PetCareEvents.Where(item => item.HomeId == homeId && item.PetId == petId).OrderBy(item => item.DueDate).Select(item => new PetCareEventView(item.Id, item.PetId, item.CareType, item.Title, item.DueDate, item.CompletedAt, item.Notes)).ToListAsync(cancellationToken));

    /// <inheritdoc />
    public async Task<ServiceResult> UpsertSupplyAsync(long homeId, long actorUserId, long petId, PetSupplyUpsertRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.ItemName) || request.ItemName.Trim().Length > 128 || request.Quantity < 0 || request.DailyUsage <= 0 || string.IsNullOrWhiteSpace(request.Unit) || request.Unit.Trim().Length > 16 || !PetSupplySourceTypes.All.Contains(request.SourceType))
            return new ServiceResult(422, "用品名称、库存和日均消耗不符合约束。");
        if (!await _db.PetProfiles.AnyAsync(item => item.Id == petId && item.HomeId == homeId && item.IsActive, cancellationToken)) return new ServiceResult(404, "宠物档案不存在。");
        var supply = await _db.PetSupplyRecords.SingleOrDefaultAsync(item => item.PetId == petId && item.ItemName == request.ItemName.Trim(), cancellationToken);
        var now = DateTime.UtcNow;
        if (supply is null)
        {
            supply = new PetSupplyRecord { PetId = petId, HomeId = homeId, CreatedByUserId = actorUserId, ItemName = request.ItemName.Trim(), Quantity = request.Quantity, DailyUsage = request.DailyUsage, Unit = request.Unit.Trim(), SourceType = request.SourceType, MeasuredAt = (request.MeasuredAt ?? now).Date, UpdatedAt = now };
            _db.PetSupplyRecords.Add(supply);
        }
        else
        {
            supply.Quantity = request.Quantity; supply.DailyUsage = request.DailyUsage; supply.Unit = request.Unit.Trim(); supply.SourceType = request.SourceType; supply.MeasuredAt = (request.MeasuredAt ?? now).Date; supply.UpdatedAt = now;
        }
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync(homeId, actorUserId, FamilyAuditActions.PetSupplyUpsert, FamilyAuditTargetTypes.PetSupply, supply.Id, null, new { supply.Id, supply.PetId, supply.ItemName, supply.Quantity, supply.DailyUsage, supply.Unit }, "更新宠物用品消耗记录", null, cancellationToken);
        return new ServiceResult(200, "用品消耗记录已保存。", await SupplyViewAsync(supply, homeId, cancellationToken));
    }

    /// <inheritdoc />
    public async Task<ServiceResult> ListSuppliesAsync(long homeId, long petId, CancellationToken cancellationToken = default)
    {
        if (!await _db.PetProfiles.AnyAsync(item => item.Id == petId && item.HomeId == homeId && item.IsActive, cancellationToken)) return new ServiceResult(404, "宠物档案不存在。");
        var result = new List<PetSupplyView>();
        foreach (var item in await _db.PetSupplyRecords.Where(item => item.HomeId == homeId && item.PetId == petId).OrderBy(item => item.Id).ToListAsync(cancellationToken)) result.Add(await SupplyViewAsync(item, homeId, cancellationToken));
        return new ServiceResult(200, "查询成功。", result);
    }

    /// <inheritdoc />
    public async Task<ServiceResult> ListAlertsAsync(long homeId, DateTime? asOf = null, CancellationToken cancellationToken = default)
    {
        var today = (asOf ?? DateTime.UtcNow).Date;
        var care = await _db.PetCareEvents.Where(item => item.HomeId == homeId && item.CompletedAt == null && item.DueDate >= today && item.DueDate <= today.AddDays(7)).OrderBy(item => item.DueDate).ToListAsync(cancellationToken);
        var supplies = await _db.PetSupplyRecords.Where(item => item.HomeId == homeId && item.DailyUsage > 0 && item.Quantity / item.DailyUsage <= 7).OrderBy(item => item.Id).ToListAsync(cancellationToken);
        var alerts = new List<object>();
        foreach (var item in care) alerts.Add(new { type = item.CareType, petId = item.PetId, title = item.Title, dueDate = item.DueDate, daysRemaining = (int)(item.DueDate - today).TotalDays, confirmationId = (long?)await EnsureCareConfirmationAsync(homeId, item, cancellationToken) });
        foreach (var item in supplies) alerts.Add(new { type = "supply_low", petId = item.PetId, title = $"{item.ItemName} 将在 7 天内耗尽", daysRemaining = (decimal?)Math.Floor(item.Quantity / item.DailyUsage), confirmationId = (long?)await EnsureSupplyConfirmationAsync(homeId, item, cancellationToken) });
        return new ServiceResult(200, "查询成功。", alerts);
    }

    private async Task<PetSupplyView> SupplyViewAsync(PetSupplyRecord item, long homeId, CancellationToken cancellationToken)
    {
        var days = item.DailyUsage > 0 ? (decimal?)(item.Quantity / item.DailyUsage) : null;
        var confirmationId = days is <= 7 ? (long?)await EnsureSupplyConfirmationAsync(homeId, item, cancellationToken) : null;
        return new PetSupplyView(item.Id, item.PetId, item.ItemName, item.Quantity, item.DailyUsage, item.Unit, item.SourceType, item.MeasuredAt, days, confirmationId);
    }

    private async Task<long> EnsureSupplyConfirmationAsync(long homeId, PetSupplyRecord item, CancellationToken cancellationToken)
    {
        var title = $"宠物用品提醒：{item.ItemName} 将在 7 天内耗尽";
        var existing = await _db.ConfirmationItems.SingleOrDefaultAsync(item => item.HomeId == homeId && item.Title == title && item.Status == ConfirmationItemStatus.Pending, cancellationToken);
        if (existing is not null) return existing.Id;
        var confirmation = new ConfirmationItem { HomeId = homeId, RiskLevel = ConfirmationRiskLevel.L1, Title = title, Description = $"按当前日均消耗，预计剩余 {Math.Floor(item.Quantity / item.DailyUsage)} 天。", ImpactSummary = "仅提醒补货，不会自动下单或访问第三方服务。", SuggestedAction = "确认后由用户自行购买并更新库存。", Status = ConfirmationItemStatus.Pending, ExpiresAt = DateTime.UtcNow.AddDays(7), CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        _db.ConfirmationItems.Add(confirmation);
        await _db.SaveChangesAsync(cancellationToken);
        return confirmation.Id;
    }

    private async Task<long> EnsureCareConfirmationAsync(long homeId, PetCareEvent item, CancellationToken cancellationToken)
    {
        var title = $"宠物照护提醒：{item.Title} 将于 {item.DueDate:yyyy-MM-dd} 到期";
        var existing = await _db.ConfirmationItems.SingleOrDefaultAsync(item => item.HomeId == homeId && item.Title == title && item.Status == ConfirmationItemStatus.Pending, cancellationToken);
        if (existing is not null) return existing.Id;
        var confirmation = new ConfirmationItem { HomeId = homeId, RiskLevel = ConfirmationRiskLevel.L1, Title = title, Description = "请确认是否加入家庭日历或待办清单。", ImpactSummary = "仅生成家庭内提醒，不会代替医疗或兽医建议。", SuggestedAction = "确认后由用户安排疫苗或驱虫。", Status = ConfirmationItemStatus.Pending, ExpiresAt = item.DueDate.AddDays(1), CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        _db.ConfirmationItems.Add(confirmation);
        await _db.SaveChangesAsync(cancellationToken);
        return confirmation.Id;
    }

    private static string? Trim(string? value, int max) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().Length <= max ? value.Trim() : value.Trim()[..max];
    private static PetView ToView(PetProfile item) => new(item.Id, item.Name, item.Species, item.Breed, item.BirthDate, item.Notes, item.IsActive, item.CreatedAt, item.UpdatedAt);
    private static PetCareEventView ToView(PetCareEvent item) => new(item.Id, item.PetId, item.CareType, item.Title, item.DueDate, item.CompletedAt, item.Notes);
}
