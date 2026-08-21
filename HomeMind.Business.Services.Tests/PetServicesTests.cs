using HomeMind.Business.Services.Family;
using HomeMind.Business.Services.Pet;
using HomeMind.Common.Model.Entities.Pet;
using HomeMind.Common.Model.ViewModel.Data.Pet;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HomeMind.Business.Services.Tests;

/// <summary>宠物档案、照护日历和用品预测定向测试。</summary>
public sealed class PetServicesTests
{
    /// <summary>创建档案并写入照护日历。</summary>
    [Fact]
    public async Task Create_And_AddCareEvent()
    {
        await using var db = NewDb();
        var service = NewService(db);
        var pet = Assert.IsType<PetView>((await service.CreateAsync(1, 10, new PetCreateRequest("豆豆", "cat"))).Data);
        var care = await service.AddCareEventAsync(1, 10, pet.Id, new PetCareEventCreateRequest(PetCareTypes.Vaccine, "年度疫苗", DateTime.UtcNow.Date.AddDays(5)));
        Assert.Equal(201, care.StatusCode);
        Assert.Single(db.PetCareEvents);
    }

    /// <summary>用品剩余七天时幂等生成 L1 确认卡。</summary>
    [Fact]
    public async Task Supply_LowStock_Projects_Confirmation_Idempotently()
    {
        await using var db = NewDb(); var service = NewService(db);
        var pet = Assert.IsType<PetView>((await service.CreateAsync(1, 10, new PetCreateRequest("豆豆", "cat"))).Data);
        var first = Assert.IsType<PetSupplyView>((await service.UpsertSupplyAsync(1, 10, pet.Id, new PetSupplyUpsertRequest("猫粮", 7, 1))).Data);
        var second = Assert.IsType<PetSupplyView>((await service.ListSuppliesAsync(1, pet.Id)).Data is List<PetSupplyView> list ? list[0] : null);
        Assert.NotNull(first.ConfirmationId);
        Assert.Equal(first.ConfirmationId, second.ConfirmationId);
        Assert.Single(db.ConfirmationItems);
    }

    /// <summary>不同家庭不能读取宠物数据。</summary>
    [Fact]
    public async Task Pet_Data_Isolated_By_Home()
    {
        await using var db = NewDb(); var service = NewService(db);
        await service.CreateAsync(1, 10, new PetCreateRequest("豆豆", "cat"));
        Assert.Empty(Assert.IsType<List<PetView>>((await service.ListAsync(2)).Data));
    }

    /// <summary>照护到期窗口返回提醒。</summary>
    [Fact]
    public async Task Alerts_Return_Care_Within_Seven_Days()
    {
        await using var db = NewDb(); var service = NewService(db);
        var pet = Assert.IsType<PetView>((await service.CreateAsync(1, 10, new PetCreateRequest("豆豆", "cat"))).Data);
        await service.AddCareEventAsync(1, 10, pet.Id, new PetCareEventCreateRequest(PetCareTypes.Deworming, "体内驱虫", DateTime.UtcNow.Date.AddDays(3)));
        var alerts = Assert.IsType<List<object>>((await service.ListAlertsAsync(1, DateTime.UtcNow.Date)).Data);
        Assert.Single(alerts);
    }

    private static PetServices NewService(HomeMindDbContext db) => new(db, new FamilyAuditLogger(db, NullLogger<FamilyAuditLogger>.Instance));
    private static HomeMindDbContext NewDb() => new(new DbContextOptionsBuilder<HomeMindDbContext>().UseInMemoryDatabase($"pet-{Guid.NewGuid()}").Options);
}
