using HomeMind.Business.Services.Agent;
using HomeMind.Common.Model.Entities;
using HomeMind.Common.Model.ViewModel.Data.AI;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;
using Xunit;
using ConversationEntity = HomeMind.Common.Model.Entities.Conversation;
using ExpertEntity = HomeMind.Common.Model.Entities.Expert;

namespace HomeMind.Business.Services.Tests;

/// <summary>AgentRun 与会话联动的定向测试：conversationId 归属校验、落库与幂等重放。</summary>
public class AgentRunConversationTests
{
    private const long TenantId = 1;
    private const long UserId = 1;

    /// <summary>携带 ConversationId 创建的运行正确落库，幂等重放保留会话归属。</summary>
    [Fact]
    public async Task Create_Persists_ConversationId_And_Replays_With_Same_Conversation()
    {
        await using var db = NewDb("run-conversation");
        Seed(db);
        await db.SaveChangesAsync();
        var services = new AgentRunServices(db);

        var first = await services.CreateAsync(UserId, TenantId, Request(conversationId: 1, key: "11111111-1111-1111-1111-111111111111"), default);
        var view = Assert.IsType<AgentRunView>(first.Data);
        Assert.Equal(201, first.StatusCode);
        Assert.Equal(1, view.ConversationId);

        var replay = await services.CreateAsync(UserId, TenantId, Request(conversationId: 1, key: "11111111-1111-1111-1111-111111111111"), default);
        Assert.Equal(200, replay.StatusCode);
        var replayed = Assert.IsType<AgentRunView>(replay.Data);
        Assert.Equal(view.Id, replayed.Id);
        Assert.Equal(1, replayed.ConversationId);

        var stored = await db.AgentRuns.SingleAsync(x => x.Id == view.Id);
        Assert.Equal(1, stored.ConversationId);
    }

    /// <summary>会话属于他人、不存在或已软删时创建运行一律 404。</summary>
    [Fact]
    public async Task Create_Rejects_Conversation_Not_Owned_Or_Deleted_With_404()
    {
        await using var db = NewDb("run-conversation-404");
        Seed(db);
        db.Conversations.Add(new ConversationEntity { Id = 2, TenantId = TenantId, OwnerUserId = 99, Title = "他人的", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        db.Conversations.Add(new ConversationEntity { Id = 3, TenantId = TenantId, OwnerUserId = UserId, Title = "已删", DeletedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var services = new AgentRunServices(db);

        var others = await services.CreateAsync(UserId, TenantId, Request(conversationId: 2, key: "22222222-2222-2222-2222-222222222222"), default);
        var missing = await services.CreateAsync(UserId, TenantId, Request(conversationId: 999, key: "33333333-3333-3333-3333-333333333333"), default);
        var deleted = await services.CreateAsync(UserId, TenantId, Request(conversationId: 3, key: "44444444-4444-4444-4444-444444444444"), default);

        Assert.Equal(404, others.StatusCode);
        Assert.Equal(404, missing.StatusCode);
        Assert.Equal(404, deleted.StatusCode);
    }

    /// <summary>同幂等键用于其他会话时返回 409，不得重放。</summary>
    [Fact]
    public async Task Create_Returns_409_When_Key_Used_For_Other_Conversation()
    {
        await using var db = NewDb("run-conversation-409");
        Seed(db);
        db.Conversations.Add(new ConversationEntity { Id = 2, TenantId = TenantId, OwnerUserId = UserId, Title = "另一会话", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var services = new AgentRunServices(db);

        var first = await services.CreateAsync(UserId, TenantId, Request(conversationId: 1, key: "55555555-5555-5555-5555-555555555555"), default);
        Assert.Equal(201, first.StatusCode);

        var other = await services.CreateAsync(UserId, TenantId, Request(conversationId: 2, key: "55555555-5555-5555-5555-555555555555"), default);

        Assert.Equal(409, other.StatusCode);
    }

    private static AgentRunCreateRequest Request(long conversationId, string key) =>
        new("expert", 10, """{"messages":[{"role":"user","content":"你好"}]}""", key, conversationId);

    private static HomeMindDbContext NewDb(string name) =>
        new(new DbContextOptionsBuilder<HomeMindDbContext>()
            .UseInMemoryDatabase($"hm-b20-agentrun-conversation-{name}-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private static void Seed(HomeMindDbContext db)
    {
        db.Tenants.Add(new Tenant { Id = TenantId, TenantType = "personal", Code = "t1", Name = "家庭1", Status = "active", OwnerUserId = UserId, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        db.Users.Add(new User { Id = UserId, DisplayName = "用户1", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        db.Experts.Add(new ExpertEntity { Id = 10, TenantId = TenantId, OwnerUserId = null, Code = "e10", Name = "专家10", Category = "test", ExpertType = "builtin", Status = "active", Description = "测试专家" });
        db.ExpertVersions.Add(new ExpertVersion { Id = 1001, TenantId = TenantId, ExpertId = 10, Version = 1, Status = "published", Persona = "人设", Methodology = "方法论", PromptTemplate = "模板", EstimatedCredits = 1 });
        db.Conversations.Add(new ConversationEntity { Id = 1, TenantId = TenantId, OwnerUserId = UserId, Title = "会话1", ExpertId = 10, ExpertVersionId = 1001, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
    }
}
