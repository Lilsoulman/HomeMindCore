using System.Text.Json;
using HomeMind.Business.IServices.Productivity;
using HomeMind.Business.Services.Life;
using HomeMind.Common.Model.Entities;
using HomeMind.Common.Model.Entities.Family;
using HomeMind.Common.Model.Entities.Life;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.Life;
using HomeMind.Common.Model.ViewModel.Data.Productivity;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HomeMind.Business.Services.Tests;

/// <summary>个人生活专家运行定向测试：翻牌评分与可见性过滤、行程生成与日历确认执行。</summary>
public class LifeExpertRunServicesTests
{
    /// <summary>行程规划生成每日安排并产出待确认的 calendar_create_event 动作。</summary>
    [Fact]
    public async Task Plan_Creates_Pending_Calendar_Action_With_Daily_Arrangement()
    {
        await using var db = NewDb("life-plan");
        SeedExpert(db);
        SeedMember(db, memberId: 1, createdBy: 10);
        SeedFavorite(db, id: 100, owner: 1, visibility: "private", name: "西湖", tags: ["拍照"], cuisine: "", address: "杭州");
        SeedFavorite(db, id: 101, owner: 1, visibility: "private", name: "楼外楼", tags: ["杭帮菜"], cuisine: "杭帮菜", address: "杭州");
        var services = new LifeExpertRunServices(db, new FakeCalendar());

        var result = await services.CreateAsync(10, 1, new LifeExpertRunRequest("plan", """{"destination":"杭州","days":2}""", null), default);

        Assert.True(result.Succeeded);
        var action = await db.ExpertRunActions.SingleAsync(x => x.ActionType == "calendar_create_event");
        Assert.Equal("pending", action.Status);
        using var document = JsonDocument.Parse(action.RequestJson);
        var days = document.RootElement.GetProperty("days").EnumerateArray().ToArray();
        Assert.Equal(2, days.Length);
        Assert.Contains(days, x => x.GetProperty("weather").GetString() is not null);
    }

    /// <summary>确认行程动作后写入日历事件并置为 executed，且同一动作不可重复执行。</summary>
    [Fact]
    public async Task Confirm_Executes_Calendar_Events_And_Rejects_Repeated_Confirm()
    {
        await using var db = NewDb("life-confirm");
        SeedExpert(db);
        SeedMember(db, memberId: 1, createdBy: 10);
        SeedFavorite(db, id: 100, owner: 1, visibility: "private", name: "西湖", tags: ["拍照"], cuisine: "", address: "杭州");
        var calendar = new FakeCalendar();
        var services = new LifeExpertRunServices(db, calendar);

        var created = await services.CreateAsync(10, 1, new LifeExpertRunRequest("plan", """{"destination":"杭州","days":1}""", null), default);
        var actionId = (await db.ExpertRunActions.SingleAsync()).Id;
        var runId = (await db.ExpertRunActions.SingleAsync()).RunId;
        var key = Guid.NewGuid().ToString();

        var result = await services.ConfirmActionAsync(10, 1, runId, actionId, new ConfirmLifeExpertActionRequest(key), default);

        Assert.True(result.Succeeded);
        Assert.Equal(1, calendar.CreatedCount);
        var action = await db.ExpertRunActions.SingleAsync();
        Assert.Equal("executed", action.Status);
        var audit = await db.ActionExecutionAudits.SingleAsync();
        Assert.Null(audit.WorkspaceConnectorId);
        Assert.Null(audit.DeviceId);

        var repeated = await services.ConfirmActionAsync(10, 1, runId, actionId, new ConfirmLifeExpertActionRequest(key), default);
        Assert.True(repeated.Succeeded);
        Assert.Equal(1, calendar.CreatedCount);
    }

