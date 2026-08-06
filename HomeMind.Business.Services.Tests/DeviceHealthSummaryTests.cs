using HomeMind.Business.Services.SmartHome;
using HomeMind.Common.Model.Entities.SmartHome;
using HomeMind.Common.Model.ViewModel.Data.SmartHome;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace HomeMind.Business.Services.Tests;

public class DeviceHealthSummaryTests
{
    [Fact]
    public async Task Aggregates_Health_Buckets_By_Tenant_And_Space()
    {
        await using var db = NewContext();
        var now = DateTime.UtcNow;
        db.SmartHomeDevices.AddRange(
            Seed(101, 1, "healthy", now),
            Seed(101, 1, "healthy", now),
            Seed(101, 1, "degraded", now),
            Seed(101, 2, "offline", now),
            Seed(101, 2, "low_battery", now),
            Seed(202, 1, "healthy", now));
        await db.SaveChangesAsync();

        var services = new SmartHomeReadServices(db, MockConfiguration(false));
        var result = await services.GetDeviceHealthAsync(101, spaceId: 1);

        Assert.True(result.Succeeded);
        var view = Assert.IsType<DeviceHealthSummaryView>(result.Data);
        Assert.Equal(3, view.Total);
        Assert.Equal(2, view.Healthy);
        Assert.Equal(1, view.Degraded);
        Assert.Equal(0, view.Offline);
        Assert.Equal(0, view.LowBattery);
        Assert.Equal("healthy", view.DominantStatus);
    }

    private static HomeMindDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<HomeMindDbContext>()
            .UseInMemoryDatabase($"hm-b10-{Guid.NewGuid()}")
            .Options;
        return new HomeMindDbContext(options);
    }

    private static SmartHomeDevice Seed(long tenantId, long spaceId, string health, DateTime now) => new()
    {
        TenantId = tenantId,
        SpaceId = spaceId,
        Name = $"device-{Guid.NewGuid():N}",
        DeviceType = "light",
        OnlineStatus = health == "offline" ? "offline" : "online",
        HealthStatus = health,
        CreatedAt = now,
        UpdatedAt = now
    };

    private static IConfiguration MockConfiguration(bool mockEnabled) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["SmartHome:MockEnabled"] = mockEnabled.ToString()
        }).Build();
}
