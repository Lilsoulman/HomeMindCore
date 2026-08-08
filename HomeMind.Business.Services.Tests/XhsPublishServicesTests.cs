using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HomeMind.Business.IServices.Connector;
using HomeMind.Business.Services.Connectors;
using HomeMind.Business.Services.Family;
using HomeMind.Common.Model.Entities;
using HomeMind.Common.Model.Entities.Family;
using HomeMind.Common.Model.Entities.SmartHome;
using HomeMind.Common.Model.ViewModel.Data.Connectors;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HomeMind.Business.Services.Tests;

/// <summary>
/// 小红书发布服务定向测试：L2 动作创建与参数校验、确认执行与 xhs_note_published 审计、
/// 幂等重放不重复发布、422-404-409 状态码、发布失败 502 与终态。
/// </summary>
public class XhsPublishServicesTests
{
    private static readonly XhsPublishRequest PublishRequest = new()
    {
        Type = "image",
        Title = "周末探店",
        Content = "周末去了这家新开的咖啡馆，环境很棒。",
        MediaPaths = ["C:\\photos\\coffee1.jpg", "C:\\photos\\coffee2.jpg"],
        Tags = ["探店", "咖啡"]
    };

    /// <summary>创建发布动作：201 + pending 动作视图，动作携带 L2 风险等级。</summary>
    [Fact]
    public async Task Create_Returns_Pending_L2_Action()
    {
        await using var db = NewDb("publish-create");
        SeedAuthorizedConnector(db);
        var services = CreateServices(db);

        var result = await services.CreateAsync(10, 1, PublishRequest, default);

        Assert.Equal(201, result.StatusCode);
        var view = ReadData<XhsPublishActionView>(result);
        Assert.Equal("xhs_publish", view.ActionType);
        Assert.Equal("pending", view.Status);
        Assert.Equal("L2", view.RiskLevel);
        var run = db.AgentRuns.Single();
        Assert.Equal("xhs", run.SourceType);
        Assert.Null(run.ExpertVersionId);
        var action = db.ExpertRunActions.Single();
        Assert.Equal("xhs_publish", action.ActionType);
        Assert.Equal("pending", action.Status);
    }

    /// <summary>发布参数非法返回 422：类型、标题、正文、媒体数量。</summary>
    [Fact]
    public async Task Create_Validates_Request_Parameters()
    {
        await using var db = NewDb("publish-validate");
        SeedAuthorizedConnector(db);
        var services = CreateServices(db);

        var badType = await services.CreateAsync(10, 1, Clone(type: "video2"), default);
        Assert.Equal(422, badType.StatusCode);
        var badTitle = await services.CreateAsync(10, 1, Clone(title: ""), default);
        Assert.Equal(422, badTitle.StatusCode);
        var tooManyImages = await services.CreateAsync(10, 1, Clone(mediaPaths: Enumerable.Range(1, 19).Select(i => $"img{i}.jpg").ToArray()), default);
        Assert.Equal(422, tooManyImages.StatusCode);
        var videoMulti = await services.CreateAsync(10, 1, Clone(type: "video", mediaPaths: ["a.mp4", "b.mp4"]), default);
        Assert.Equal(422, videoMulti.StatusCode);
        Assert.Empty(db.AgentRuns);
    }

    /// <summary>未授权（无连接器）创建发布返回 404。</summary>
    [Fact]
    public async Task Create_Without_Authorized_Connector_Returns_404()
    {
        await using var db = NewDb("publish-404");
        var services = CreateServices(db);

        var result = await services.CreateAsync(10, 1, PublishRequest, default);

        Assert.Equal(404, result.StatusCode);
    }

