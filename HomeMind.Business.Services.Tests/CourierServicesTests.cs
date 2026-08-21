using HomeMind.Business.IServices.Connector;
using HomeMind.Business.Services.Courier;
using HomeMind.Common.Model.Entities.Courier;
using HomeMind.Common.Model.Entities.Steward;
using HomeMind.Common.Model.ViewModel.Data.Courier;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HomeMind.Business.Services.Tests;

/// <summary>快递管家状态、异常与个人隔离定向测试。</summary>
public sealed class CourierServicesTests
{
    /// <summary>刷新状态写入事件并生成滞留建议卡。</summary>
    [Fact]
    public async Task Refresh_Writes_Events_And_Stagnant_Confirmation()
    {
        await using var db = NewDb();
        var mcp = new FakeMcp([new("in_transit", "运输中", "杭州", DateTime.UtcNow.AddHours(-72))]);
        var service = new CourierServices(db, mcp);
        var created = await service.CreateAsync(1, 10, new CourierShipmentCreateRequest("SF123456789"));
        var shipment = Assert.IsType<CourierShipmentView>(created.Data);
        var refreshed = await service.RefreshAsync(1, 10, shipment.Id);
        var view = Assert.IsType<CourierRefreshView>(refreshed.Data);
        Assert.Contains(view.Anomalies, item => item.Type == CourierAnomalyTypes.Stagnant);
        Assert.Single(db.CourierShipmentEvents);
        Assert.Single(db.ConfirmationItems);
    }

    /// <summary>派送中的生鲜包裹分别识别无人签收和时效风险。</summary>
    [Fact]
    public async Task Refresh_Detects_Unattended_And_Fresh_Food_Risk()
    {
        await using var db = NewDb();
        var service = new CourierServices(db, new FakeMcp([new("out_for_delivery", "派送中", "余姚", DateTime.UtcNow)]));
        var created = await service.CreateAsync(1, 10, new CourierShipmentCreateRequest("YT123456789", IsFreshFood: true, ExpectedDeliveryAt: DateTime.UtcNow.AddHours(-1)));
        var id = Assert.IsType<CourierShipmentView>(created.Data).Id;
        var result = Assert.IsType<CourierRefreshView>((await service.RefreshAsync(1, 10, id)).Data);
        Assert.Contains(result.Anomalies, item => item.Type == CourierAnomalyTypes.Unattended);
        Assert.Contains(result.Anomalies, item => item.Type == CourierAnomalyTypes.FreshFoodRisk);
    }

    /// <summary>不同用户在同一家庭中只能看见自己的运单。</summary>
    [Fact]
    public async Task List_Isolated_By_Owner()
    {
        await using var db = NewDb(); var service = new CourierServices(db, new FakeMcp([]));
        await service.CreateAsync(1, 10, new CourierShipmentCreateRequest("JD123456789"));
        await service.CreateAsync(1, 11, new CourierShipmentCreateRequest("JD987654321"));
        var list = Assert.IsType<List<CourierShipmentView>>((await service.ListAsync(1, 10)).Data);
        Assert.Single(list); Assert.EndsWith("6789", list[0].TrackingNumberMasked);
    }

    private static HomeMindDbContext NewDb() => new(new DbContextOptionsBuilder<HomeMindDbContext>().UseInMemoryDatabase($"courier-{Guid.NewGuid()}").Options);
    private sealed class FakeMcp(IReadOnlyList<Kuaidi100TrackingEvent> events) : IKuaidi100McpClient
    { public Task<Kuaidi100TrackingResult> TrackAsync(string trackingNumber, string? carrier, CancellationToken cancellationToken = default) => Task.FromResult(new Kuaidi100TrackingResult(events)); }
}
