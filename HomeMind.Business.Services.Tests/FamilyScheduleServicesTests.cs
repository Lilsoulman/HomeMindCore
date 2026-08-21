using HomeMind.Business.IServices.Family;
using HomeMind.Business.Services.Family;
using HomeMind.Common.Model.Entities;
using HomeMind.Common.Model.Entities.Family;
using HomeMind.Common.Model.Entities.Finance;
using HomeMind.Common.Model.ViewModel.Data.Family;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HomeMind.Business.Services.Tests;

/// <summary>家庭日程协同管家的定向测试。</summary>
public sealed class FamilyScheduleServicesTests
{
    /// <summary>只聚合本家庭内活跃成员的日程。</summary>
    [Fact]
    public async Task Events_Aggregates_Only_Active_Home_Members()
    {
        await using var db = NewDb(); SeedMembers(db);
        db.CalendarEvents.AddRange(Event(1, 1, "爸爸会议", 9, 60), Event(1, 2, "妈妈约诊", 10, 60), Event(1, 3, "已停用", 11, 60), Event(2, 4, "其他家庭", 12, 60));
        await db.SaveChangesAsync();

        var result = await NewService(db).ListEventsAsync(1, Today(0, 8), Today(0, 14));

        var events = Assert.IsType<List<FamilyScheduleEventView>>(result.Data);
        Assert.Equal(new[] { "爸爸会议", "妈妈约诊" }, events.Select(item => item.Title));
    }

    /// <summary>相交事件会返回冲突，并且共同空档排除全部成员占用。</summary>
    [Fact]
    public async Task Conflicts_And_Availability_Use_All_Member_Events()
    {
        await using var db = NewDb(); SeedMembers(db);
        db.CalendarEvents.AddRange(Event(1, 1, "会议", 9, 90), Event(1, 2, "体检", 10, 60));
        await db.SaveChangesAsync(); var service = NewService(db);

        var conflicts = Assert.IsType<List<FamilyScheduleConflictView>>((await service.ListConflictsAsync(1, Today(0, 8), Today(0, 13))).Data);
        var availability = Assert.IsType<List<FamilyScheduleAvailabilityView>>((await service.ListAvailabilityAsync(1, Today(0, 8), Today(0, 13), 60)).Data);

        Assert.Single(conflicts);
        Assert.Contains(availability, item => item.StartAt == Today(0, 8) && item.EndAt == Today(0, 9));
        Assert.Contains(availability, item => item.StartAt == Today(0, 11) && item.EndAt == Today(0, 13));
    }

    /// <summary>缴费和证件到期事项会幂等投影为 L1 确认卡。</summary>
    [Fact]
    public async Task Reminders_Project_Billing_And_Document_Confirmations_Idempotently()
    {
        await using var db = NewDb(); SeedMembers(db);
        db.BillingAccounts.Add(new BillingAccount { HomeId = 1, CreatedByUserId = 1, BillingType = BillingTypes.Electricity, Provider = "供电公司", Label = "本月电费", NextDueDate = Today(2), CreatedAt = Today(0), UpdatedAt = Today(0) });
        await db.SaveChangesAsync(); var service = NewService(db);
        var create = await service.CreateDocumentDeadlineAsync(1, 1, new FamilyDocumentDeadlineCreateRequest(FamilyDocumentTypes.Passport, "妈妈护照", 2, Today(10)));
        Assert.Equal(201, create.StatusCode);

        var first = Assert.IsType<List<FamilyScheduleReminderView>>((await service.ListRemindersAsync(1, Today(0))).Data);
        var second = Assert.IsType<List<FamilyScheduleReminderView>>((await service.ListRemindersAsync(1, Today(0))).Data);

        Assert.Equal(2, first.Count); Assert.Equal(first.Select(item => item.ConfirmationId), second.Select(item => item.ConfirmationId));
        Assert.Equal(2, db.ConfirmationItems.Count());
    }

    /// <summary>明日预览同时返回明日日程和提醒。</summary>
    [Fact]
    public async Task TomorrowPreview_Returns_Events_And_Reminders()
    {
        await using var db = NewDb(); SeedMembers(db);
        db.CalendarEvents.Add(Event(1, 1, "明日家长会", 9, 60, Today(1)));
        db.BillingAccounts.Add(new BillingAccount { HomeId = 1, CreatedByUserId = 1, BillingType = BillingTypes.Water, Provider = "水务", Label = "水费", NextDueDate = Today(2), CreatedAt = Today(0), UpdatedAt = Today(0) });
        await db.SaveChangesAsync();

        var preview = Assert.IsType<FamilyTomorrowPreviewView>((await NewService(db).GetTomorrowPreviewAsync(1, Today(0))).Data);

        Assert.Single(preview.Events); Assert.Single(preview.Reminders);
    }

    private static FamilyScheduleServices NewService(HomeMindDbContext db) => new(db, new FakeAuditLogger());
    private static HomeMindDbContext NewDb() => new(new DbContextOptionsBuilder<HomeMindDbContext>().UseInMemoryDatabase($"schedule-{Guid.NewGuid()}").Options);
    private static DateTime Today(int days, int hour = 0) => DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(days).AddHours(hour), DateTimeKind.Utc);
    private static CalendarEvent Event(long tenantId, long userId, string title, int hour, int minutes, DateTime? date = null) => new() { TenantId = tenantId, UserId = userId, Title = title, StartAt = (date ?? Today(0)).Date.AddHours(hour), EndAt = (date ?? Today(0)).Date.AddHours(hour).AddMinutes(minutes) };
    private static void SeedMembers(HomeMindDbContext db)
    {
        db.Users.AddRange(new User { Id = 1, DisplayName = "爸爸" }, new User { Id = 2, DisplayName = "妈妈" }, new User { Id = 3, DisplayName = "停用成员" }, new User { Id = 4, DisplayName = "其他家庭" });
        db.TenantMembers.AddRange(new TenantMember { TenantId = 1, UserId = 1, Status = "active" }, new TenantMember { TenantId = 1, UserId = 2, Status = "active" }, new TenantMember { TenantId = 1, UserId = 3, Status = "suspended" }, new TenantMember { TenantId = 2, UserId = 4, Status = "active" });
    }

    private sealed class FakeAuditLogger : IFamilyAuditLogger
    {
        public Task<bool> LogAsync(long homeId, long? actorUserId, string action, string targetType, long? targetId, object? before, object? after, string? reason, long? relatedRunId, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }
}
