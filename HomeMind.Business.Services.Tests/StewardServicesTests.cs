using HomeMind.Business.IServices.Family;
using HomeMind.Business.Services.Steward;
using HomeMind.Common.Model.Entities.Family;
using HomeMind.Common.Model.Entities.Steward;
using HomeMind.Common.Model.ViewModel.Data.Steward;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HomeMind.Business.Services.Tests;

/// <summary>管家动态与确认中心服务定向测试：游标分页、撤销校验、确认/拒绝状态机、L1 批量确认原子性与幂等重放。</summary>
public class StewardServicesTests
{
    /// <summary>活动列表游标分页：翻页取完全部数据，末页游标为空。</summary>
    [Fact]
    public async Task ListActivities_Returns_Cursor_Paged_Items()
    {
        await using var db = NewDb("activities-paged");
        var services = new StewardServices(db, new FakeAuditLogger());
        for (var i = 0; i < 25; i++) SeedActivity(db, 1, i);
        await db.SaveChangesAsync();

        var page1 = await services.ListActivitiesAsync(1, 10, null);
        Assert.True(page1.Succeeded);
        var d1 = ParsePage(page1.Data!);
        Assert.Equal(10, d1.Items);
        Assert.NotNull(d1.Cursor);

        var page2 = await services.ListActivitiesAsync(1, 10, d1.Cursor);
        Assert.True(page2.Succeeded);
        var d2 = ParsePage(page2.Data!);
        Assert.Equal(10, d2.Items);
        Assert.NotNull(d2.Cursor);

        var page3 = await services.ListActivitiesAsync(1, 10, d2.Cursor);
        Assert.True(page3.Succeeded);
        var d3 = ParsePage(page3.Data!);
        Assert.Equal(5, d3.Items);
        Assert.Null(d3.Cursor);
    }