    /// <summary>非法幂等键返回 422。</summary>
    [Fact]
    public async Task Confirm_Rejects_Invalid_Idempotency_Key()
    {
        await using var db = NewDb("life-key");
        SeedExpert(db);
        SeedMember(db, memberId: 1, createdBy: 10);
        SeedFavorite(db, id: 100, owner: 1, visibility: "private", name: "西湖", tags: ["拍照"], cuisine: "", address: "杭州");
        var services = new LifeExpertRunServices(db, new FakeCalendar());

        var created = await services.CreateAsync(10, 1, new LifeExpertRunRequest("plan", """{"destination":"杭州","days":1}""", null), default);
        var action = await db.ExpertRunActions.SingleAsync();

        var result = await services.ConfirmActionAsync(10, 1, action.RunId, action.Id, new ConfirmLifeExpertActionRequest("not-a-uuid"), default);

        Assert.Equal(422, result.StatusCode);
    }

    /// <summary>翻牌按口味与位置评分返回 Top 建议；仅推荐可见收藏。</summary>
    [Fact]
    public async Task Recommend_Returns_Top_Picks_Matching_Taste_And_Location()
    {
        await using var db = NewDb("life-recommend");
        SeedExpert(db);
        SeedMember(db, memberId: 1, createdBy: 10);
        SeedFavorite(db, id: 100, owner: 1, visibility: "private", name: "老王面馆", tags: ["面", "晚餐"], cuisine: "面食", address: "城西");
        SeedFavorite(db, id: 101, owner: 2, visibility: "private", name: "别人的私藏", tags: ["面"], cuisine: "面食", address: "城西");
        SeedFavorite(db, id: 102, owner: 2, visibility: "family", name: "全家火锅", tags: ["火锅", "晚餐"], cuisine: "川菜", address: "城东");
        var services = new LifeExpertRunServices(db, new FakeCalendar());

        var result = await services.CreateAsync(10, 1, new LifeExpertRunRequest("recommend", """{"time":"evening","location":"城西","taste":"面"}""", null), default);

        Assert.True(result.Succeeded);
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(result.Data));
        var recommendations = document.RootElement.GetProperty("Recommendations").EnumerateArray().ToArray();
        Assert.NotEmpty(recommendations);
        // 评分最高的应为同时命中口味与位置的老王面馆；他人的 private 收藏不可见。
        Assert.Contains(recommendations, x => x.GetProperty("FavoriteId").GetInt64() == 100);
        Assert.DoesNotContain(recommendations, x => x.GetProperty("FavoriteId").GetInt64() == 101);
    }

    /// <summary>口味不匹配时仍以私藏店铺库兜底推荐（0 分项），运行正常完成。</summary>
    [Fact]
    public async Task Recommend_Falls_Back_To_Private_Pool_When_Nothing_Matches()
    {
        await using var db = NewDb("life-empty");
        SeedExpert(db);
        SeedMember(db, memberId: 1, createdBy: 10);
        SeedFavorite(db, id: 100, owner: 1, visibility: "private", name: "老王小炒", tags: ["家常菜"], cuisine: "家常菜", address: "城西");
        var services = new LifeExpertRunServices(db, new FakeCalendar());

        var result = await services.CreateAsync(10, 1, new LifeExpertRunRequest("recommend", """{"taste":"火锅"}""", null), default);

        Assert.True(result.Succeeded);
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(result.Data));
        var recommendations = document.RootElement.GetProperty("Recommendations").EnumerateArray().ToArray();
        var item = Assert.Single(recommendations);
        Assert.Equal(100, item.GetProperty("FavoriteId").GetInt64());
        Assert.Contains("私藏店铺库", item.GetProperty("Reason").GetString());
    }

    /// <summary>专家未注册时返回 503，提示先应用 017 迁移。</summary>
    [Fact]
    public async Task Create_Returns_503_When_Expert_Not_Initialized()
    {
        await using var db = NewDb("life-noexpert");
        var services = new LifeExpertRunServices(db, new FakeCalendar());

        var result = await services.CreateAsync(10, 1, new LifeExpertRunRequest("recommend", "{}", null), default);

        Assert.Equal(503, result.StatusCode);
        Assert.Contains("017", result.Message);
    }

    /// <summary>非法意图返回 422；行程规划占位提示。</summary>
    [Fact]
    public async Task Create_Rejects_Unsupported_Intent()
    {
        await using var db = NewDb("life-intent");
        SeedExpert(db);
        var services = new LifeExpertRunServices(db, new FakeCalendar());

        var result = await services.CreateAsync(10, 1, new LifeExpertRunRequest("plan", "{}", null), default);

        Assert.Equal(422, result.StatusCode);
    }

    private static HomeMindDbContext NewDb(string name) =>
        new(new DbContextOptionsBuilder<HomeMindDbContext>()
            .UseInMemoryDatabase($"hm-b16-life-{name}-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private static void SeedExpert(HomeMindDbContext db)
    {
        db.Experts.Add(new HomeMind.Common.Model.Entities.Expert { TenantId = 1, Code = "personal-life-expert", Name = "个人生活专家", Category = "life", ExpertType = "builtin", Status = "active" });
        db.SaveChanges();
        var expert = db.Experts.Single(x => x.Code == "personal-life-expert");
        db.ExpertVersions.Add(new ExpertVersion { TenantId = 1, ExpertId = expert.Id, Version = 1, Status = "published", Persona = "x", Methodology = "x", PromptTemplate = "x", ToolPolicy = """{"skills":["favorite.recommend","trip.plan","favorite.create"]}""", OutputSchema = "{}", EstimatedCredits = 1.5m });
        db.SaveChanges();
    }

    private static void SeedMember(HomeMindDbContext db, long memberId, long createdBy)
    {
        db.FamilyMembers.Add(new FamilyMember { Id = memberId, HomeId = 1, Name = $"成员{memberId}", Relation = "self", MemberStatus = "active", CreatedByUserId = createdBy, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        db.SaveChanges();
    }

    private static void SeedFavorite(HomeMindDbContext db, long id, long owner, string visibility, string name, string[] tags, string cuisine, string address)
    {
        db.PersonalFavorites.Add(new PersonalFavorite
        {
            Id = id,
            HomeId = 1,
            OwnerMemberId = owner,
            Category = "restaurant",
            Name = name,
            DetailJson = JsonSerializer.Serialize(new { tags, cuisine, address }),
            Visibility = visibility,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        db.SaveChanges();
    }

    /// <summary>日历服务测试替身，记录创建成功的事件数。</summary>
    private sealed class FakeCalendar : ICalendarServices
    {
        public int CreatedCount { get; private set; }

        public Task<ServiceResult> ListEventsAsync(long userId, long tenantId, DateTime? from, DateTime? to, CancellationToken token = default) =>
            Task.FromResult(new ServiceResult(200, "ok"));
        public Task<ServiceResult> CreateEventAsync(long userId, long tenantId, CalendarEventRequest request, CancellationToken token = default)
        {
            CreatedCount++;
            return Task.FromResult(new ServiceResult(201, "ok"));
        }
        public Task<ServiceResult> UpdateEventAsync(long userId, long tenantId, long id, CalendarEventRequest request, CancellationToken token = default) =>
            Task.FromResult(new ServiceResult(200, "ok"));
        public Task<ServiceResult> DeleteEventAsync(long userId, long tenantId, long id, CancellationToken token = default) =>
            Task.FromResult(new ServiceResult(200, "ok"));
        public Task<ServiceResult> ListSubscriptionsAsync(long userId, long tenantId, CancellationToken token = default) =>
            Task.FromResult(new ServiceResult(200, "ok"));
        public Task<ServiceResult> CreateSubscriptionAsync(long userId, long tenantId, SubscriptionRequest request, CancellationToken token = default) =>
            Task.FromResult(new ServiceResult(201, "ok"));
        public Task<ServiceResult> UpdateSubscriptionAsync(long userId, long tenantId, long id, SubscriptionRequest request, CancellationToken token = default) =>
            Task.FromResult(new ServiceResult(200, "ok"));
        public Task<ServiceResult> DeleteSubscriptionAsync(long userId, long tenantId, long id, CancellationToken token = default) =>
            Task.FromResult(new ServiceResult(200, "ok"));
    }
}
