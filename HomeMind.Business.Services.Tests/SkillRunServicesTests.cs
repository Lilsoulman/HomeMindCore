using System.Text.Json;
using HomeMind.Business.IServices.AI;
using HomeMind.Business.IServices.Expert;
using HomeMind.Business.Services.AI;
using HomeMind.Business.Services.Family;
using HomeMind.Common.Model.Entities;
using HomeMind.Common.Model.Entities.Family;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.AI;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HomeMind.Business.Services.Tests;

/// <summary>
/// Skill 独立执行定向测试：SkillRun 创建（SourceType=skill、不绑定专家）、确定性方案生成
/// （时长提取/单片段）、幂等重放与跨类型幂等冲突、未知 Skill 与非法输入 422、
/// 跨租户/跨用户 404 与 skill_run_created 审计。
/// </summary>
public class SkillRunServicesTests
{
    /// <summary>创建成功：SourceType=skill、ExpertVersionId 为空、单个 draft_generate 动作（L1）、方案承载于 RequestJson 并写审计。</summary>
    [Fact]
    public async Task Create_Succeeds_And_Generates_Draft_Plan()
    {
        await using var db = NewDb("create");
        SeedQuickEdit(db);
        var services = NewServices(db);

        var result = await services.CreateAsync(10, 1, "quick-edit", new SkillRunCreateRequest(null, """{"media_location":"/nas/videos/探店.mp4","instruction":"竖屏 30 秒，加字幕"}"""), default);

        Assert.Equal(201, result.StatusCode);
        var run = await db.AgentRuns.SingleAsync();
        Assert.Equal("skill", run.SourceType);
        Assert.Null(run.ExpertVersionId);
        Assert.Equal("pending_actions", run.Status);

        var action = await db.ExpertRunActions.SingleAsync();
        Assert.Equal("draft_generate", action.ActionType);
        Assert.Equal("pending", action.Status);
        using var plan = JsonDocument.Parse(action.RequestJson);
        Assert.Equal(30, plan.RootElement.GetProperty("total_duration").GetInt32());
        Assert.Equal(1, plan.RootElement.GetProperty("segments").GetArrayLength());
        Assert.Equal("探店.mp4", plan.RootElement.GetProperty("segments")[0].GetProperty("source").GetString());
        Assert.Contains("探店.mp4", run.ResultSummary);

        var audit = await db.FamilyAuditLogs.SingleAsync();
        Assert.Equal(FamilyAuditActions.SkillRunCreated, audit.Action);
        Assert.Equal(FamilyAuditTargetTypes.SkillRun, audit.TargetType);
        Assert.Equal(run.Id, audit.TargetId);
        Assert.Equal(run.Id, audit.RelatedRunId);
    }

    /// <summary>B30：创建后的动作视图输出结构化片段序列（Segments/TotalDuration），供 Web 渲染方案时间线。</summary>
    [Fact]
    public async Task Create_ActionView_Exposes_Structured_Plan()
    {
        await using var db = NewDb("plan-view");
        SeedQuickEdit(db);
        var services = NewServices(db);

        var result = await services.CreateAsync(10, 1, "quick-edit", new SkillRunCreateRequest(null, """{"media_location":"/nas/videos/探店.mp4","instruction":"竖屏 30 秒"}"""), default);

        Assert.True(result.Succeeded);
        var view = Assert.IsType<SkillRunView>(result.Data);
        var action = Assert.Single(view.Actions);
        Assert.NotNull(action.Segments);
        var segment = Assert.Single(action.Segments);
        Assert.Equal(1, segment.Index);
        Assert.Equal("探店.mp4", segment.Source);
        Assert.Equal(30, segment.Duration);
        Assert.Equal(30, action.TotalDuration);
    }

