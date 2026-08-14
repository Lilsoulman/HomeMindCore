using HomeMind.Business.IServices.Family;
using HomeMind.Business.Services.Agent;
using HomeMind.Business.Services.Conversation;
using HomeMind.Business.Services.Expert;
using HomeMind.Common.Model.Entities;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.AI;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using ConversationEntity = HomeMind.Common.Model.Entities.Conversation;
using ExpertEntity = HomeMind.Common.Model.Entities.Expert;

namespace HomeMind.Business.Services.Tests;

/// <summary>自建专家服务定向测试：创建落库、scope 过滤、跨用户 404、版本演进与软删除闭环。</summary>
public class ExpertSelfServeTests
{
    private const long TenantId = 1;
    private const long UserId = 1;

    /// <summary>创建落库：owner=本人、custom/active、code 前缀 custom-，v1 published 版本生成。</summary>
    [Fact]
    public async Task Create_Persists_Owned_Expert_With_V1_Published_Version()
    {
        await using var db = NewDb("create-persist");
        SeedBase(db);
        await db.SaveChangesAsync();
        var services = new ExpertSelfServeServices(db);

        var result = await services.CreateAsync(UserId, TenantId, Request("{\"type\":\"object\",\"properties\":{\"memoryCandidates\":{\"type\":\"array\"}}}"), default);

        Assert.Equal(201, result.StatusCode);
        var view = Assert.IsType<ExpertDetailView>(result.Data);
        Assert.Equal("mine", view.Source);
        Assert.Equal(1, view.Version);
        Assert.StartsWith("custom-", view.Code);
        var stored = await db.Experts.SingleAsync();
        Assert.Equal(UserId, stored.OwnerUserId);
        Assert.Equal("custom", stored.ExpertType);
        Assert.Equal("active", stored.Status);
        var version = await db.ExpertVersions.SingleAsync();
        Assert.Equal(1, version.Version);
        Assert.Equal("published", version.Status);
        Assert.Contains("memoryCandidates", version.OutputSchema!);
    }

    /// <summary>两次创建的编码互不相同。</summary>
    [Fact]
    public async Task Create_Generates_Unique_Codes()
    {
        await using var db = NewDb("create-codes");
        SeedBase(db);
        await db.SaveChangesAsync();
        var services = new ExpertSelfServeServices(db);

        var first = await services.CreateAsync(UserId, TenantId, Request(), default);
        var second = await services.CreateAsync(UserId, TenantId, Request(), default);

        var firstView = Assert.IsType<ExpertDetailView>(first.Data);
        var secondView = Assert.IsType<ExpertDetailView>(second.Data);
        Assert.NotEqual(firstView.Code, secondView.Code);
    }

    /// <summary>缺必填字段与非法 ToolPolicyJson 返回 422。</summary>
    [Fact]
    public async Task Create_Rejects_Missing_Fields_And_Invalid_Json()
    {
        await using var db = NewDb("create-invalid");
        SeedBase(db);
        await db.SaveChangesAsync();
        var services = new ExpertSelfServeServices(db);

        var missingName = await services.CreateAsync(UserId, TenantId, new ExpertCreateRequest { Name = "", Category = "test", Persona = "人设", PromptTemplate = "模板" }, default);
        var invalidJson = await services.CreateAsync(UserId, TenantId, new ExpertCreateRequest { Name = "a", Category = "test", Persona = "人设", PromptTemplate = "模板", ToolPolicyJson = "{not-json" }, default);

        Assert.Equal(422, missingName.StatusCode);
        Assert.Equal(ApiErrorCodes.ValidationFailed, missingName.Code);
        Assert.Equal(422, invalidJson.StatusCode);
    }