    /// <summary>将分页匿名响应解析为（条数, 游标），避免跨程序集访问匿名类型成员。</summary>
    private static (int Items, string? Cursor) ParsePage(object data)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(data));
        return (doc.RootElement.GetProperty("Items").GetArrayLength(), doc.RootElement.GetProperty("Cursor").GetString());
    }

    /// <summary>乱码游标不应抛异常，回退到首页。</summary>
    [Fact]
    public async Task ListActivities_Invalid_Cursor_FallsBack_To_FirstPage()
    {
        await using var db = NewDb("activities-badcursor");
        var services = new StewardServices(db, new FakeAuditLogger());
        SeedActivity(db, 1, 0);
        SeedActivity(db, 1, 1);
        await db.SaveChangesAsync();

        var result = await services.ListActivitiesAsync(1, 10, "not-a-cursor!!");
        Assert.True(result.Succeeded);
        var data = ParsePage(result.Data!);
        Assert.Equal(2, data.Items);
    }

    /// <summary>跨家庭或不存在活动详情返回 404。</summary>
    [Fact]
    public async Task GetActivity_NotFound_Returns_404()
    {
        await using var db = NewDb("activity-notfound");
        var services = new StewardServices(db, new FakeAuditLogger());
        var activity = SeedActivity(db, 1, 0);
        await db.SaveChangesAsync();

        var crossHome = await services.GetActivityAsync(2, activity.Id);
        Assert.Equal(404, crossHome.StatusCode);
        var missing = await services.GetActivityAsync(1, 9999);
        Assert.Equal(404, missing.StatusCode);
    }

    /// <summary>撤销已完成的可撤销活动成功，写入 activity_undo 审计并置位撤销时间。</summary>
    [Fact]
    public async Task Undo_Completed_Undoable_Succeeds_And_Audits()
    {
        await using var db = NewDb("undo-ok");
        var audit = new FakeAuditLogger();
        var services = new StewardServices(db, audit);
        var activity = SeedActivity(db, 1, 0);
        activity.Status = StewardActivityStatus.Completed;
        activity.Undoable = true;
        await db.SaveChangesAsync();

        var result = await services.UndoActivityAsync(1, 1, activity.Id);
        Assert.True(result.Succeeded);
        Assert.Equal(1, audit.LoggedCount);
        Assert.Equal(FamilyAuditActions.ActivityUndo, audit.LastAction);
        Assert.Equal(FamilyAuditTargetTypes.StewardActivity, audit.LastTargetType);

        var view = Assert.IsType<StewardActivityView>(result.Data);
        Assert.NotNull(view.UndoneAt);
        Assert.False(view.Undoable);
    }

    /// <summary>未完成活动不可撤销（422）。</summary>
    [Fact]
    public async Task Undo_NonCompleted_Rejected_422()
    {
        await using var db = NewDb("undo-noncompleted");
        var services = new StewardServices(db, new FakeAuditLogger());
        var activity = SeedActivity(db, 1, 0);
        activity.Status = StewardActivityStatus.Pending;
        activity.Undoable = true;
        await db.SaveChangesAsync();

        var result = await services.UndoActivityAsync(1, 1, activity.Id);
        Assert.Equal(422, result.StatusCode);
    }

    /// <summary>不可撤销活动返回 422。</summary>
    [Fact]
    public async Task Undo_NonUndoable_Rejected_422()
    {
        await using var db = NewDb("undo-nonundoable");
        var services = new StewardServices(db, new FakeAuditLogger());
        var activity = SeedActivity(db, 1, 0);
        activity.Status = StewardActivityStatus.Completed;
        activity.Undoable = false;
        await db.SaveChangesAsync();

        var result = await services.UndoActivityAsync(1, 1, activity.Id);
        Assert.Equal(422, result.StatusCode);
    }

    /// <summary>已撤销活动重复撤销返回 409。</summary>
    [Fact]
    public async Task Undo_Already_Undone_Rejected_409()
    {
        await using var db = NewDb("undo-already");
        var services = new StewardServices(db, new FakeAuditLogger());
        var activity = SeedActivity(db, 1, 0);
        activity.Status = StewardActivityStatus.Completed;
        activity.Undoable = true;
        activity.UndoneAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var result = await services.UndoActivityAsync(1, 1, activity.Id);
        Assert.Equal(409, result.StatusCode);
    }

    /// <summary>确认列表按风险等级与状态过滤，非法过滤参数返回 422。</summary>
    [Fact]
    public async Task ListConfirmations_Filters_By_RiskLevel_And_Status()
    {
        await using var db = NewDb("confirmations-filter");
        var services = new StewardServices(db, new FakeAuditLogger());
        SeedConfirmation(db, 1, ConfirmationRiskLevel.L1, ConfirmationItemStatus.Pending, "低风险项", null, null);
        SeedConfirmation(db, 1, ConfirmationRiskLevel.L2, ConfirmationItemStatus.Pending, "中风险项", null, null);
        SeedConfirmation(db, 1, ConfirmationRiskLevel.L3, ConfirmationItemStatus.Confirmed, "已确认高风险", null, null);
        await db.SaveChangesAsync();

        var l2 = await services.ListConfirmationsAsync(1, ConfirmationRiskLevel.L2, null);
        Assert.True(l2.Succeeded);
        Assert.Single(Assert.IsType<ConfirmationItemView[]>(l2.Data));

        var pending = await services.ListConfirmationsAsync(1, null, ConfirmationItemStatus.Pending);
        Assert.True(pending.Succeeded);
        Assert.Equal(2, Assert.IsType<ConfirmationItemView[]>(pending.Data).Length);

        var bad = await services.ListConfirmationsAsync(1, "L4", null);
        Assert.Equal(422, bad.StatusCode);
    }

    /// <summary>L2 确认成功：确认项转 confirmed，关联 pending 活动转 confirmed，生成 reporting 动态并审计。</summary>
    [Fact]
    public async Task Confirm_L2_Pending_Succeeds_And_Updates_Linked_Activity()
    {
        await using var db = NewDb("confirm-l2");
        var audit = new FakeAuditLogger();
        var services = new StewardServices(db, audit);
        var activity = SeedActivity(db, 1, 0);
        activity.Status = StewardActivityStatus.Pending;
        await db.SaveChangesAsync();
        var item = SeedConfirmation(db, 1, ConfirmationRiskLevel.L2, ConfirmationItemStatus.Pending, "调低热水器温度", null, activity.Id);
        await db.SaveChangesAsync();

        var result = await services.ConfirmAsync(1, 1, item.Id, new ConfirmationConfirmRequest { IdempotencyKey = Guid.NewGuid().ToString() });
        Assert.True(result.Succeeded);
        Assert.Equal(ConfirmationItemStatus.Confirmed, Assert.IsType<ConfirmationItemView>(result.Data).Status);
        Assert.Equal(FamilyAuditActions.ConfirmationConfirm, audit.LastAction);

        Assert.Equal(StewardActivityStatus.Confirmed, (await db.StewardActivities.SingleAsync(x => x.Id == activity.Id)).Status);
        Assert.True(await db.StewardActivities.AnyAsync(x => x.Title == "已确认：调低热水器温度" && x.Category == StewardActivityCategory.Reporting));
    }

    /// <summary>非法幂等键返回 422。</summary>
    [Fact]
    public async Task Confirm_Invalid_IdempotencyKey_422()
    {
        await using var db = NewDb("confirm-badkey");
        var services = new StewardServices(db, new FakeAuditLogger());
        var item = SeedConfirmation(db, 1, ConfirmationRiskLevel.L2, ConfirmationItemStatus.Pending, "待确认", null, null);
        await db.SaveChangesAsync();

        var result = await services.ConfirmAsync(1, 1, item.Id, new ConfirmationConfirmRequest { IdempotencyKey = "not-uuid" });
        Assert.Equal(422, result.StatusCode);
    }

    /// <summary>重复确认已确认项返回 200 重放且不重复审计。</summary>
    [Fact]
    public async Task Confirm_Already_Confirmed_Replays_200()
    {
        await using var db = NewDb("confirm-replay");
        var audit = new FakeAuditLogger();
        var services = new StewardServices(db, audit);
        var item = SeedConfirmation(db, 1, ConfirmationRiskLevel.L2, ConfirmationItemStatus.Confirmed, "已确认项", null, null);
        await db.SaveChangesAsync();

        var result = await services.ConfirmAsync(1, 1, item.Id, new ConfirmationConfirmRequest { IdempotencyKey = Guid.NewGuid().ToString() });
        Assert.True(result.Succeeded);
        Assert.Equal(0, audit.LoggedCount);
    }

    /// <summary>已拒绝项再次确认返回 409。</summary>
    [Fact]
    public async Task Confirm_Denied_Item_409()
    {
        await using var db = NewDb("confirm-denied");
        var services = new StewardServices(db, new FakeAuditLogger());
        var item = SeedConfirmation(db, 1, ConfirmationRiskLevel.L3, ConfirmationItemStatus.Denied, "已拒绝项", null, null);
        await db.SaveChangesAsync();

        var result = await services.ConfirmAsync(1, 1, item.Id, new ConfirmationConfirmRequest { IdempotencyKey = Guid.NewGuid().ToString() });
        Assert.Equal(409, result.StatusCode);
    }

    /// <summary>过期确认项返回 409。</summary>
    [Fact]
    public async Task Confirm_Expired_Item_409()
    {
        await using var db = NewDb("confirm-expired");
        var services = new StewardServices(db, new FakeAuditLogger());
        var item = SeedConfirmation(db, 1, ConfirmationRiskLevel.L2, ConfirmationItemStatus.Pending, "过期项", DateTime.UtcNow.AddMinutes(-1), null);
        await db.SaveChangesAsync();

        var result = await services.ConfirmAsync(1, 1, item.Id, new ConfirmationConfirmRequest { IdempotencyKey = Guid.NewGuid().ToString() });
        Assert.Equal(409, result.StatusCode);
    }

    /// <summary>拒绝必须提供原因。</summary>
    [Fact]
    public async Task Deny_Blank_Reason_422()
    {
        await using var db = NewDb("deny-noreason");
        var services = new StewardServices(db, new FakeAuditLogger());
        var item = SeedConfirmation(db, 1, ConfirmationRiskLevel.L2, ConfirmationItemStatus.Pending, "待拒绝", null, null);
        await db.SaveChangesAsync();

        var result = await services.DenyAsync(1, 1, item.Id, new ConfirmationDenyRequest { Reason = "  " });
        Assert.Equal(422, result.StatusCode);
    }

    /// <summary>拒绝成功：确认项转 denied，关联 pending 活动转 cancelled，原因入审计。</summary>
    [Fact]
    public async Task Deny_Succeeds_And_Audits_With_Reason()
    {
        await using var db = NewDb("deny-ok");
        var audit = new FakeAuditLogger();
        var services = new StewardServices(db, audit);
        var activity = SeedActivity(db, 1, 0);
        activity.Status = StewardActivityStatus.Pending;
        await db.SaveChangesAsync();
        var item = SeedConfirmation(db, 1, ConfirmationRiskLevel.L3, ConfirmationItemStatus.Pending, "夜间开门", null, activity.Id);
        await db.SaveChangesAsync();

        var result = await services.DenyAsync(1, 1, item.Id, new ConfirmationDenyRequest { Reason = "未授权操作" });
        Assert.True(result.Succeeded);
        Assert.Equal(ConfirmationItemStatus.Denied, Assert.IsType<ConfirmationItemView>(result.Data).Status);
        Assert.Equal(FamilyAuditActions.ConfirmationDeny, audit.LastAction);
        Assert.Equal("未授权操作", audit.LastReason);
        Assert.Equal(StewardActivityStatus.Cancelled, (await db.StewardActivities.SingleAsync(x => x.Id == activity.Id)).Status);
    }

    /// <summary>批量确认全 L1 pending 成功：全部确认、幂等记录落表、汇总动态、审计。</summary>
    [Fact]
    public async Task BatchConfirm_All_L1_Pending_Succeeds()
    {
        await using var db = NewDb("batch-ok");
        var audit = new FakeAuditLogger();
        var services = new StewardServices(db, audit);
        var a1 = SeedConfirmation(db, 1, ConfirmationRiskLevel.L1, ConfirmationItemStatus.Pending, "开阳台灯", null, null);
        var a2 = SeedConfirmation(db, 1, ConfirmationRiskLevel.L1, ConfirmationItemStatus.Pending, "关客厅空调", null, null);
        await db.SaveChangesAsync();

        var result = await services.BatchConfirmAsync(1, 1, new ConfirmationBatchConfirmRequest
        {
            ConfirmationIds = new[] { a1.Id, a2.Id },
            IdempotencyKey = Guid.NewGuid().ToString()
        });
        Assert.True(result.Succeeded);
        var view = Assert.IsType<ConfirmationBatchResultView>(result.Data);
        Assert.Equal(2, view.ConfirmedCount);
        Assert.True(view.Items.All(x => x.Status == ConfirmationItemStatus.Confirmed));
        Assert.Equal(FamilyAuditActions.ConfirmationBatch, audit.LastAction);
        Assert.Equal(1, await db.ConfirmationBatchRecords.CountAsync());
        Assert.True(await db.StewardActivities.AnyAsync(x => x.Title.StartsWith("已批量确认")));
    }

    /// <summary>批量确认含 L2 项整体拒绝 409，全部保持 pending。</summary>
    [Fact]
    public async Task BatchConfirm_Contains_L2_Rejects_409_Nothing_Changed()
    {
        await using var db = NewDb("batch-l2");
        var services = new StewardServices(db, new FakeAuditLogger());
        var a1 = SeedConfirmation(db, 1, ConfirmationRiskLevel.L1, ConfirmationItemStatus.Pending, "低风险", null, null);
        var a2 = SeedConfirmation(db, 1, ConfirmationRiskLevel.L2, ConfirmationItemStatus.Pending, "中风险", null, null);
        await db.SaveChangesAsync();

        var result = await services.BatchConfirmAsync(1, 1, new ConfirmationBatchConfirmRequest
        {
            ConfirmationIds = new[] { a1.Id, a2.Id },
            IdempotencyKey = Guid.NewGuid().ToString()
        });
        Assert.Equal(409, result.StatusCode);
        Assert.All(await db.ConfirmationItems.ToListAsync(), x => Assert.Equal(ConfirmationItemStatus.Pending, x.Status));
    }

    /// <summary>批量确认含跨家庭 ID 整体拒绝 404，全部保持 pending。</summary>
    [Fact]
    public async Task BatchConfirm_Contains_Cross_Home_Id_Rejects_404_Nothing_Changed()
    {
        await using var db = NewDb("batch-crosshome");
        var services = new StewardServices(db, new FakeAuditLogger());
        var a1 = SeedConfirmation(db, 1, ConfirmationRiskLevel.L1, ConfirmationItemStatus.Pending, "本家庭", null, null);
        var a2 = SeedConfirmation(db, 2, ConfirmationRiskLevel.L1, ConfirmationItemStatus.Pending, "他家", null, null);
        await db.SaveChangesAsync();

        var result = await services.BatchConfirmAsync(1, 1, new ConfirmationBatchConfirmRequest
        {
            ConfirmationIds = new[] { a1.Id, a2.Id },
            IdempotencyKey = Guid.NewGuid().ToString()
        });
        Assert.Equal(404, result.StatusCode);
        Assert.All(await db.ConfirmationItems.ToListAsync(), x => Assert.Equal(ConfirmationItemStatus.Pending, x.Status));
    }

    /// <summary>批量确认含已终态项整体拒绝 409。</summary>
    [Fact]
    public async Task BatchConfirm_Contains_Terminal_Item_Rejects_409()
    {
        await using var db = NewDb("batch-terminal");
        var services = new StewardServices(db, new FakeAuditLogger());
        var a1 = SeedConfirmation(db, 1, ConfirmationRiskLevel.L1, ConfirmationItemStatus.Pending, "待确认", null, null);
        var a2 = SeedConfirmation(db, 1, ConfirmationRiskLevel.L1, ConfirmationItemStatus.Confirmed, "已确认", null, null);
        await db.SaveChangesAsync();

        var result = await services.BatchConfirmAsync(1, 1, new ConfirmationBatchConfirmRequest
        {
            ConfirmationIds = new[] { a1.Id, a2.Id },
            IdempotencyKey = Guid.NewGuid().ToString()
        });
        Assert.Equal(409, result.StatusCode);
    }

    /// <summary>批量确认含过期项整体拒绝 409。</summary>
    [Fact]
    public async Task BatchConfirm_Contains_Expired_Item_Rejects_409()
    {
        await using var db = NewDb("batch-expired");
        var services = new StewardServices(db, new FakeAuditLogger());
        var a1 = SeedConfirmation(db, 1, ConfirmationRiskLevel.L1, ConfirmationItemStatus.Pending, "待确认", null, null);
        var a2 = SeedConfirmation(db, 1, ConfirmationRiskLevel.L1, ConfirmationItemStatus.Pending, "已过期", DateTime.UtcNow.AddMinutes(-1), null);
        await db.SaveChangesAsync();

        var result = await services.BatchConfirmAsync(1, 1, new ConfirmationBatchConfirmRequest
        {
            ConfirmationIds = new[] { a1.Id, a2.Id },
            IdempotencyKey = Guid.NewGuid().ToString()
        });
        Assert.Equal(409, result.StatusCode);
    }

    /// <summary>批量确认请求形状非法：重复 ID / 空列表 / 非法键均返回 422。</summary>
    [Fact]
    public async Task BatchConfirm_Invalid_Shapes_Reject_422()
    {
        await using var db = NewDb("batch-shapes");
        var services = new StewardServices(db, new FakeAuditLogger());
        var a1 = SeedConfirmation(db, 1, ConfirmationRiskLevel.L1, ConfirmationItemStatus.Pending, "待确认", null, null);
        await db.SaveChangesAsync();
        var key = Guid.NewGuid().ToString();

        var dup = await services.BatchConfirmAsync(1, 1, new ConfirmationBatchConfirmRequest { ConfirmationIds = new[] { a1.Id, a1.Id }, IdempotencyKey = key });
        Assert.Equal(422, dup.StatusCode);

        var empty = await services.BatchConfirmAsync(1, 1, new ConfirmationBatchConfirmRequest { ConfirmationIds = Array.Empty<long>(), IdempotencyKey = key });
        Assert.Equal(422, empty.StatusCode);

        var badKey = await services.BatchConfirmAsync(1, 1, new ConfirmationBatchConfirmRequest { ConfirmationIds = new[] { a1.Id }, IdempotencyKey = "nope" });
        Assert.Equal(422, badKey.StatusCode);
    }

    /// <summary>同幂等键同 ID 集合重放首次结果，确认项不被二次触碰且审计不重复。</summary>
    [Fact]
    public async Task BatchConfirm_Same_Key_Same_Ids_Replays_Stored_Result()
    {
        await using var db = NewDb("batch-replay");
        var audit = new FakeAuditLogger();
        var services = new StewardServices(db, audit);
        var a1 = SeedConfirmation(db, 1, ConfirmationRiskLevel.L1, ConfirmationItemStatus.Pending, "待确认", null, null);
        await db.SaveChangesAsync();
        var key = Guid.NewGuid().ToString();

        var first = await services.BatchConfirmAsync(1, 1, new ConfirmationBatchConfirmRequest { ConfirmationIds = new[] { a1.Id }, IdempotencyKey = key });
        Assert.True(first.Succeeded);
        Assert.Equal(1, audit.LoggedCount);

        var second = await services.BatchConfirmAsync(1, 1, new ConfirmationBatchConfirmRequest { ConfirmationIds = new[] { a1.Id }, IdempotencyKey = key });
        Assert.True(second.Succeeded);
        Assert.Equal("批量确认已完成（幂等重放）。", second.Message);
        Assert.Equal(1, audit.LoggedCount);
        Assert.Single(await db.ConfirmationBatchRecords.ToListAsync());
    }

    /// <summary>同幂等键不同 ID 集合返回 409。</summary>
    [Fact]
    public async Task BatchConfirm_Same_Key_Different_Ids_Rejects_409()
    {
        await using var db = NewDb("batch-key-conflict");
        var services = new StewardServices(db, new FakeAuditLogger());
        var a1 = SeedConfirmation(db, 1, ConfirmationRiskLevel.L1, ConfirmationItemStatus.Pending, "项一", null, null);
        var a2 = SeedConfirmation(db, 1, ConfirmationRiskLevel.L1, ConfirmationItemStatus.Pending, "项二", null, null);
        await db.SaveChangesAsync();
        var key = Guid.NewGuid().ToString();

        var first = await services.BatchConfirmAsync(1, 1, new ConfirmationBatchConfirmRequest { ConfirmationIds = new[] { a1.Id }, IdempotencyKey = key });
        Assert.True(first.Succeeded);

        var second = await services.BatchConfirmAsync(1, 1, new ConfirmationBatchConfirmRequest { ConfirmationIds = new[] { a2.Id }, IdempotencyKey = key });
        Assert.Equal(409, second.StatusCode);
    }

    /// <summary>批量确认将关联的 pending 活动一并转为 confirmed。</summary>
    [Fact]
    public async Task BatchConfirm_Linked_Activities_Moved_To_Confirmed()
    {
        await using var db = NewDb("batch-linked");
        var services = new StewardServices(db, new FakeAuditLogger());
        var activity = SeedActivity(db, 1, 0);
        activity.Status = StewardActivityStatus.Pending;
        await db.SaveChangesAsync();
        var item = SeedConfirmation(db, 1, ConfirmationRiskLevel.L1, ConfirmationItemStatus.Pending, "批量联动", null, activity.Id);
        await db.SaveChangesAsync();

        var result = await services.BatchConfirmAsync(1, 1, new ConfirmationBatchConfirmRequest
        {
            ConfirmationIds = new[] { item.Id },
            IdempotencyKey = Guid.NewGuid().ToString()
        });
        Assert.True(result.Succeeded);
        Assert.Equal(StewardActivityStatus.Confirmed, (await db.StewardActivities.SingleAsync(x => x.Id == activity.Id)).Status);
    }

    private static HomeMindDbContext NewDb(string name) =>
        new(new DbContextOptionsBuilder<HomeMindDbContext>()
            .UseInMemoryDatabase($"hm-b12-steward-{name}-{Guid.NewGuid()}")
            .Options);

    /// <summary>构造一条管家动态（创建时间随序号递增，保证游标分页稳定）。</summary>
    private static StewardActivity SeedActivity(HomeMindDbContext db, long homeId, int ordinal)
    {
        var now = DateTime.UtcNow.AddSeconds(ordinal);
        var activity = new StewardActivity
        {
            HomeId = homeId,
            Category = StewardActivityCategory.Sensing,
            Title = $"活动 {ordinal}",
            RiskLevel = ConfirmationRiskLevel.L1,
            Status = StewardActivityStatus.Pending,
            Undoable = false,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.StewardActivities.Add(activity);
        return activity;
    }

    /// <summary>构造一条确认项。</summary>
    private static ConfirmationItem SeedConfirmation(HomeMindDbContext db, long homeId, string riskLevel, string status, string title, DateTime? expiresAt, long? activityId)
    {
        var now = DateTime.UtcNow;
        var item = new ConfirmationItem
        {
            HomeId = homeId,
            ActivityId = activityId,
            RiskLevel = riskLevel,
            Title = title,
            Status = status,
            ExpiresAt = expiresAt,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.ConfirmationItems.Add(item);
        return item;
    }

    /// <summary>假审计日志写入器，记录调用次数、动作、目标类型与原因。</summary>
    private sealed class FakeAuditLogger : IFamilyAuditLogger
    {
        public int LoggedCount { get; private set; }
        public string? LastAction { get; private set; }
        public string? LastTargetType { get; private set; }
        public string? LastReason { get; private set; }

        public Task<bool> LogAsync(long homeId, long? actorUserId, string action, string targetType, long? targetId, object? before, object? after, string? reason, long? relatedRunId, CancellationToken cancellationToken = default)
        {
            LoggedCount++;
            LastAction = action;
            LastTargetType = targetType;
            LastReason = reason;
            return Task.FromResult(true);
        }
    }
}
