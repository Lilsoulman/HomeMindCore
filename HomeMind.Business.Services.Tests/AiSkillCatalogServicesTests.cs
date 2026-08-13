using HomeMind.Business.Services.AI;
using HomeMind.Common.Model.Entities;
using HomeMind.Common.Model.ViewModel.Data.AI;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HomeMind.Business.Services.Tests;

/// <summary>B34 Skill 目录 scope 定向测试：默认 mine、平台目录、聚合脱敏与角色边界。</summary>
public sealed class AiSkillCatalogServicesTests
{
    /// <summary>mine 仅返回当前用户自建技能，保留 Prompt 的既有行为不变。</summary>
    [Fact]
    public async Task List_Mine_Remains_Current_User_View()
    {
        await using var db = NewDb("mine");
        SeedUserSkill(db, 1, 10, "我的技能", "我的提示词");
        SeedUserSkill(db, 2, 11, "他人技能", "不应返回");
        var services = new AiSkillServices(db);

        var result = await services.ListAsync(10, 1);

        Assert.Equal(200, result.StatusCode);
        var item = Assert.Single(Assert.IsAssignableFrom<IEnumerable<object>>(result.Data));
        Assert.Contains("我的提示词", item.ToString());
    }

    /// <summary>platform 仅返回启用的平台目录，owner/admin 可访问，member/viewer 被拒绝。</summary>
    [Fact]
    public async Task ListPlatform_Enforces_Role_And_Hides_Inactive()
    {
        await using var db = NewDb("platform");
        SeedPlatformSkill(db, "mindmap", "active");
        SeedPlatformSkill(db, "disabled", "inactive");
        var services = new AiSkillServices(db);

        var owner = await services.ListPlatformAsync(1, "owner");
        var admin = await services.ListPlatformAsync(1, "admin");
        var member = await services.ListPlatformAsync(1, "member");
        var viewer = await services.ListPlatformAsync(1, "viewer");

        Assert.Equal(200, owner.StatusCode);
        Assert.Equal(200, admin.StatusCode);
        Assert.Equal(403, member.StatusCode);
        Assert.Equal(403, viewer.StatusCode);
        var item = Assert.Single(Assert.IsAssignableFrom<IEnumerable<PlatformSkillView>>(owner.Data));
        Assert.Equal("mindmap", item.Key);
    }

    /// <summary>all 返回平台目录和当前租户活跃成员的用户 Skill 摘要，绝不包含 Prompt。</summary>
    [Fact]
    public async Task ListAll_Returns_Desensitized_Member_Summaries()
    {
        await using var db = NewDb("all");
        SeedPlatformSkill(db, "mindmap", "active");
        SeedUser(db, 10, "甲成员");
        SeedUser(db, 11, "乙成员");
        SeedUser(db, 12, "停用成员");
        SeedMember(db, 1, 10, "owner", "active");
        SeedMember(db, 1, 11, "member", "active");
        SeedMember(db, 1, 12, "member", "disabled");
        SeedUserSkill(db, 1, 10, "甲的技能", "敏感提示 A");
        SeedUserSkill(db, 2, 11, "乙的技能", "敏感提示 B");
        SeedUserSkill(db, 3, 12, "停用成员技能", "敏感提示 C");
        SeedUserSkill(db, 4, 20, "跨租户技能", "敏感提示 D", 2);
        var services = new AiSkillServices(db);

        var result = await services.ListAllAsync(1, "admin");

        Assert.Equal(200, result.StatusCode);
        var view = Assert.IsType<AllSkillsView>(result.Data);
        Assert.Single(view.PlatformSkills);
        Assert.Equal(2, view.MemberSkills.Count);
        Assert.All(view.MemberSkills, item => Assert.DoesNotContain("提示", item.ToString()));
        Assert.Contains(view.MemberSkills, item => item.MemberName == "甲成员" && item.Name == "甲的技能");
    }

    private static HomeMindDbContext NewDb(string name) => new(new DbContextOptionsBuilder<HomeMindDbContext>()
        .UseInMemoryDatabase($"hm-b34-{name}-{Guid.NewGuid()}").Options);

    private static void SeedPlatformSkill(HomeMindDbContext db, string key, string status)
    {
        db.SkillCatalogs.Add(new SkillCatalog { TenantId = 1, Key = key, Name = key, Category = "test", InputSchema = "{}", RequiredPermission = "test.read", RiskLevel = "L1", Status = status, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        db.SaveChanges();
    }

    private static void SeedUser(HomeMindDbContext db, long id, string displayName)
    {
        db.Users.Add(new User { Id = id, DisplayName = displayName, Status = "active", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        db.SaveChanges();
    }

    private static void SeedMember(HomeMindDbContext db, long tenantId, long userId, string role, string status)
    {
        db.TenantMembers.Add(new TenantMember { TenantId = tenantId, UserId = userId, Role = role, Status = status, JoinedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        db.SaveChanges();
    }

    private static void SeedUserSkill(HomeMindDbContext db, long id, long userId, string name, string prompt, long tenantId = 1)
    {
        db.AiSkills.Add(new AiSkill { Id = id, TenantId = tenantId, UserId = userId, Name = name, Prompt = prompt, Scopes = "[]", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        db.SaveChanges();
    }
}
