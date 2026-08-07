using HomeMind.Business.IServices.SmartHome;
using HomeMind.Business.Services.SmartHome;
using HomeMind.Common.Model.Entities.SmartHome;
using HomeMind.Common.Model.ViewModel.Data.Connectors;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HomeMind.Business.Services.Tests;

/// <summary>"我的个人连接"汇总服务定向测试：仅返回本人 personal 实例，并携带最近一次授权会话状态。</summary>
public class PersonalConnectionSummaryTests
{
    /// <summary>仅返回当前用户作为 owner 的 personal 实例。</summary>
    [Fact]
    public async Task List_Returns_Only_Owners_Personal_Connections()
    {
        await using var db = NewDb("summary-mine");
        var provider = new ConnectorProvider { Id = 1, Code = "mock_oauth", Name = "Mock OAuth", Provider = "mock", ConnectorType = "calendar", Status = "active", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        db.ConnectorProviders.Add(provider);
        db.WorkspaceConnectors.AddRange(
            new WorkspaceConnector { Id = 1, TenantId = 1, ConnectorProviderId = 1, BindingScope = "personal", OwnerUserId = 1, Name = "我的日历", Status = "connected", AuthStatus = "connected", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new WorkspaceConnector { Id = 2, TenantId = 1, ConnectorProviderId = 1, BindingScope = "personal", OwnerUserId = 2, Name = "他人日历", Status = "disconnected", AuthStatus = "revoked", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new WorkspaceConnector { Id = 3, TenantId = 1, ConnectorProviderId = 1, BindingScope = "household", OwnerUserId = null, Name = "家庭 HA", Status = "connected", AuthStatus = "none", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var services = new ConnectorServices(db, new DummySecretReferenceValidator());

        var result = await services.ListMyPersonalConnectionsAsync(userId: 1, tenantId: 1, default);

        Assert.True(result.Succeeded);
        var items = Assert.IsAssignableFrom<IReadOnlyList<PersonalConnectionSummaryView>>(result.Data);
        Assert.Single(items);
        Assert.Equal(1, items[0].ConnectorId);
    }

    /// <summary>附带最近一次授权会话状态。</summary>
    [Fact]
    public async Task List_Attaches_Latest_Authorization_Session_Status()
    {
        await using var db = NewDb("summary-session");
        var provider = new ConnectorProvider { Id = 1, Code = "mock_oauth", Name = "Mock OAuth", Provider = "mock", ConnectorType = "calendar", Status = "active", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        db.ConnectorProviders.Add(provider);
        db.WorkspaceConnectors.Add(new WorkspaceConnector { Id = 1, TenantId = 1, ConnectorProviderId = 1, BindingScope = "personal", OwnerUserId = 1, Name = "我的日历", Status = "connected", AuthStatus = "connected", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        db.ConnectorAuthorizationSessions.AddRange(
            new ConnectorAuthorizationSession { Id = 1, TenantId = 1, ConnectorProviderId = 1, BindingScope = "personal", InitiatorUserId = 1, StateHash = "old", RedirectUri = "https://app.example.com/cb", Status = "used", ExpiresAt = DateTime.UtcNow.AddDays(-1), CreatedAt = DateTime.UtcNow.AddDays(-2), UpdatedAt = DateTime.UtcNow.AddDays(-1) },
            new ConnectorAuthorizationSession { Id = 2, TenantId = 1, ConnectorProviderId = 1, BindingScope = "personal", InitiatorUserId = 1, StateHash = "new", RedirectUri = "https://app.example.com/cb", Status = "completed", ExpiresAt = DateTime.UtcNow.AddDays(1), CreatedAt = DateTime.UtcNow.AddDays(-1), UpdatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var services = new ConnectorServices(db, new DummySecretReferenceValidator());

        var result = await services.ListMyPersonalConnectionsAsync(userId: 1, tenantId: 1, default);

        var items = Assert.IsAssignableFrom<IReadOnlyList<PersonalConnectionSummaryView>>(result.Data);
        Assert.Equal(2, items[0].LastSessionId);
        Assert.Equal("completed", items[0].LastSessionStatus);
    }

    /// <summary>不返回他人 personal 实例（跨成员隔离）。</summary>
    [Fact]
    public async Task List_Excludes_Others_Personal_Connections()
    {
        await using var db = NewDb("summary-other");
        var provider = new ConnectorProvider { Id = 1, Code = "mock_oauth", Name = "Mock OAuth", Provider = "mock", ConnectorType = "calendar", Status = "active", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        db.ConnectorProviders.Add(provider);
        db.WorkspaceConnectors.Add(new WorkspaceConnector { Id = 1, TenantId = 1, ConnectorProviderId = 1, BindingScope = "personal", OwnerUserId = 2, Name = "他人日历", Status = "connected", AuthStatus = "connected", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var services = new ConnectorServices(db, new DummySecretReferenceValidator());

        var result = await services.ListMyPersonalConnectionsAsync(userId: 1, tenantId: 1, default);

        var items = Assert.IsAssignableFrom<IReadOnlyList<PersonalConnectionSummaryView>>(result.Data);
        Assert.Empty(items);
    }

    private static HomeMindDbContext NewDb(string name) =>
        new(new DbContextOptionsBuilder<HomeMindDbContext>()
            .UseInMemoryDatabase($"hm-b19-personal-{name}-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private sealed class DummySecretReferenceValidator : IConnectorSecretReferenceValidator
    {
        public Task<ConnectorSecretReferenceValidation> ValidateAsync(long tenantId, string credentialRef, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ConnectorSecretReferenceValidation(true, true, "ok"));
    }
}