    /// <summary>确认发布成功：action executed / run completed，写 xhs_note_published 审计（目标 xhs_note）。</summary>
    [Fact]
    public async Task Confirm_Publishes_And_Writes_Audit()
    {
        await using var db = NewDb("publish-confirm");
        SeedAuthorizedConnector(db);
        var xhs = new FakeXhsClient();
        var services = CreateServices(db, xhs);
        var created = await services.CreateAsync(10, 1, PublishRequest, default);
        var actionId = ReadData<XhsPublishActionView>(created).ActionId;

        var result = await services.ConfirmActionAsync(10, 1, actionId, new XhsPublishConfirmRequest { IdempotencyKey = Guid.NewGuid().ToString() }, default);

        Assert.Equal(200, result.StatusCode);
        Assert.Equal(1, xhs.PublishCalls);
        var run = db.AgentRuns.Single();
        Assert.Equal("completed", run.Status);
        var action = db.ExpertRunActions.Single();
        Assert.Equal("executed", action.Status);
        Assert.Equal(1, await db.FamilyAuditLogs.CountAsync(x => x.Action == FamilyAuditActions.XhsNotePublished));
        var audit = await db.FamilyAuditLogs.SingleAsync(x => x.Action == FamilyAuditActions.XhsNotePublished);
        Assert.Equal(FamilyAuditTargetTypes.XhsNote, audit.TargetType);
        Assert.Equal(run.Id, audit.RelatedRunId);
    }

    /// <summary>同一幂等键重复确认重放首次结果，不重复发布。</summary>
    [Fact]
    public async Task Confirm_Same_Idempotency_Key_Replays_Without_Republishing()
    {
        await using var db = NewDb("publish-idempotent");
        SeedAuthorizedConnector(db);
        var xhs = new FakeXhsClient();
        var services = CreateServices(db, xhs);
        var created = await services.CreateAsync(10, 1, PublishRequest, default);
        var actionId = ReadData<XhsPublishActionView>(created).ActionId;
        var key = Guid.NewGuid().ToString();

        var first = await services.ConfirmActionAsync(10, 1, actionId, new XhsPublishConfirmRequest { IdempotencyKey = key }, default);
        var second = await services.ConfirmActionAsync(10, 1, actionId, new XhsPublishConfirmRequest { IdempotencyKey = key }, default);

        Assert.Equal(200, first.StatusCode);
        Assert.Equal(200, second.StatusCode);
        Assert.Equal(1, xhs.PublishCalls);
        Assert.Equal(1, await db.FamilyAuditLogs.CountAsync(x => x.Action == FamilyAuditActions.XhsNotePublished));
    }

    /// <summary>非法幂等键 422；动作不存在或非本人 404；已终态换键 409。</summary>
    [Fact]
    public async Task Confirm_Returns_422_404_409()
    {
        await using var db = NewDb("publish-codes");
        SeedAuthorizedConnector(db);
        var services = CreateServices(db);
        var created = await services.CreateAsync(10, 1, PublishRequest, default);
        var actionId = ReadData<XhsPublishActionView>(created).ActionId;
        var key = Guid.NewGuid().ToString();

        var badKey = await services.ConfirmActionAsync(10, 1, actionId, new XhsPublishConfirmRequest { IdempotencyKey = "not-a-uuid" }, default);
        Assert.Equal(422, badKey.StatusCode);

        var missing = await services.ConfirmActionAsync(10, 1, 9999, new XhsPublishConfirmRequest { IdempotencyKey = Guid.NewGuid().ToString() }, default);
        Assert.Equal(404, missing.StatusCode);

        await services.ConfirmActionAsync(10, 1, actionId, new XhsPublishConfirmRequest { IdempotencyKey = key }, default);
        var afterTerminal = await services.ConfirmActionAsync(10, 1, actionId, new XhsPublishConfirmRequest { IdempotencyKey = Guid.NewGuid().ToString() }, default);
        Assert.Equal(409, afterTerminal.StatusCode);
    }

    /// <summary>发布失败返回 502，action/run 终态 failed。</summary>
    [Fact]
    public async Task Confirm_Publish_Failure_Returns_502_And_Terminal_Failed()
    {
        await using var db = NewDb("publish-fail");
        SeedAuthorizedConnector(db);
        var services = CreateServices(db, new FakeXhsClient(publishFails: true));
        var created = await services.CreateAsync(10, 1, PublishRequest, default);
        var actionId = ReadData<XhsPublishActionView>(created).ActionId;

        var result = await services.ConfirmActionAsync(10, 1, actionId, new XhsPublishConfirmRequest { IdempotencyKey = Guid.NewGuid().ToString() }, default);

        Assert.Equal(502, result.StatusCode);
        Assert.Equal("failed", db.AgentRuns.Single().Status);
        Assert.Equal("failed", db.ExpertRunActions.Single().Status);
        Assert.Equal(0, await db.FamilyAuditLogs.CountAsync(x => x.Action == FamilyAuditActions.XhsNotePublished));
    }

