using System.Text.Json;
using HomeMind.Business.IServices.Family;
using HomeMind.Business.Services.Conversation;
using HomeMind.Common.Model.Entities;
using HomeMind.Common.Model.Entities.Family;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.AI;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using ConversationEntity = HomeMind.Common.Model.Entities.Conversation;
using ExpertEntity = HomeMind.Common.Model.Entities.Expert;

namespace HomeMind.Business.Services.Tests;

/// <summary>专家会话服务定向测试：归属隔离、游标分页、上下文拼接、幂等与审计。</summary>
public class ConversationServicesTests
{
    private const long TenantId = 1;
    private const long UserId = 1;

    /// <summary>创建会话成功并写 conversation_create 审计。</summary>
    [Fact]
    public async Task Create_Succeeds_And_Writes_Audit()
    {
        await using var db = NewDb("create-ok");
        var audit = new FakeAuditLogger();
        SeedBase(db);
        await db.SaveChangesAsync();
        var services = NewServices(db, audit);

        var result = await services.CreateAsync(UserId, TenantId, new ConversationCreateRequest { Title = "装修咨询" }, default);

        Assert.True(result.Succeeded);
        Assert.Equal(201, result.StatusCode);
        var view = Assert.IsType<ConversationView>(result.Data);
        Assert.Equal("装修咨询", view.Title);
        Assert.Equal(FamilyAuditActions.ConversationCreate, audit.LastAction);
    }

    /// <summary>创建会话绑定可见专家时解析最新已发布版本。</summary>
    [Fact]
    public async Task Create_Binds_Expert_To_Latest_Published_Version()
    {
        await using var db = NewDb("create-expert");
        var audit = new FakeAuditLogger();
        SeedBase(db);
        SeedExpert(db, expertId: 10, tenantId: TenantId, ownerUserId: null);
        db.ExpertVersions.Add(new ExpertVersion { Id = 1002, TenantId = TenantId, ExpertId = 10, Version = 2, Status = "published", Persona = "v2", Methodology = "m", PromptTemplate = "p", EstimatedCredits = 1 });
        await db.SaveChangesAsync();
        var services = NewServices(db, audit);

        var result = await services.CreateAsync(UserId, TenantId, new ConversationCreateRequest { Title = "装修", ExpertId = 10 }, default);

        var view = Assert.IsType<ConversationView>(result.Data);
        Assert.Equal(10, view.ExpertId);
        Assert.Equal(1002, view.ExpertVersionId);
    }

    /// <summary>绑定跨租户/他人自建/不存在的专家一律 404。</summary>
    [Fact]
    public async Task Create_Rejects_Invisible_Expert_With_404()
    {
        await using var db = NewDb("create-invisible");
        var audit = new FakeAuditLogger();
        SeedBase(db);
        SeedExpert(db, expertId: 10, tenantId: 2, ownerUserId: null);      // 跨租户
        SeedExpert(db, expertId: 11, tenantId: TenantId, ownerUserId: 99); // 他人自建
        await db.SaveChangesAsync();
        var services = NewServices(db, audit);

        var crossTenant = await services.CreateAsync(UserId, TenantId, new ConversationCreateRequest { Title = "a", ExpertId = 10 }, default);
        var othersMine = await services.CreateAsync(UserId, TenantId, new ConversationCreateRequest { Title = "b", ExpertId = 11 }, default);
        var missing = await services.CreateAsync(UserId, TenantId, new ConversationCreateRequest { Title = "c", ExpertId = 999 }, default);

        Assert.Equal(404, crossTenant.StatusCode);
        Assert.Equal(404, othersMine.StatusCode);
        Assert.Equal(404, missing.StatusCode);
    }

