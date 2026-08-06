using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HomeMind.Business.IServices.Family;
using HomeMind.Business.IServices.Productivity;
using HomeMind.Business.IServices.SmartHome;
using HomeMind.Business.Services.Family;
using HomeMind.Business.Services.Life;
using HomeMind.Business.Services.SmartHome;
using HomeMind.Common.Infrastructure;
using HomeMind.Common.Model.Entities;
using HomeMind.Common.Model.Entities.Family;
using HomeMind.Common.Model.Entities.SmartHome;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.Connectors;
using HomeMind.Common.Model.ViewModel.Data.Life;
using HomeMind.Common.Model.ViewModel.Data.Productivity;
using HomeMind.Common.Model.ViewModel.Data.SmartHome;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HomeMind.Business.Services.Tests;

/// <summary>
/// 连接器个人授权服务定向测试：会话单次使用与过期、state 哈希与凭据不落明文、
/// 回调落库与审计、跨租户/跨成员 404 隔离、撤销幂等，以及 Run 权限快照写入与确认复验。
/// </summary>
public class ConnectorAuthorizationServicesTests
{
    private const string AllowedRedirectUri = "https://app.example.com/callback";
    private const string AllowedRedirectUri2 = "https://app.example.com/callback2";

    /// <summary>发起授权返回跳转地址，state 仅以哈希落库，PKCE 校验器仅以密文引用保存。</summary>
    [Fact]
    public async Task Start_Creates_Session_With_Hashed_State_And_Safe_References()
    {
        await using var db = NewDb("auth-start");
        var audit = new FakeAuditLogger();
        SeedProvider(db);
        var services = CreateServices(db, audit);

        var result = await services.StartAuthorizationAsync(userId: 10, tenantId: 1, providerCode: "mock_oauth",
            new StartAuthorizationRequest { RedirectUri = AllowedRedirectUri }, default);

        Assert.Equal(201, result.StatusCode);
        var view = ReadData<AuthorizationSessionView>(result);
        Assert.NotNull(view.AuthorizationUrl);
        var state = ExtractQueryValue(view.AuthorizationUrl!, "state");
        var session = db.ConnectorAuthorizationSessions.Single();
        Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(state))), session.StateHash);
        Assert.NotEqual(state, session.StateHash);
        Assert.StartsWith("enc:", session.PkceVerifierRef);
        Assert.NotEqual("", session.PkceVerifierRef);
        Assert.Equal(ConnectorAuthorizationSessionStatus.Pending, session.Status);
        Assert.Equal(FamilyAuditActions.ConnectorAuthorizeStarted, audit.LastAction);
        Assert.Equal(FamilyAuditTargetTypes.ConnectorAuthorization, audit.LastTargetType);
    }

    /// <summary>回调跳转地址不在 Provider 预注册白名单内时拒绝发起。</summary>
    [Fact]
    public async Task Start_Rejects_Redirect_Uri_Outside_Whitelist()
    {
        await using var db = NewDb("auth-whitelist");
        SeedProvider(db);
        var services = CreateServices(db, new FakeAuditLogger());

        var result = await services.StartAuthorizationAsync(10, 1, "mock_oauth", new StartAuthorizationRequest { RedirectUri = "https://evil.example.com/cb" }, default);

        Assert.Equal(422, result.StatusCode);
        Assert.Empty(db.ConnectorAuthorizationSessions);
    }

    /// <summary>Secret Vault 不可用时发起授权返回 503，不创建会话。</summary>
    [Fact]
    public async Task Start_Returns_503_When_Vault_Unavailable()
    {
        await using var db = NewDb("auth-vault-down");
        SeedProvider(db);
        var services = CreateServices(db, new FakeAuditLogger(), vaultAvailable: false);

        var result = await services.StartAuthorizationAsync(10, 1, "mock_oauth", new StartAuthorizationRequest { RedirectUri = AllowedRedirectUri }, default);

        Assert.Equal(503, result.StatusCode);
        Assert.Empty(db.ConnectorAuthorizationSessions);
    }

    /// <summary>同一 state 二次回调必须拒绝（会话单次使用）。</summary>
    [Fact]
    public async Task Callback_Rejects_Replay_Of_Same_State()
    {
        await using var db = NewDb("auth-replay");
        var audit = new FakeAuditLogger();
        SeedProvider(db);
        var services = CreateServices(db, audit);

        var start = await services.StartAuthorizationAsync(10, 1, "mock_oauth", new StartAuthorizationRequest { RedirectUri = AllowedRedirectUri }, default);
        var state = ExtractQueryValue(ReadData<AuthorizationSessionView>(start).AuthorizationUrl!, "state");

        var first = await services.HandleCallbackAsync("mock_oauth", state, "mock-code", default);
        var second = await services.HandleCallbackAsync("mock_oauth", state, "mock-code", default);

        Assert.Equal(302, first.StatusCode);
        Assert.Equal(400, second.StatusCode);
        Assert.Equal(ConnectorAuthorizationSessionStatus.Completed, db.ConnectorAuthorizationSessions.Single().Status);
    }

    /// <summary>过期会话回调拒绝，并置为 expired 终态。</summary>
    [Fact]
    public async Task Callback_Rejects_Expired_Session()
    {
        await using var db = NewDb("auth-expired");
        SeedProvider(db);
        SeedSession(db, id: 1, userId: 10, expiresAt: DateTime.UtcNow.AddMinutes(-1));
        var services = CreateServices(db, new FakeAuditLogger());

        var result = await services.HandleCallbackAsync("mock_oauth", "expired-state", "mock-code", default);

        Assert.Equal(400, result.StatusCode);
        Assert.Equal(ConnectorAuthorizationSessionStatus.Expired, db.ConnectorAuthorizationSessions.Single().Status);
    }

    /// <summary>回调完成后创建 owner 为发起人的 personal 实例，凭据仅以 vault 引用落库并写审计。</summary>
    [Fact]
    public async Task Callback_Completes_And_Creates_Personal_Connector()
    {
        await using var db = NewDb("auth-complete");
        var audit = new FakeAuditLogger();
        SeedProvider(db);
        var services = CreateServices(db, audit);

        var start = await services.StartAuthorizationAsync(10, 1, "mock_oauth", new StartAuthorizationRequest { RedirectUri = AllowedRedirectUri }, default);
        var state = ExtractQueryValue(ReadData<AuthorizationSessionView>(start).AuthorizationUrl!, "state");
        var result = await services.HandleCallbackAsync("mock_oauth", state, "mock-code", default);

        Assert.Equal(302, result.StatusCode);
        Assert.Equal(AllowedRedirectUri, ReadData<AuthorizationSessionView>(result).RedirectUri);
        var connector = db.WorkspaceConnectors.Single();
        Assert.Equal("personal", connector.BindingScope);
        Assert.Equal(10, connector.OwnerUserId);
        Assert.Equal("connected", connector.Status);
        Assert.Equal(WorkspaceConnectorAuthStatus.Connected, connector.AuthStatus);
        Assert.StartsWith("vault://tenants/1/", connector.CredentialRef);
        var session = db.ConnectorAuthorizationSessions.Single();
        Assert.Equal(ConnectorAuthorizationSessionStatus.Completed, session.Status);
        Assert.NotNull(session.CompletedAt);
        Assert.Equal(FamilyAuditActions.ConnectorAuthorizeCompleted, audit.LastAction);
    }

    /// <summary>会话状态仅本人可查；同租户他人与跨租户访问统一 404。</summary>
    [Fact]
    public async Task GetStatus_Returns_404_For_Non_Owner_Or_Cross_Tenant()
    {
        await using var db = NewDb("auth-status-404");
        SeedProvider(db);
        SeedSession(db, id: 1, userId: 10);
        var services = CreateServices(db, new FakeAuditLogger());

        var owner = await services.GetAuthorizationStatusAsync(10, 1, 1, default);
        var otherMember = await services.GetAuthorizationStatusAsync(20, 1, 1, default);
        var crossTenant = await services.GetAuthorizationStatusAsync(10, 2, 1, default);

        Assert.Equal(200, owner.StatusCode);
        Assert.Equal(404, otherMember.StatusCode);
        Assert.Equal(404, crossTenant.StatusCode);
    }

    /// <summary>个人实例仅向所有者本人返回；owner/admin 亦不可见他人个人实例，且本人可见 IsCurrentUserOwner。</summary>
    [Fact]
    public async Task List_Connectors_Hides_Others_Personal_Instance()
    {
        await using var db = NewDb("auth-list-isolation");
        SeedProvider(db);
        SeedConnector(db, id: 1, scope: "household", ownerUserId: null);
        SeedConnector(db, id: 2, scope: "personal", ownerUserId: 10);
        var services = new ConnectorServices(db, new FakeSecretReferenceValidator(true));

        var other = await services.ListConnectorsAsync(userId: 20, tenantId: 1, canManage: true, default);
        var self = await services.ListConnectorsAsync(userId: 10, tenantId: 1, canManage: true, default);

        Assert.Contains(ReadData<WorkspaceConnectorView[]>(other), x => x.Id == 1);
        Assert.DoesNotContain(ReadData<WorkspaceConnectorView[]>(other), x => x.Id == 2);
        var selfViews = ReadData<WorkspaceConnectorView[]>(self);
        Assert.Contains(selfViews, x => x.Id == 2 && x.IsCurrentUserOwner && x.BindingScope == "personal");
        Assert.Contains(selfViews, x => x.Id == 1 && !x.IsCurrentUserOwner && x.BindingScope == "household");
    }

    /// <summary>撤销使实例凭据可用性失效并写审计；重复撤销幂等返回既有结果。</summary>
    [Fact]
    public async Task Revoke_Revokes_Connector_And_Is_Idempotent()
    {
        await using var db = NewDb("auth-revoke");
        var audit = new FakeAuditLogger();
        SeedProvider(db);
        var services = CreateServices(db, audit);
        var start = await services.StartAuthorizationAsync(10, 1, "mock_oauth", new StartAuthorizationRequest { RedirectUri = AllowedRedirectUri }, default);
        var state = ExtractQueryValue(ReadData<AuthorizationSessionView>(start).AuthorizationUrl!, "state");
        await services.HandleCallbackAsync("mock_oauth", state, "mock-code", default);
        var sessionId = db.ConnectorAuthorizationSessions.Single().Id;

        var first = await services.RevokeAuthorizationAsync(10, 1, sessionId, default);
        var second = await services.RevokeAuthorizationAsync(10, 1, sessionId, default);

        Assert.Equal(200, first.StatusCode);
        Assert.Equal(200, second.StatusCode);
        var connector = db.WorkspaceConnectors.Single();
        Assert.Equal(WorkspaceConnectorAuthStatus.Revoked, connector.AuthStatus);
        Assert.Equal("disconnected", connector.Status);
        Assert.Equal(ConnectorAuthorizationSessionStatus.Revoked, db.ConnectorAuthorizationSessions.Single().Status);
        Assert.Equal(FamilyAuditActions.ConnectorAuthorizeRevoked, audit.LastAction);
    }

    /// <summary>非本人撤销会话返回 404。</summary>
    [Fact]
    public async Task Revoke_Returns_404_For_Non_Owner()
    {
        await using var db = NewDb("auth-revoke-404");
        SeedProvider(db);
        SeedSession(db, id: 1, userId: 10);
        var services = CreateServices(db, new FakeAuditLogger());

        var result = await services.RevokeAuthorizationAsync(20, 1, 1, default);

        Assert.Equal(404, result.StatusCode);
    }

    /// <summary>Run 创建时写入权限快照（scope 与 owner）。</summary>
    [Fact]
    public async Task Run_Create_Writes_Scope_Owner_Snapshot()
    {
        await using var db = NewDb("run-snapshot-create");
        SeedLifeExpert(db);
        var services = new LifeExpertRunServices(db, new FakeCalendarServices());

        var result = await services.CreateAsync(10, 1,
            new LifeExpertRunRequest("recommend", """{"taste":"火锅"}""", Guid.NewGuid().ToString()), default);

        Assert.Equal(201, result.StatusCode);
        var snapshot = db.AgentRuns.Single().PermissionSnapshot!;
        Assert.Contains("\"bindingScope\":\"household\"", snapshot);
        Assert.Contains("\"ownerUserId\":10", snapshot);
    }

    /// <summary>personal 快照的运行动作仅快照所有者可确认，他人确认返回 403。</summary>
    [Fact]
    public async Task Run_Confirm_Rejects_Non_Owner_For_Personal_Snapshot()
    {
        await using var db = NewDb("run-snapshot-personal");
        SeedLifeExpert(db);
        var runId = SeedRun(db, permissionSnapshot: """{"bindingScope":"personal","ownerUserId":10,"connectorGrants":[]}""");
        SeedAction(db, runId, userId: 20);
        var services = new LifeExpertRunServices(db, new FakeCalendarServices());

        var result = await services.ConfirmActionAsync(20, 1, runId, actionId: 1,
            new ConfirmLifeExpertActionRequest(Guid.NewGuid().ToString()), default);

        Assert.Equal(403, result.StatusCode);
        Assert.Equal("pending", db.ExpertRunActions.Single().Status);
    }

    /// <summary>household 快照的连接器授权失效时确认拒绝；授权恢复后确认通过并执行。</summary>
    [Fact]
    public async Task Run_Confirm_Rejects_When_Household_Grant_Invalid()
    {
        await using var db = NewDb("run-snapshot-grant");
        SeedLifeExpert(db);
        SeedConnector(db, id: 5, scope: "household", ownerUserId: null);
        var runId = SeedRun(db, permissionSnapshot: """{"bindingScope":"household","ownerUserId":10,"connectorGrants":[{"connectorId":5}]}""");
        SeedAction(db, runId, userId: 20);
        var services = new LifeExpertRunServices(db, new FakeCalendarServices());

        var denied = await services.ConfirmActionAsync(20, 1, runId, actionId: 1,
            new ConfirmLifeExpertActionRequest(Guid.NewGuid().ToString()), default);
        Assert.Equal(403, denied.StatusCode);

        db.UserConnectorAuthorizations.Add(new UserConnectorAuthorization
        {
            TenantId = 1,
            UserId = 20,
            WorkspaceConnectorId = 5,
            Scope = """["calendar.read"]""",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var granted = await services.ConfirmActionAsync(20, 1, runId, actionId: 1,
            new ConfirmLifeExpertActionRequest(Guid.NewGuid().ToString()), default);

        Assert.Equal(200, granted.StatusCode);
        Assert.Equal("executed", db.ExpertRunActions.Single().Status);
    }

    private static ConnectorAuthorizationServices CreateServices(HomeMindDbContext db, FakeAuditLogger audit, bool vaultAvailable = true) =>
        new(db, new FakeSecretReferenceValidator(vaultAvailable), audit, new SecretProtector(BuildConfig()), BuildConfig());

    private static HomeMindDbContext NewDb(string name) =>
        new(new DbContextOptionsBuilder<HomeMindDbContext>()
            .UseInMemoryDatabase($"hm-b18-connector-auth-{name}-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private static IConfiguration BuildConfig() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:SigningKey"] = "unit-test-signing-key-32bytes-minimum-aaaa",
                ["ConnectorOAuth:AllowedRedirectUris"] = $"{AllowedRedirectUri},{AllowedRedirectUri2}",
                ["SecretVault:Enabled"] = "true"
            })
            .Build();

    private static void SeedProvider(HomeMindDbContext db)
    {
        db.ConnectorProviders.Add(new ConnectorProvider
        {
            Id = 1,
            Code = "mock_oauth",
            Name = "Mock OAuth",
            Provider = "mock_oauth",
            ConnectorType = "calendar",
            Status = "active",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        db.SaveChanges();
    }

    private static void SeedSession(HomeMindDbContext db, long id, long userId, DateTime? expiresAt = null)
    {
        db.ConnectorAuthorizationSessions.Add(new ConnectorAuthorizationSession
        {
            Id = id,
            TenantId = 1,
            ConnectorProviderId = 1,
            BindingScope = "personal",
            InitiatorUserId = userId,
            StateHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("expired-state"))),
            PkceVerifierRef = "enc:placeholder",
            RedirectUri = AllowedRedirectUri,
            Status = ConnectorAuthorizationSessionStatus.Pending,
            ExpiresAt = expiresAt ?? DateTime.UtcNow.AddMinutes(10),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        db.SaveChanges();
    }

    private static void SeedConnector(HomeMindDbContext db, long id, string scope, long? ownerUserId)
    {
        db.WorkspaceConnectors.Add(new WorkspaceConnector
        {
            Id = id,
            TenantId = 1,
            ConnectorProviderId = 1,
            BindingScope = scope,
            OwnerUserId = ownerUserId,
            Name = $"连接器{id}",
            Status = "connected",
            AuthStatus = scope == "personal" ? WorkspaceConnectorAuthStatus.Connected : "none",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        db.SaveChanges();
    }

    private static void SeedLifeExpert(HomeMindDbContext db)
    {
        db.Experts.Add(new HomeMind.Common.Model.Entities.Expert { Id = 1, TenantId = 1, Code = "personal-life-expert", Name = "个人生活专家", Category = "life", Status = "active" });
        db.ExpertVersions.Add(new ExpertVersion
        {
            Id = 1,
            TenantId = 1,
            ExpertId = 1,
            Version = 1,
            Status = "published",
            Persona = "个人生活专家",
            Methodology = "确定性规则",
            PromptTemplate = "personal-life-expert prompt",
            EstimatedCredits = 1
        });
        db.SaveChanges();
    }

    private static long SeedRun(HomeMindDbContext db, string permissionSnapshot)
    {
        var run = new AgentRun
        {
            TenantId = 1,
            UserId = 10,
            SourceType = "expert",
            ExpertVersionId = 1,
            RequestIdempotencyKey = Guid.NewGuid().ToString(),
            Input = "{}",
            Status = "pending_actions",
            Mode = "steward",
            AutoConfirmPolicy = "L3_only",
            PermissionSnapshot = permissionSnapshot,
            CreatedAt = DateTime.UtcNow
        };
        db.AgentRuns.Add(run);
        db.SaveChanges();
        return run.Id;
    }

    private static void SeedAction(HomeMindDbContext db, long runId, long userId)
    {
        db.ExpertRunActions.Add(new ExpertRunAction
        {
            Id = 1,
            RunId = runId,
            TenantId = 1,
            UserId = userId,
            ActionType = "calendar_create_event",
            RequestIdempotencyKey = Guid.NewGuid().ToString(),
            RequestJson = """{"destination":"杭州","days":[{"day":1,"weather":"晴","activities":[{"timeSlot":"上午","name":"西湖","note":"游览"}]}]}""",
            Status = "pending",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        db.SaveChanges();
    }

    private static T ReadData<T>(ServiceResult result) =>
        System.Text.Json.JsonSerializer.Deserialize<T>(System.Text.Json.JsonSerializer.Serialize(result.Data))!;

    private static string ExtractQueryValue(string url, string key)
    {
        var query = url.Contains('?') ? url[(url.IndexOf('?') + 1)..] : url;
        return query.Split('&').Select(x => x.Split('=')).First(x => x[0] == key)[1];
    }

    private sealed class FakeSecretReferenceValidator(bool vaultAvailable) : IConnectorSecretReferenceValidator
    {
        public Task<ConnectorSecretReferenceValidation> ValidateAsync(long tenantId, string credentialRef, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ConnectorSecretReferenceValidation(true, vaultAvailable, vaultAvailable ? "凭据引用有效。" : "Secret Vault 未配置或暂时不可用。"));
    }

    private sealed class FakeAuditLogger : IFamilyAuditLogger
    {
        public string? LastAction { get; private set; }
        public string? LastTargetType { get; private set; }

        public Task<bool> LogAsync(long homeId, long? actorUserId, string action, string targetType, long? targetId, object? before, object? after, string? reason, long? relatedRunId, CancellationToken cancellationToken = default)
        {
            LastAction = action;
            LastTargetType = targetType;
            return Task.FromResult(true);
        }
    }

    private sealed class FakeCalendarServices : ICalendarServices
    {
        public Task<ServiceResult> CreateEventAsync(long userId, long tenantId, CalendarEventRequest request, CancellationToken token = default) =>
            Task.FromResult(new ServiceResult(200, "ok", null));

        public Task<ServiceResult> ListEventsAsync(long userId, long tenantId, DateTime? from, DateTime? to, CancellationToken token = default) => throw new NotImplementedException();
        public Task<ServiceResult> UpdateEventAsync(long userId, long tenantId, long id, CalendarEventRequest request, CancellationToken token = default) => throw new NotImplementedException();
        public Task<ServiceResult> DeleteEventAsync(long userId, long tenantId, long id, CancellationToken token = default) => throw new NotImplementedException();
        public Task<ServiceResult> ListSubscriptionsAsync(long userId, long tenantId, CancellationToken token = default) => throw new NotImplementedException();
        public Task<ServiceResult> CreateSubscriptionAsync(long userId, long tenantId, SubscriptionRequest request, CancellationToken token = default) => throw new NotImplementedException();
        public Task<ServiceResult> UpdateSubscriptionAsync(long userId, long tenantId, long id, SubscriptionRequest request, CancellationToken token = default) => throw new NotImplementedException();
        public Task<ServiceResult> DeleteSubscriptionAsync(long userId, long tenantId, long id, CancellationToken token = default) => throw new NotImplementedException();
    }
}