    /// <summary>scope 过滤：basic 仅平台基础、mine 仅本人自建、all 两者；他人自建不泄露。</summary>
    [Fact]
    public async Task List_Scopes_Basic_Mine_All_Without_Leaking_Others()
    {
        await using var db = NewDb("list-scope");
        SeedBase(db);
        db.Experts.AddRange(
            new ExpertEntity { Id = 10, TenantId = TenantId, OwnerUserId = null, Code = "e10", Name = "平台专家", Category = "test", ExpertType = "builtin", Status = "active", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new ExpertEntity { Id = 11, TenantId = TenantId, OwnerUserId = UserId, Code = "custom-a", Name = "我的专家", Category = "test", ExpertType = "custom", Status = "active", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new ExpertEntity { Id = 12, TenantId = TenantId, OwnerUserId = 99, Code = "custom-b", Name = "他人专家", Category = "test", ExpertType = "custom", Status = "active", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        db.ExpertVersions.AddRange(
            new ExpertVersion { Id = 101, TenantId = TenantId, ExpertId = 10, Version = 1, Status = "published", Persona = "p", Methodology = "m", PromptTemplate = "t", EstimatedCredits = 1 },
            new ExpertVersion { Id = 111, TenantId = TenantId, ExpertId = 11, Version = 1, Status = "published", Persona = "p", Methodology = "m", PromptTemplate = "t", EstimatedCredits = 1 },
            new ExpertVersion { Id = 121, TenantId = TenantId, ExpertId = 12, Version = 1, Status = "published", Persona = "p", Methodology = "m", PromptTemplate = "t", EstimatedCredits = 1 });
        await db.SaveChangesAsync();
        var services = new ExpertCatalogServices(db);

        var basic = await services.ListAsync(UserId, TenantId, null, null, "expert", "basic", default);
        var mine = await services.ListAsync(UserId, TenantId, null, null, "expert", "mine", default);
        var all = await services.ListAsync(UserId, TenantId, null, null, "expert", "all", default);

        var basicItems = Assert.IsAssignableFrom<IReadOnlyList<ExpertCatalogItemView>>(basic.Data);
        Assert.Single(basicItems);
        Assert.Equal(10, basicItems[0].Id);
        Assert.Equal("basic", basicItems[0].Source);

        var mineItems = Assert.IsAssignableFrom<IReadOnlyList<ExpertCatalogItemView>>(mine.Data);
        Assert.Single(mineItems);
        Assert.Equal(11, mineItems[0].Id);
        Assert.Equal("mine", mineItems[0].Source);

        var allItems = Assert.IsAssignableFrom<IReadOnlyList<ExpertCatalogItemView>>(all.Data);
        Assert.Equal(2, allItems.Count);
        Assert.DoesNotContain(allItems, x => x.Id == 12); // 他人自建不泄露
    }

    /// <summary>详情：本人自建可见（Source=mine）；他人自建与已软删 404。</summary>
    [Fact]
    public async Task Get_Scopes_To_Owner_Only()
    {
        await using var db = NewDb("get-scope");
        SeedBase(db);
        db.Experts.AddRange(
            new ExpertEntity { Id = 11, TenantId = TenantId, OwnerUserId = UserId, Code = "custom-a", Name = "我的专家", Category = "test", ExpertType = "custom", Status = "active", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new ExpertEntity { Id = 12, TenantId = TenantId, OwnerUserId = 99, Code = "custom-b", Name = "他人专家", Category = "test", ExpertType = "custom", Status = "active", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new ExpertEntity { Id = 13, TenantId = TenantId, OwnerUserId = UserId, Code = "custom-c", Name = "已删专家", Category = "test", ExpertType = "custom", Status = "active", DeletedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        db.ExpertVersions.AddRange(
            new ExpertVersion { Id = 111, TenantId = TenantId, ExpertId = 11, Version = 1, Status = "published", Persona = "p", Methodology = "m", PromptTemplate = "t", EstimatedCredits = 1 },
            new ExpertVersion { Id = 121, TenantId = TenantId, ExpertId = 12, Version = 1, Status = "published", Persona = "p", Methodology = "m", PromptTemplate = "t", EstimatedCredits = 1 },
            new ExpertVersion { Id = 131, TenantId = TenantId, ExpertId = 13, Version = 1, Status = "published", Persona = "p", Methodology = "m", PromptTemplate = "t", EstimatedCredits = 1 });
        await db.SaveChangesAsync();
        var services = new ExpertCatalogServices(db);

        var mine = await services.GetAsync(UserId, TenantId, 11, "expert", default);
        var others = await services.GetAsync(UserId, TenantId, 12, "expert", default);
        var deleted = await services.GetAsync(UserId, TenantId, 13, "expert", default);

        Assert.True(mine.Succeeded);
        Assert.Equal("mine", Assert.IsType<ExpertDetailView>(mine.Data).Source);
        Assert.Equal(404, others.StatusCode);
        Assert.Equal(404, deleted.StatusCode);
    }

    /// <summary>更新生成 version+1 已发布版本、RowVersion 递增；RowVersion 不符 409/40903。</summary>
    [Fact]
    public async Task Update_Creates_Next_Version_And_Enforces_Optimistic_Lock()
    {
        await using var db = NewDb("update-version");
        SeedBase(db);
        db.Experts.Add(new ExpertEntity { Id = 11, TenantId = TenantId, OwnerUserId = UserId, Code = "custom-a", Name = "旧名", Category = "test", ExpertType = "custom", Status = "active", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        db.ExpertVersions.Add(new ExpertVersion { Id = 111, TenantId = TenantId, ExpertId = 11, Version = 1, Status = "published", Persona = "p", Methodology = "m", PromptTemplate = "t", EstimatedCredits = 1 });
        await db.SaveChangesAsync();
        var services = new ExpertSelfServeServices(db);

        var result = await services.UpdateAsync(UserId, TenantId, 11, new ExpertUpdateRequest { Name = "新名", Category = "test", Persona = "新p", PromptTemplate = "新t", RowVersion = 1 }, default);

        Assert.True(result.Succeeded);
        var view = Assert.IsType<ExpertDetailView>(result.Data);
        Assert.Equal(2, view.Version);
        Assert.Equal("新名", view.Name);
        var stored = await db.Experts.SingleAsync();
        Assert.Equal(2, stored.RowVersion);
        Assert.Equal(2, await db.ExpertVersions.CountAsync());

        var stale = await services.UpdateAsync(UserId, TenantId, 11, new ExpertUpdateRequest { Name = "x", Category = "test", Persona = "p", PromptTemplate = "t", RowVersion = 1 }, default);
        Assert.Equal(409, stale.StatusCode);
        Assert.Equal(ApiErrorCodes.OptimisticLockConflict, stale.Code);
    }

    /// <summary>更新与删除他人自建专家一律 404。</summary>
    [Fact]
    public async Task Update_And_Delete_Others_Mine_Return_404()
    {
        await using var db = NewDb("others-404");
        SeedBase(db);
        db.Experts.Add(new ExpertEntity { Id = 12, TenantId = TenantId, OwnerUserId = 99, Code = "custom-b", Name = "他人专家", Category = "test", ExpertType = "custom", Status = "active", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        db.ExpertVersions.Add(new ExpertVersion { Id = 121, TenantId = TenantId, ExpertId = 12, Version = 1, Status = "published", Persona = "p", Methodology = "m", PromptTemplate = "t", EstimatedCredits = 1 });
        await db.SaveChangesAsync();
        var services = new ExpertSelfServeServices(db);

        var update = await services.UpdateAsync(UserId, TenantId, 12, new ExpertUpdateRequest { Name = "x", Category = "test", Persona = "p", PromptTemplate = "t", RowVersion = 1 }, default);
        var delete = await services.DeleteAsync(UserId, TenantId, 12, default);

        Assert.Equal(404, update.StatusCode);
        Assert.Equal(404, delete.StatusCode);
    }

    /// <summary>软删除后：目录不含、运行解析 404、会话发送 404（全链路消失）。</summary>
    [Fact]
    public async Task Delete_Removes_Expert_From_Catalog_Run_And_Conversation()
    {
        await using var db = NewDb("delete-closed-loop");
        SeedBase(db);
        var expert = new ExpertEntity { Id = 11, TenantId = TenantId, OwnerUserId = UserId, Code = "custom-a", Name = "我的专家", Category = "test", ExpertType = "custom", Status = "active", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        db.Experts.Add(expert);
        db.ExpertVersions.Add(new ExpertVersion { Id = 111, TenantId = TenantId, ExpertId = 11, Version = 1, Status = "published", Persona = "p", Methodology = "m", PromptTemplate = "t", EstimatedCredits = 1 });
        db.Conversations.Add(new ConversationEntity { Id = 1, TenantId = TenantId, OwnerUserId = UserId, Title = "会话", ExpertId = 11, ExpertVersionId = 111, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var selfServe = new ExpertSelfServeServices(db);
        var catalog = new ExpertCatalogServices(db);
        var runs = new AgentRunServices(db);
        var conversations = new ConversationServices(db, new FakeAuditLogger(), NullLogger<ConversationServices>.Instance);

        var delete = await selfServe.DeleteAsync(UserId, TenantId, 11, default);
        Assert.True(delete.Succeeded);

        var list = await catalog.ListAsync(UserId, TenantId, null, null, "expert", "all", default);
        var items = Assert.IsAssignableFrom<IReadOnlyList<ExpertCatalogItemView>>(list.Data);
        Assert.DoesNotContain(items, x => x.Id == 11);

        var run = await runs.CreateAsync(UserId, TenantId, new AgentRunCreateRequest("expert", 11, """{"messages":[{"role":"user","content":"你好"}]}""", null, null), default);
        Assert.Equal(404, run.StatusCode);

        var prepared = await conversations.PrepareMessageAsync(UserId, TenantId, 1, "你好", default);
        Assert.Equal(404, prepared.StatusCode);
    }

    private static ExpertCreateRequest Request(string? outputSchemaJson = null) =>
        new() { Name = "我的专家", Category = "test", Persona = "人设", PromptTemplate = "模板", OutputSchemaJson = outputSchemaJson };

    private static HomeMindDbContext NewDb(string name) =>
        new(new DbContextOptionsBuilder<HomeMindDbContext>()
            .UseInMemoryDatabase($"hm-b21-self-serve-{name}-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private static void SeedBase(HomeMindDbContext db)
    {
        db.Tenants.Add(new Tenant { Id = TenantId, TenantType = "personal", Code = "t1", Name = "家庭1", Status = "active", OwnerUserId = UserId, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        db.Users.Add(new User { Id = UserId, DisplayName = "用户1", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
    }

    private sealed class FakeAuditLogger : IFamilyAuditLogger
    {
        public Task<bool> LogAsync(long homeId, long? actorUserId, string action, string targetType, long? targetId, object? before, object? after, string? reason, long? relatedRunId, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }
}