    /// <summary>同幂等键已用于其他运行类型返回 409。</summary>
    [Fact]
    public async Task Create_Same_Key_For_Other_Run_Type_Returns_409()
    {
        await using var db = NewDb("publish-key-conflict");
        SeedAuthorizedConnector(db);
        var services = CreateServices(db);
        var key = Guid.NewGuid().ToString();
        db.AgentRuns.Add(new AgentRun
        {
            TenantId = 1,
            UserId = 10,
            SourceType = "skill",
            RequestIdempotencyKey = key,
            Input = "{}",
            Status = "planning",
            Mode = "steward",
            AutoConfirmPolicy = "L3_only",
            CreatedAt = DateTime.UtcNow
        });
        db.SaveChanges();

        var result = await services.CreateAsync(10, 1, CloneWithKey(key), default);

        Assert.Equal(409, result.StatusCode);
    }

    private static XhsPublishServices CreateServices(HomeMindDbContext db, FakeXhsClient? xhs = null) =>
        new(db, new FamilyAuditLogger(db, NullLogger<FamilyAuditLogger>.Instance), xhs ?? new FakeXhsClient());

    /// <summary>复制发布请求并覆盖指定字段（XhsPublishRequest 为 class，测试中避免 with 表达式）。</summary>
    private static XhsPublishRequest Clone(string? type = null, string? title = null, IReadOnlyList<string>? mediaPaths = null) => new()
    {
        Type = type ?? PublishRequest.Type,
        Title = title ?? PublishRequest.Title,
        Content = PublishRequest.Content,
        MediaPaths = mediaPaths ?? PublishRequest.MediaPaths,
        Tags = PublishRequest.Tags
    };

    /// <summary>复制发布请求并携带指定幂等键。</summary>
    private static XhsPublishRequest CloneWithKey(string idempotencyKey) => new()
    {
        IdempotencyKey = idempotencyKey,
        Type = PublishRequest.Type,
        Title = PublishRequest.Title,
        Content = PublishRequest.Content,
        MediaPaths = PublishRequest.MediaPaths,
        Tags = PublishRequest.Tags
    };

    private static HomeMindDbContext NewDb(string name) =>
        new(new DbContextOptionsBuilder<HomeMindDbContext>()
            .UseInMemoryDatabase($"hm-b27-xhs-publish-{name}-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private static void SeedAuthorizedConnector(HomeMindDbContext db)
    {
        db.ConnectorProviders.Add(new ConnectorProvider
        {
            Id = 1,
            Code = "xhs",
            Name = "小红书",
            Provider = "xhs_mcp",
            ConnectorType = "social",
            Status = "active",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        db.WorkspaceConnectors.Add(new WorkspaceConnector
        {
            Id = 1,
            TenantId = 1,
            ConnectorProviderId = 1,
            BindingScope = "personal",
            OwnerUserId = 10,
            Name = "小红书",
            CredentialRef = "local://xhs-sessions/1",
            Status = "connected",
            AuthStatus = WorkspaceConnectorAuthStatus.Connected,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        db.SaveChanges();
    }

    private static T ReadData<T>(HomeMind.Common.Model.ViewModel.Common.ServiceResult result) =>
        System.Text.Json.JsonSerializer.Deserialize<T>(System.Text.Json.JsonSerializer.Serialize(result.Data))!;

    /// <summary>小红书 MCP 的可控测试替身：记录发布调用次数，可按构造参数模拟发布失败。</summary>
    private sealed class FakeXhsClient(bool publishFails = false) : IXhsMcpClient
    {
        public int PublishCalls { get; private set; }

        public Task<XhsAuthStatus> GetAuthStatusAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new XhsAuthStatus(true, "已登录"));

        public Task<XhsLoginHint> TriggerLoginAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task LogoutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<XhsSearchResult> SearchNotesAsync(string query, int limit, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<XhsNoteDetail> GetNoteDetailAsync(string url, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<XhsPublishResult> PublishAsync(XhsPublishInput input, CancellationToken cancellationToken = default)
        {
            PublishCalls++;
            return Task.FromResult(publishFails
                ? new XhsPublishResult(false, "", "模拟发布失败")
                : new XhsPublishResult(true, "published-note-1", "发布成功"));
        }
    }
}