    /// <summary>列表仅返回本人且排除软删除的会话。</summary>
    [Fact]
    public async Task List_Returns_Only_Owned_Non_Deleted()
    {
        await using var db = NewDb("list-own");
        var audit = new FakeAuditLogger();
        SeedBase(db);
        var mine = new ConversationEntity { Id = 1, TenantId = TenantId, OwnerUserId = UserId, Title = "我的", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var mineDeleted = new ConversationEntity { Id = 2, TenantId = TenantId, OwnerUserId = UserId, Title = "已删", DeletedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var others = new ConversationEntity { Id = 3, TenantId = TenantId, OwnerUserId = 99, Title = "他人的", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        db.Conversations.AddRange(mine, mineDeleted, others);
        await db.SaveChangesAsync();
        var services = NewServices(db, audit);

        var result = await services.ListAsync(UserId, TenantId, 20, null, default);

        var list = Assert.IsType<ConversationListView>(result.Data);
        Assert.Single(list.Items);
        Assert.Equal(1, list.Items[0].Id);
    }

    /// <summary>列表游标分页：25 条时首页 20 条带游标，次页 5 条无重复。</summary>
    [Fact]
    public async Task List_Paginates_By_Cursor_Without_Duplicates()
    {
        await using var db = NewDb("list-paging");
        var audit = new FakeAuditLogger();
        SeedBase(db);
        var baseTime = DateTime.UtcNow;
        for (var i = 1; i <= 25; i++)
            db.Conversations.Add(new ConversationEntity { Id = i, TenantId = TenantId, OwnerUserId = UserId, Title = $"会话{i}", CreatedAt = baseTime.AddSeconds(i), UpdatedAt = baseTime.AddSeconds(i) });
        await db.SaveChangesAsync();
        var services = NewServices(db, audit);

        var first = await services.ListAsync(UserId, TenantId, 20, null, default);
        var firstList = Assert.IsType<ConversationListView>(first.Data);
        Assert.Equal(20, firstList.Items.Count);
        Assert.NotNull(firstList.Cursor);

        var second = await services.ListAsync(UserId, TenantId, 20, firstList.Cursor, default);
        var secondList = Assert.IsType<ConversationListView>(second.Data);
        Assert.Equal(5, secondList.Items.Count);
        Assert.Null(secondList.Cursor);
        Assert.Empty(secondList.Items.Select(x => x.Id).Intersect(firstList.Items.Select(x => x.Id)));
    }

    /// <summary>详情：本人 200；他人/已软删 404。</summary>
    [Fact]
    public async Task Get_Scopes_To_Owner_Only()
    {
        await using var db = NewDb("get-scope");
        var audit = new FakeAuditLogger();
        SeedBase(db);
        db.Conversations.AddRange(
            new ConversationEntity { Id = 1, TenantId = TenantId, OwnerUserId = UserId, Title = "我的", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new ConversationEntity { Id = 2, TenantId = TenantId, OwnerUserId = 99, Title = "他人的", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new ConversationEntity { Id = 3, TenantId = TenantId, OwnerUserId = UserId, Title = "已删", DeletedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var services = NewServices(db, audit);

        Assert.Equal(200, (await services.GetAsync(UserId, TenantId, 1, default)).StatusCode);
        Assert.Equal(404, (await services.GetAsync(UserId, TenantId, 2, default)).StatusCode);
        Assert.Equal(404, (await services.GetAsync(UserId, TenantId, 3, default)).StatusCode);
    }

    /// <summary>更新重命名并写 conversation_rename 审计。</summary>
    [Fact]
    public async Task Update_Renames_And_Writes_Audit()
    {
        await using var db = NewDb("update-rename");
        var audit = new FakeAuditLogger();
        SeedBase(db);
        db.Conversations.Add(new ConversationEntity { Id = 1, TenantId = TenantId, OwnerUserId = UserId, Title = "旧标题", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var services = NewServices(db, audit);

        var result = await services.UpdateAsync(UserId, TenantId, 1, new ConversationUpdateRequest { Title = "新标题", RowVersion = 1 }, default);

        Assert.True(result.Succeeded);
        var view = Assert.IsType<ConversationView>(result.Data);
        Assert.Equal("新标题", view.Title);
        Assert.Equal(2, view.RowVersion);
        Assert.Equal(FamilyAuditActions.ConversationRename, audit.LastAction);
    }

    /// <summary>RowVersion 与服务端不一致返回 409/40903。</summary>
    [Fact]
    public async Task Update_Rejects_Stale_RowVersion_With_409()
    {
        await using var db = NewDb("update-stale");
        var audit = new FakeAuditLogger();
        SeedBase(db);
        db.Conversations.Add(new ConversationEntity { Id = 1, TenantId = TenantId, OwnerUserId = UserId, Title = "旧", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var services = NewServices(db, audit);

        var result = await services.UpdateAsync(UserId, TenantId, 1, new ConversationUpdateRequest { Title = "新", RowVersion = 99 }, default);

        Assert.Equal(409, result.StatusCode);
        Assert.Equal(ApiErrorCodes.OptimisticLockConflict, result.Code);
    }

    /// <summary>软删除写审计，重复删除 404。</summary>
    [Fact]
    public async Task Delete_Soft_Deletes_And_Rejects_Repeat()
    {
        await using var db = NewDb("delete-soft");
        var audit = new FakeAuditLogger();
        SeedBase(db);
        db.Conversations.Add(new ConversationEntity { Id = 1, TenantId = TenantId, OwnerUserId = UserId, Title = "待删", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var services = NewServices(db, audit);

        var first = await services.DeleteAsync(UserId, TenantId, 1, default);
        var second = await services.DeleteAsync(UserId, TenantId, 1, default);

        Assert.True(first.Succeeded);
        Assert.Equal(FamilyAuditActions.ConversationDelete, audit.LastAction);
        Assert.Equal(404, second.StatusCode);
        var conversation = await db.Conversations.SingleAsync(x => x.Id == 1);
        Assert.NotNull(conversation.DeletedAt);
    }

    /// <summary>消息列表按主键倒序游标分页，乱码游标按第一页处理。</summary>
    [Fact]
    public async Task ListMessages_Paginates_And_Tolerates_Bad_Cursor()
    {
        await using var db = NewDb("messages-paging");
        var audit = new FakeAuditLogger();
        SeedBase(db);
        db.Conversations.Add(new ConversationEntity { Id = 1, TenantId = TenantId, OwnerUserId = UserId, Title = "会话", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        for (var i = 1; i <= 5; i++)
            db.ConversationMessages.Add(new ConversationMessage { Id = i, ConversationId = 1, Role = "user", Content = $"消息{i}", RunId = i, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var services = NewServices(db, audit);

        var first = await services.ListMessagesAsync(UserId, TenantId, 1, 2, null, default);
        var firstList = Assert.IsType<ConversationMessageListView>(first.Data);
        Assert.Equal(2, firstList.Items.Count);
        Assert.Equal(5, firstList.Items[0].Id);
        Assert.NotNull(firstList.Cursor);

        var second = await services.ListMessagesAsync(UserId, TenantId, 1, 2, firstList.Cursor, default);
        var secondList = Assert.IsType<ConversationMessageListView>(second.Data);
        Assert.Equal(2, secondList.Items.Count);
        Assert.Equal(3, secondList.Items[0].Id);

        var badCursor = await services.ListMessagesAsync(UserId, TenantId, 1, 2, "!!!not-base64!!!", default);
        var badList = Assert.IsType<ConversationMessageListView>(badCursor.Data);
        Assert.Equal(2, badList.Items.Count);
    }

    /// <summary>未绑定专家的会话发送消息返回 422/42200。</summary>
    [Fact]
    public async Task PrepareMessage_Rejects_Unbound_Conversation_With_422()
    {
        await using var db = NewDb("prepare-unbound");
        var audit = new FakeAuditLogger();
        SeedBase(db);
        db.Conversations.Add(new ConversationEntity { Id = 1, TenantId = TenantId, OwnerUserId = UserId, Title = "无专家", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var services = NewServices(db, audit);

        var result = await services.PrepareMessageAsync(UserId, TenantId, 1, "你好", default);

        Assert.Equal(422, result.StatusCode);
        Assert.Equal(ApiErrorCodes.PreconditionFailed, result.Code);
    }

    /// <summary>绑定专家后上下文拼接包含升序历史与最新 user 消息。</summary>
    [Fact]
    public async Task PrepareMessage_Builds_Context_With_History_And_New_Message()
    {
        await using var db = NewDb("prepare-context");
        var audit = new FakeAuditLogger();
        SeedBase(db);
        SeedExpert(db, expertId: 10, tenantId: TenantId, ownerUserId: null);
        db.Conversations.Add(new ConversationEntity { Id = 1, TenantId = TenantId, OwnerUserId = UserId, Title = "会话", ExpertId = 10, ExpertVersionId = 1001, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        db.ConversationMessages.AddRange(
            new ConversationMessage { Id = 1, ConversationId = 1, Role = "user", Content = "第一问", RunId = 1, CreatedAt = DateTime.UtcNow },
            new ConversationMessage { Id = 2, ConversationId = 1, Role = "assistant", Content = "第一答", RunId = 2, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var services = NewServices(db, audit);

        var result = await services.PrepareMessageAsync(UserId, TenantId, 1, "第二问", default);

        var context = Assert.IsType<PreparedMessageContext>(result.Data);
        Assert.Equal(10, context.ExpertId);
        using var doc = JsonDocument.Parse(context.InputJson);
        var messages = doc.RootElement.GetProperty("messages").EnumerateArray().Select(m => (role: m.GetProperty("role").GetString(), content: m.GetProperty("content").GetString())).ToList();
        Assert.Equal(3, messages.Count);
        Assert.Equal(("user", "第一问"), messages[0]);
        Assert.Equal(("assistant", "第一答"), messages[1]);
        Assert.Equal(("user", "第二问"), messages[2]);
    }

    /// <summary>上下文拼接仅取最近 20 条，超 12000 字符从最旧丢弃。</summary>
    [Fact]
    public async Task PrepareMessage_Truncates_History_By_Count_And_Budget()
    {
        await using var db = NewDb("prepare-truncate");
        var audit = new FakeAuditLogger();
        SeedBase(db);
        SeedExpert(db, expertId: 10, tenantId: TenantId, ownerUserId: null);
        db.Conversations.Add(new ConversationEntity { Id = 1, TenantId = TenantId, OwnerUserId = UserId, Title = "会话", ExpertId = 10, ExpertVersionId = 1001, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        for (var i = 1; i <= 25; i++)
            db.ConversationMessages.Add(new ConversationMessage { Id = i, ConversationId = 1, Role = i % 2 == 0 ? "assistant" : "user", Content = $"msg-{i}" + new string('测', 590), RunId = i, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var services = NewServices(db, audit);

        var result = await services.PrepareMessageAsync(UserId, TenantId, 1, "新问题", default);

        var context = Assert.IsType<PreparedMessageContext>(result.Data);
        using var doc = JsonDocument.Parse(context.InputJson);
        var messages = doc.RootElement.GetProperty("messages").EnumerateArray().ToList();
        // 25 条历史取最近 20 条（Id 6-25，每条 596 字符 → 11920 字符在 12000 预算内），再加最新 user 消息。
        Assert.Equal(21, messages.Count);
        Assert.Equal("新问题", messages[^1].GetProperty("content").GetString());
        var ids = messages
            .Select(m => m.GetProperty("content").GetString()!)
            .Where(c => c.StartsWith("msg-"))
            .Select(c => int.Parse(new string(c.Skip(4).TakeWhile(char.IsDigit).ToArray())))
            .ToArray();
        // 最旧的 5 条（Id 1-5）被丢弃，最近的 20 条（Id 6-25）保留。
        Assert.DoesNotContain(ids, id => id is >= 1 and <= 5);
        Assert.Contains(6, ids);
        Assert.Contains(25, ids);
    }

    /// <summary>跨用户准备消息一律 404。</summary>
    [Fact]
    public async Task PrepareMessage_Cross_User_Returns_404()
    {
        await using var db = NewDb("prepare-cross-user");
        var audit = new FakeAuditLogger();
        SeedBase(db);
        SeedExpert(db, expertId: 10, tenantId: TenantId, ownerUserId: null);
        db.Conversations.Add(new ConversationEntity { Id = 1, TenantId = TenantId, OwnerUserId = UserId, Title = "会话", ExpertId = 10, ExpertVersionId = 1001, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var services = NewServices(db, audit);

        var result = await services.PrepareMessageAsync(99, TenantId, 1, "你好", default);

        Assert.Equal(404, result.StatusCode);
    }

    /// <summary>user 消息带 run_id 落库，同 (conversation_id, run_id) 幂等不新增。</summary>
    [Fact]
    public async Task RecordUserMessage_Is_Idempotent_By_Run()
    {
        await using var db = NewDb("record-idempotent");
        var audit = new FakeAuditLogger();
        SeedBase(db);
        db.Conversations.Add(new ConversationEntity { Id = 1, TenantId = TenantId, OwnerUserId = UserId, Title = "会话", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var services = NewServices(db, audit);

        var first = await services.RecordUserMessageAsync(UserId, TenantId, 1, 100, "内容", default);
        var second = await services.RecordUserMessageAsync(UserId, TenantId, 1, 100, "内容", default);

        Assert.Equal(201, first.StatusCode);
        Assert.Equal(200, second.StatusCode);
        Assert.Equal(first.Data, second.Data);
        Assert.Single(db.ConversationMessages.Where(x => x.RunId == 100).ToList());
    }

    /// <summary>assistant 消息在 Run 终态后落库，幂等；会话不存在不抛异常。</summary>
    [Fact]
    public async Task AppendAssistantMessage_Is_Idempotent_And_Tolerant()
    {
        await using var db = NewDb("append-assistant");
        var audit = new FakeAuditLogger();
        SeedBase(db);
        db.Conversations.Add(new ConversationEntity { Id = 1, TenantId = TenantId, OwnerUserId = UserId, Title = "会话", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var services = NewServices(db, audit);

        var first = await services.AppendAssistantMessageAsync(TenantId, 1, 100, "回答", default);
        var second = await services.AppendAssistantMessageAsync(TenantId, 1, 100, "回答", default);
        var missing = await services.AppendAssistantMessageAsync(TenantId, 999, 100, "回答", default);

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.True(missing.Succeeded);
        var messages = db.ConversationMessages.Where(x => x.RunId == 100).ToList();
        Assert.Single(messages);
        Assert.Equal("assistant", messages[0].Role);
    }

    private static HomeMindDbContext NewDb(string name) =>
        new(new DbContextOptionsBuilder<HomeMindDbContext>()
            .UseInMemoryDatabase($"hm-b20-conversation-{name}-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private static ConversationServices NewServices(HomeMindDbContext db, IFamilyAuditLogger audit) =>
        new(db, audit, NullLogger<ConversationServices>.Instance);

    private static void SeedBase(HomeMindDbContext db)
    {
        db.Tenants.Add(new Tenant { Id = TenantId, TenantType = "personal", Code = "t1", Name = "家庭1", Status = "active", OwnerUserId = UserId, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        db.Users.Add(new User { Id = UserId, DisplayName = "用户1", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
    }

    private static void SeedExpert(HomeMindDbContext db, long expertId, long tenantId, long? ownerUserId)
    {
        db.Experts.Add(new ExpertEntity { Id = expertId, TenantId = tenantId, OwnerUserId = ownerUserId, Code = $"e{expertId}", Name = $"专家{expertId}", Category = "test", ExpertType = ownerUserId is null ? "builtin" : "custom", Status = "active", Description = "测试专家" });
        db.ExpertVersions.Add(new ExpertVersion { Id = expertId * 100 + 1, TenantId = tenantId, ExpertId = expertId, Version = 1, Status = "published", Persona = "人设", Methodology = "方法论", PromptTemplate = "模板", EstimatedCredits = 1 });
    }

    private sealed class FakeAuditLogger : IFamilyAuditLogger
    {
        public string? LastAction { get; private set; }

        public Task<bool> LogAsync(long homeId, long? actorUserId, string action, string targetType, long? targetId, object? before, object? after, string? reason, long? relatedRunId, CancellationToken cancellationToken = default)
        {
            LastAction = action;
            return Task.FromResult(true);
        }
    }
}