    /// <summary>同一幂等键重复创建返回既有运行，不重复创建。</summary>
    [Fact]
    public async Task Create_Replays_Same_Idempotency_Key()
    {
        await using var db = NewDb("replay");
        SeedQuickEdit(db);
        var services = NewServices(db);
        var key = Guid.NewGuid().ToString();

        var first = await services.CreateAsync(10, 1, "quick-edit", new SkillRunCreateRequest(key, """{"media_location":"/nas/videos/a.mp4"}"""), default);
        var second = await services.CreateAsync(10, 1, "quick-edit", new SkillRunCreateRequest(key, """{"media_location":"/nas/videos/a.mp4"}"""), default);

        Assert.Equal(201, first.StatusCode);
        Assert.Equal(200, second.StatusCode);
        Assert.Equal(1, await db.AgentRuns.CountAsync());
        var firstView = Assert.IsType<SkillRunView>(first.Data);
        Assert.Equal(firstView.Id, Assert.IsType<SkillRunView>(second.Data).Id);
    }

    /// <summary>未知或未启用的 Skill 返回 422。</summary>
    [Fact]
    public async Task Create_Rejects_Unknown_Skill_With_422()
    {
        await using var db = NewDb("unknown-skill");
        SeedQuickEdit(db);
        var services = NewServices(db);

        var result = await services.CreateAsync(10, 1, "unknown", new SkillRunCreateRequest(null, """{"media_location":"/nas/videos/a.mp4"}"""), default);

        Assert.Equal(422, result.StatusCode);
        Assert.Equal(0, await db.AgentRuns.CountAsync());
    }

    /// <summary>缺少 media_location 或非法 JSON 返回 422。</summary>
    [Fact]
    public async Task Create_Rejects_Missing_MediaLocation_And_Invalid_Json()
    {
        await using var db = NewDb("invalid-input");
        SeedQuickEdit(db);
        var services = NewServices(db);

        var missing = await services.CreateAsync(10, 1, "quick-edit", new SkillRunCreateRequest(null, """{"instruction":"30秒"}"""), default);
        var invalid = await services.CreateAsync(10, 1, "quick-edit", new SkillRunCreateRequest(null, "not-json"), default);

        Assert.Equal(422, missing.StatusCode);
        Assert.Equal(422, invalid.StatusCode);
        Assert.Equal(0, await db.AgentRuns.CountAsync());
    }

    /// <summary>从创作指令提取目标时长：N分钟乘以 60；无指令默认 15 秒。</summary>
    [Fact]
    public async Task Create_Parses_Duration_From_Instruction()
    {
        await using var db = NewDb("duration");
        SeedQuickEdit(db);
        var services = NewServices(db);

        var minutes = await services.CreateAsync(10, 1, "quick-edit", new SkillRunCreateRequest(null, """{"media_location":"/nas/videos/a.mp4","instruction":"时长 2 分钟"}"""), default);
        Assert.True(minutes.Succeeded);
        var minutesRun = await db.AgentRuns.SingleAsync();
        using var minutesPlan = JsonDocument.Parse(await db.ExpertRunActions.Where(x => x.RunId == minutesRun.Id).Select(x => x.RequestJson).SingleAsync());
        Assert.Equal(120, minutesPlan.RootElement.GetProperty("total_duration").GetInt32());

        var defaulted = await services.CreateAsync(10, 1, "quick-edit", new SkillRunCreateRequest(null, """{"media_location":"/nas/videos/b.mp4"}"""), default);
        Assert.True(defaulted.Succeeded);
        var defaultRun = await db.AgentRuns.SingleAsync(x => x.Id != minutesRun.Id);
        using var defaultPlan = JsonDocument.Parse(await db.ExpertRunActions.Where(x => x.RunId == defaultRun.Id).Select(x => x.RequestJson).SingleAsync());
        Assert.Equal(15, defaultPlan.RootElement.GetProperty("total_duration").GetInt32());
    }

    /// <summary>跨租户、跨用户或不存在查询一律 404。</summary>
    [Fact]
    public async Task Get_Rejects_Cross_Tenant_And_Other_User_With_404()
    {
        await using var db = NewDb("cross");
        SeedQuickEdit(db);
        var services = NewServices(db);
        var created = await services.CreateAsync(10, 1, "quick-edit", new SkillRunCreateRequest(null, """{"media_location":"/nas/videos/a.mp4"}"""), default);
        var runId = Assert.IsType<SkillRunView>(created.Data).Id;

        var own = await services.GetAsync(10, 1, runId, default);
        var otherUser = await services.GetAsync(11, 1, runId, default);
        var otherTenant = await services.GetAsync(10, 2, runId, default);
        var missing = await services.GetAsync(10, 1, 9999, default);

        Assert.Equal(200, own.StatusCode);
        Assert.Equal(404, otherUser.StatusCode);
        Assert.Equal(404, otherTenant.StatusCode);
        Assert.Equal(404, missing.StatusCode);
    }

    /// <summary>同一幂等键已用于其他运行类型（如 scenario）时返回 409。</summary>
    [Fact]
    public async Task Create_Rejects_Idempotency_Key_Used_By_Other_Run_Type()
    {
        await using var db = NewDb("key-conflict");
        SeedQuickEdit(db);
        var services = NewServices(db);
        var key = Guid.NewGuid().ToString();
        db.AgentRuns.Add(new AgentRun
        {
            TenantId = 1,
            UserId = 10,
            SourceType = "scenario",
            RequestIdempotencyKey = key,
            Input = "{}",
            Status = "completed",
            Mode = "steward",
            AutoConfirmPolicy = "L3_only",
            PermissionSnapshot = "{}",
            EstimatedCredits = 0,
            StartedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var result = await services.CreateAsync(10, 1, "quick-edit", new SkillRunCreateRequest(key, """{"media_location":"/nas/videos/a.mp4"}"""), default);

        Assert.Equal(409, result.StatusCode);
        Assert.Equal(1, await db.AgentRuns.CountAsync());
    }

    /// <summary>确认执行：经剪辑 MCP 生成草稿并登记为生成文件，action executed、run completed、两条审计落库。</summary>
    [Fact]
    public async Task Confirm_Executes_Registers_Draft_And_Audits()
    {
        await using var db = NewDb("confirm-execute");
        SeedQuickEdit(db);
        var files = new FakeExpertFileServices();
        var services = NewServices(db, files);
        var runId = await CreateRunAsync(services, db);
        var actionId = await db.ExpertRunActions.Where(x => x.ActionType == "draft_generate").Select(x => x.Id).SingleAsync();

        var result = await services.ConfirmActionAsync(10, 1, runId, actionId, new ConfirmSkillRunActionRequest(Guid.NewGuid().ToString()), default);

        Assert.Equal(200, result.StatusCode);
        var action = await db.ExpertRunActions.SingleAsync();
        Assert.Equal("executed", action.Status);
        var run = await db.AgentRuns.SingleAsync();
        Assert.Equal("completed", run.Status);
        Assert.Contains("剪映", run.ResultSummary);
        Assert.Equal(1, files.RegisterCalls);
        Assert.Equal($"quick_edit_{runId}.draft.json", files.LastName);
        Assert.Equal("application/json", files.LastMime);
        Assert.Equal(runId, files.LastRunId);
        Assert.NotNull(files.LastContent);
        Assert.True(files.LastContent!.Length > 0);

        Assert.Equal(1, await db.FamilyAuditLogs.CountAsync(x => x.Action == FamilyAuditActions.SkillActionConfirmed));
        Assert.Equal(1, await db.FamilyAuditLogs.CountAsync(x => x.Action == FamilyAuditActions.SkillDraftRegistered));
        var draftAudit = await db.FamilyAuditLogs.SingleAsync(x => x.Action == FamilyAuditActions.SkillDraftRegistered);
        Assert.Equal(FamilyAuditTargetTypes.SkillDraft, draftAudit.TargetType);
        Assert.NotNull(draftAudit.TargetId);
    }

    /// <summary>同一幂等键重复确认重放首次结果，不重复登记草稿文件。</summary>
    [Fact]
    public async Task Confirm_Replays_Same_Idempotency_Key()
    {
        await using var db = NewDb("confirm-replay");
        SeedQuickEdit(db);
        var files = new FakeExpertFileServices();
        var services = NewServices(db, files);
        var runId = await CreateRunAsync(services, db);
        var actionId = await db.ExpertRunActions.Where(x => x.ActionType == "draft_generate").Select(x => x.Id).SingleAsync();
        var key = Guid.NewGuid().ToString();

        var first = await services.ConfirmActionAsync(10, 1, runId, actionId, new ConfirmSkillRunActionRequest(key), default);
        var second = await services.ConfirmActionAsync(10, 1, runId, actionId, new ConfirmSkillRunActionRequest(key), default);

        Assert.Equal(200, first.StatusCode);
        Assert.Equal(200, second.StatusCode);
        Assert.Equal(1, files.RegisterCalls);
    }

    /// <summary>非法幂等键 422；非本人动作 404；已终态换键 409。</summary>
    [Fact]
    public async Task Confirm_Rejects_Invalid_Key_Missing_Action_And_Reprocessing()
    {
        await using var db = NewDb("confirm-errors");
        SeedQuickEdit(db);
        var services = NewServices(db);
        var runId = await CreateRunAsync(services, db);
        var actionId = await db.ExpertRunActions.Where(x => x.ActionType == "draft_generate").Select(x => x.Id).SingleAsync();

        var invalidKey = await services.ConfirmActionAsync(10, 1, runId, actionId, new ConfirmSkillRunActionRequest("not-a-uuid"), default);
        Assert.Equal(422, invalidKey.StatusCode);

        var missing = await services.ConfirmActionAsync(11, 1, runId, actionId, new ConfirmSkillRunActionRequest(Guid.NewGuid().ToString()), default);
        Assert.Equal(404, missing.StatusCode);

        var first = await services.ConfirmActionAsync(10, 1, runId, actionId, new ConfirmSkillRunActionRequest(Guid.NewGuid().ToString()), default);
        Assert.True(first.Succeeded);
        var reprocessed = await services.ConfirmActionAsync(10, 1, runId, actionId, new ConfirmSkillRunActionRequest(Guid.NewGuid().ToString()), default);
        Assert.Equal(409, reprocessed.StatusCode);
    }

    /// <summary>文件登记失败：action failed、run failed、502，不写 skill_draft_registered 审计。</summary>
    [Fact]
    public async Task Confirm_Fails_When_Registration_Fails()
    {
        await using var db = NewDb("confirm-register-fail");
        SeedQuickEdit(db);
        var files = new FakeExpertFileServices { FailRegistration = true };
        var services = NewServices(db, files);
        var runId = await CreateRunAsync(services, db);
        var actionId = await db.ExpertRunActions.Where(x => x.ActionType == "draft_generate").Select(x => x.Id).SingleAsync();

        var result = await services.ConfirmActionAsync(10, 1, runId, actionId, new ConfirmSkillRunActionRequest(Guid.NewGuid().ToString()), default);

        Assert.Equal(502, result.StatusCode);
        var action = await db.ExpertRunActions.SingleAsync();
        Assert.Equal("failed", action.Status);
        var run = await db.AgentRuns.SingleAsync();
        Assert.Equal("failed", run.Status);
        Assert.Equal(1, await db.FamilyAuditLogs.CountAsync(x => x.Action == FamilyAuditActions.SkillActionConfirmed));
        Assert.Equal(0, await db.FamilyAuditLogs.CountAsync(x => x.Action == FamilyAuditActions.SkillDraftRegistered));
    }

    /// <summary>Mock 剪辑 MCP 生成确定性草稿结构：片段/时长/来源与方案一致。</summary>
    [Fact]
    public async Task MockClipping_Generates_Draft_With_Plan_Summary()
    {
        var client = new MockClippingMcpClient();

        var content = await client.GenerateDraftAsync("""{"media_location":"/nas/videos/探店.mp4","instruction":"竖屏 30 秒","segments":[{"index":1,"source":"探店.mp4","duration":30}],"total_duration":30}""", default);

        Assert.NotNull(content);
        Assert.True(content.Length > 0);
        using var document = JsonDocument.Parse(content);
        var root = document.RootElement;
        Assert.True(root.TryGetProperty("mock", out var mock) && mock.GetBoolean());
        Assert.Equal(1, root.GetProperty("summary").GetProperty("segment_count").GetInt32());
        Assert.Equal(30, root.GetProperty("summary").GetProperty("total_duration").GetInt32());
        var video = root.GetProperty("materials").GetProperty("videos")[0];
        Assert.Equal("探店.mp4", video.GetProperty("source").GetString());
        Assert.Equal(30, video.GetProperty("duration").GetInt32());
    }

    /// <summary>B31：修订以新指令重新生成方案：替换方案 RequestJson、plan_revised 事件、skill_run_revised 审计、视图携带新片段序列。</summary>
    [Fact]
    public async Task Revise_Succeeds_And_Replaces_Plan()
    {
        await using var db = NewDb("revise");
        SeedQuickEdit(db);
        var services = NewServices(db);
        var runId = await CreateRunAsync(services, db);
        var key = Guid.NewGuid().ToString();

        var result = await services.ReviseAsync(10, 1, runId, new ReviseSkillRunRequest("竖屏 60 秒，加字幕", key), default);

        Assert.Equal(200, result.StatusCode);
        var view = Assert.IsType<SkillRunView>(result.Data);
        Assert.Equal("pending_actions", view.Status);
        var action = Assert.Single(view.Actions);
        var segment = Assert.Single(action.Segments!);
        Assert.Equal(60, segment.Duration);
        Assert.Equal(60, action.TotalDuration);

        var stored = await db.ExpertRunActions.SingleAsync();
        using var plan = JsonDocument.Parse(stored.RequestJson);
        Assert.Equal(60, plan.RootElement.GetProperty("total_duration").GetInt32());
        Assert.Contains(db.RunEvents.ToList(), e => e.EventType == "plan_revised");
        var audit = await db.FamilyAuditLogs.SingleAsync(x => x.Action == FamilyAuditActions.SkillRunRevised);
        Assert.Equal(FamilyAuditTargetTypes.SkillRun, audit.TargetType);
        Assert.Equal(runId, audit.RelatedRunId);
    }

    /// <summary>B31：同一修订幂等键重放返回当前视图，不重复生成 plan_revised 事件与审计。</summary>
    [Fact]
    public async Task Revise_Replays_Same_Idempotency_Key()
    {
        await using var db = NewDb("revise-replay");
        SeedQuickEdit(db);
        var services = NewServices(db);
        var runId = await CreateRunAsync(services, db);
        var key = Guid.NewGuid().ToString();

        var first = await services.ReviseAsync(10, 1, runId, new ReviseSkillRunRequest("竖屏 45 秒", key), default);
        var second = await services.ReviseAsync(10, 1, runId, new ReviseSkillRunRequest("横屏 90 秒", key), default);

        Assert.Equal(200, first.StatusCode);
        Assert.Equal(200, second.StatusCode);
        Assert.Equal(1, await db.ActionExecutionAudits.CountAsync());
        Assert.Equal(1, db.RunEvents.Count(e => e.EventType == "plan_revised"));
        Assert.Equal(2, await db.FamilyAuditLogs.CountAsync()); // skill_run_created + skill_run_revised
        var view = Assert.IsType<SkillRunView>(second.Data);
        Assert.Equal(45, Assert.Single(Assert.Single(view.Actions).Segments!).Duration);
    }

    /// <summary>B31：方案已确认（action 非 pending）后修订返回 409，不覆盖已执行方案。</summary>
    [Fact]
    public async Task Revise_After_Confirm_Returns409()
    {
        await using var db = NewDb("revise-confirmed");
        SeedQuickEdit(db);
        var services = NewServices(db);
        var runId = await CreateRunAsync(services, db);
        var action = await db.ExpertRunActions.SingleAsync();
        await services.ConfirmActionAsync(10, 1, runId, action.Id, new ConfirmSkillRunActionRequest(Guid.NewGuid().ToString()), default);

        var result = await services.ReviseAsync(10, 1, runId, new ReviseSkillRunRequest("竖屏 60 秒", Guid.NewGuid().ToString()), default);

        Assert.Equal(409, result.StatusCode);
        Assert.DoesNotContain(db.RunEvents.ToList(), e => e.EventType == "plan_revised");
    }

    /// <summary>B31：跨用户/跨租户修订返回 404；非法幂等键返回 422。</summary>
    [Fact]
    public async Task Revise_OtherUser_Or_InvalidKey_Returns_404_422()
    {
        await using var db = NewDb("revise-other");
        SeedQuickEdit(db);
        var services = NewServices(db);
        var runId = await CreateRunAsync(services, db);

        var otherUser = await services.ReviseAsync(11, 1, runId, new ReviseSkillRunRequest("竖屏 30 秒", Guid.NewGuid().ToString()), default);
        var otherTenant = await services.ReviseAsync(10, 2, runId, new ReviseSkillRunRequest("竖屏 30 秒", Guid.NewGuid().ToString()), default);
        var invalidKey = await services.ReviseAsync(10, 1, runId, new ReviseSkillRunRequest("竖屏 30 秒", "not-a-guid"), default);

        Assert.Equal(404, otherUser.StatusCode);
        Assert.Equal(404, otherTenant.StatusCode);
        Assert.Equal(422, invalidKey.StatusCode);
    }

    private static async Task<long> CreateRunAsync(SkillRunServices services, HomeMindDbContext db)
    {
        var created = await services.CreateAsync(10, 1, "quick-edit", new SkillRunCreateRequest(null, """{"media_location":"/nas/videos/探店.mp4","instruction":"竖屏 30 秒"}"""), default);
        Assert.True(created.Succeeded);
        return Assert.IsType<SkillRunView>(created.Data).Id;
    }

    private static SkillRunServices NewServices(HomeMindDbContext db, FakeExpertFileServices? files = null) =>
        new(db, new FamilyAuditLogger(db, NullLogger<FamilyAuditLogger>.Instance), new MockClippingMcpClient(), files ?? new FakeExpertFileServices());

    private static HomeMindDbContext NewDb(string name) =>
        new(new DbContextOptionsBuilder<HomeMindDbContext>()
            .UseInMemoryDatabase($"hm-b24-{name}-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private static void SeedQuickEdit(HomeMindDbContext db)
    {
        db.SkillCatalogs.Add(new SkillCatalog
        {
            TenantId = 1,
            Key = "quick-edit",
            Name = "快速剪辑",
            Category = "media",
            Description = "把本机/NAS 素材按创作目标和指令生成可编辑的剪映草稿。",
            InputSchema = """{"type":"object","required":["media_location"]}""",
            OutputSchema = """{"type":"object"}""",
            RequiredPermission = "media.read",
            RiskLevel = "L1",
            Status = "active",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        db.SaveChanges();
    }

    /// <summary>专家文件服务测试替身：仅登记服务端生成文件，记录调用参数并返回固定 fileId。</summary>
    private sealed class FakeExpertFileServices : IExpertFileServices
    {
        /// <summary>是否模拟登记失败（对象存储不可用）。</summary>
        public bool FailRegistration { get; set; }

        /// <summary>登记调用次数。</summary>
        public int RegisterCalls { get; private set; }

        /// <summary>最近一次登记的文件名。</summary>
        public string? LastName { get; private set; }

        /// <summary>最近一次登记的 MIME 类型。</summary>
        public string? LastMime { get; private set; }

        /// <summary>最近一次登记的文件内容。</summary>
        public byte[]? LastContent { get; private set; }

        /// <summary>最近一次登记的附件运行主键。</summary>
        public long? LastRunId { get; private set; }

        public Task<ServiceResult> RegisterGeneratedFileAsync(long userId, long tenantId, string name, string mimeType, byte[] content, long? attachRunId, CancellationToken cancellationToken = default)
        {
            RegisterCalls++;
            LastName = name;
            LastMime = mimeType;
            LastContent = content;
            LastRunId = attachRunId;
            if (FailRegistration) return Task.FromResult(new ServiceResult(503, "对象存储暂不可用，请稍后重试。"));
            return Task.FromResult(new ServiceResult(201, "生成文件已就绪。", new { fileId = 100, status = "ready", name, mimeType, sizeBytes = content.Length }));
        }

        public Task<ServiceResult> CreateUploadAsync(long userId, long tenantId, ExpertFileUploadRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ServiceResult> CommitObjectAsync(long userId, long tenantId, long fileId, ExpertFileObjectRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ServiceResult> ListAsync(long userId, long tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ServiceResult> DeleteAsync(long userId, long tenantId, long fileId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ServiceResult> AttachToExpertAsync(long userId, long tenantId, long expertId, ExpertFileAttachmentRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ServiceResult> AttachToRunAsync(long userId, long tenantId, long runId, ExpertFileAttachmentRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ServiceResult> GenerateReadTokenAsync(long userId, long tenantId, long fileId, string purpose, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ServiceResult> GetContentAsync(long userId, long tenantId, long fileId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
