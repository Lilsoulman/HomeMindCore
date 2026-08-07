using System.Text.Json;
using HomeMind.Business.IServices.Connector;
using HomeMind.Business.IServices.SmartHome;
using HomeMind.Business.Services.Connectors.Bridge;
using HomeMind.Business.Services.SmartHome;
using HomeMind.Common.Model.Entities;
using HomeMind.Common.Model.Entities.SmartHome;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.SmartHome;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HomeMind.Business.Services.Tests;

/// <summary>
/// 场景工作流定向测试：模板列表、Device Resolver 实例化（含缺设备容忍）、场景运行创建、
/// 确认执行的状态计算规则（success/partial/failed）、幂等重放与兼容代理懒启用。
/// </summary>
public class ScenarioWorkflowServicesTests
{
    private const string GoodnightSteps = """
        [{"id":"step_1","name":"关闭卧室灯","device_type":"light","room":"bedroom","capability":"power","value":false,"optional":false},
         {"id":"step_2","name":"设置卧室空调","device_type":"air_conditioner","room":"bedroom","capability":"temperature","value":26,"optional":true}]
        """;

    /// <summary>模板列表返回种子模板。</summary>
    [Fact]
    public async Task ListTemplates_Returns_Seeded_Templates()
    {
        await using var db = NewDb("list-templates");
        SeedTemplate(db, "goodnight", "晚安", GoodnightSteps);
        var services = new ScenarioWorkflowServices(db, NewRelay(new FakeCommandExecutor()));

        var result = await services.ListTemplatesAsync(1, default);

        Assert.True(result.Succeeded);
        var templates = Assert.IsType<ScenarioTemplateView[]>(result.Data);
        var template = Assert.Single(templates);
        Assert.Equal("goodnight", template.Code);
        Assert.Equal(2, template.Steps.Count);
    }

    /// <summary>Enable 按 device_type + room + capability 解析设备并落库实例；重复启用返回既有实例。</summary>
    [Fact]
    public async Task Enable_Resolves_Devices_By_Type_Room_Capability()
    {
        await using var db = NewDb("enable-resolve");
        SeedTemplate(db, "goodnight", "晚安", GoodnightSteps);
        SeedDeviceContext(db);
        var services = new ScenarioWorkflowServices(db, NewRelay(new FakeCommandExecutor()));

        var result = await services.EnableAsync(10, 1, "goodnight", default);

        Assert.True(result.Succeeded);
        var instance = Assert.IsType<ScenarioInstanceView>(result.Data);
        Assert.Equal("goodnight", instance.TemplateCode);
        Assert.All(instance.Steps, x => Assert.Equal("ready", x.StepStatus));
        Assert.Equal(1, instance.Steps[0].DeviceId);
        Assert.Equal(2, instance.Steps[1].DeviceId);

        var repeated = await services.EnableAsync(10, 1, "goodnight", default);
        Assert.Equal(200, repeated.StatusCode);
        Assert.Equal(1, await db.ScenarioInstances.CountAsync());
    }

    /// <summary>缺设备时启用仍成功，步骤标记 unavailable 并携带原因（Enable-time tolerant）。</summary>
    [Fact]
    public async Task Enable_Tolerates_Missing_Device_With_Unavailable_Step()
    {
        await using var db = NewDb("enable-tolerant");
        const string steps = """
            [{"id":"step_1","name":"打开窗帘","device_type":"curtain","room":"bedroom","capability":"open","value":true,"optional":false}]
            """;
        SeedTemplate(db, "morning", "晨光", steps);
        SeedDeviceContext(db);
        var services = new ScenarioWorkflowServices(db, NewRelay(new FakeCommandExecutor()));

        var result = await services.EnableAsync(10, 1, "morning", default);

        Assert.True(result.Succeeded);
        var instance = Assert.IsType<ScenarioInstanceView>(result.Data);
        var step = Assert.Single(instance.Steps);
        Assert.Equal("unavailable", step.StepStatus);
        Assert.Equal("no matching device", step.Reason);
        Assert.Null(step.DeviceId);
    }

    /// <summary>场景运行创建单个 scenario 动作，步骤上下文承载于 RequestJson，unavailable 步骤标记 skipped。</summary>
    [Fact]
    public async Task Run_Creates_Single_Scenario_Action_With_Metadata()
    {
        await using var db = NewDb("run-create");
        SeedTemplate(db, "goodnight", "晚安", GoodnightSteps);
        SeedDeviceContext(db);
        var services = new ScenarioWorkflowServices(db, NewRelay(new FakeCommandExecutor()));
        var instance = Assert.IsType<ScenarioInstanceView>((await services.EnableAsync(10, 1, "goodnight", default)).Data);

        var result = await services.RunAsync(10, 1, instance.Id, new ScenarioRunRequest(null), default);

        Assert.True(result.Succeeded);
        var run = Assert.IsType<ScenarioRunView>(result.Data);
        Assert.Equal("pending_actions", run.Status);
        var action = Assert.Single(run.Actions);
        Assert.Equal("scenario", action.ActionType);
        Assert.Equal("pending", action.Status);
        var stored = await db.ExpertRunActions.SingleAsync(x => x.ActionType == "scenario");
        using var document = JsonDocument.Parse(stored.RequestJson);
        Assert.Equal(instance.Id, document.RootElement.GetProperty("scenario_id").GetInt64());
        Assert.Equal("晚安", document.RootElement.GetProperty("scenario_name").GetString());
        Assert.All(document.RootElement.GetProperty("steps").EnumerateArray(), x => Assert.Equal("pending", x.GetProperty("status").GetString()));
    }

    /// <summary>同一幂等键重复运行返回既有运行，不重复创建动作。</summary>
    [Fact]
    public async Task Run_Replays_Existing_By_Idempotency_Key()
    {
        await using var db = NewDb("run-replay");
        SeedTemplate(db, "goodnight", "晚安", GoodnightSteps);
        SeedDeviceContext(db);
        var services = new ScenarioWorkflowServices(db, NewRelay(new FakeCommandExecutor()));
        var instance = Assert.IsType<ScenarioInstanceView>((await services.EnableAsync(10, 1, "goodnight", default)).Data);
        var key = Guid.NewGuid().ToString();

        var first = await services.RunAsync(10, 1, instance.Id, new ScenarioRunRequest(key), default);
        var second = await services.RunAsync(10, 1, instance.Id, new ScenarioRunRequest(key), default);

        Assert.Equal(201, first.StatusCode);
        Assert.Equal(200, second.StatusCode);
        Assert.Equal(1, await db.ExpertRunActions.CountAsync());
    }

    /// <summary>全部步骤成功 → Run 结果 success，动作 executed，幂等审计落库。</summary>
    [Fact]
    public async Task Confirm_All_Success_Produces_Success_Result()
    {
        await using var db = NewDb("confirm-success");
        SeedTemplate(db, "goodnight", "晚安", GoodnightSteps);
        SeedDeviceContext(db);
        var executor = new FakeCommandExecutor();
        var services = new ScenarioWorkflowServices(db, NewRelay(executor));
        var runId = await CreatePendingRunAsync(services, db);

        var result = await services.ConfirmActionAsync(10, 1, runId, await ActionIdAsync(db), new ConfirmScenarioActionRequest(Guid.NewGuid().ToString()), default);

        Assert.True(result.Succeeded);
        Assert.Equal(2, executor.ExecutedCount);
        var action = await db.ExpertRunActions.SingleAsync();
        Assert.Equal("executed", action.Status);
        var run = await db.AgentRuns.SingleAsync();
        Assert.Equal("completed", run.Status);
        using var document = JsonDocument.Parse(run.Result);
        Assert.Equal("success", document.RootElement.GetProperty("status").GetString());
        Assert.Equal(2, document.RootElement.GetProperty("success_count").GetInt64());
        Assert.Equal(0, document.RootElement.GetProperty("failed_count").GetInt64());
    }

    /// <summary>required 步骤失败后继续执行后续步骤，汇总 partial，失败原因写入结果。</summary>
    [Fact]
    public async Task Confirm_Required_Failure_Continues_And_Produces_Partial()
    {
        await using var db = NewDb("confirm-partial");
        SeedTemplate(db, "goodnight", "晚安", GoodnightSteps);
        SeedDeviceContext(db);
        var executor = new FakeCommandExecutor { ShouldFail = command => command.Capability == "power" };
        var services = new ScenarioWorkflowServices(db, NewRelay(executor));
        var runId = await CreatePendingRunAsync(services, db);

        var result = await services.ConfirmActionAsync(10, 1, runId, await ActionIdAsync(db), new ConfirmScenarioActionRequest(Guid.NewGuid().ToString()), default);

        Assert.True(result.Succeeded);
        Assert.Equal(2, executor.ExecutedCount);
        var run = await db.AgentRuns.SingleAsync();
        using var document = JsonDocument.Parse(run.Result);
        Assert.Equal("partial", document.RootElement.GetProperty("status").GetString());
        Assert.Equal(1, document.RootElement.GetProperty("success_count").GetInt64());
        var failed = document.RootElement.GetProperty("failed_steps").EnumerateArray().Single();
        Assert.Equal("关闭卧室灯", failed.GetProperty("name").GetString());
        Assert.Contains("模拟执行失败", failed.GetProperty("reason").GetString());
    }

    /// <summary>仅 optional 步骤失败 → Run 结果 success（可选失败不阻塞场景）。</summary>
    [Fact]
    public async Task Confirm_Only_Optional_Failure_Produces_Success()
    {
        await using var db = NewDb("confirm-optional");
        SeedTemplate(db, "goodnight", "晚安", GoodnightSteps);
        SeedDeviceContext(db);
        var executor = new FakeCommandExecutor { ShouldFail = command => command.Capability == "temperature" };
        var services = new ScenarioWorkflowServices(db, NewRelay(executor));
        var runId = await CreatePendingRunAsync(services, db);

        var result = await services.ConfirmActionAsync(10, 1, runId, await ActionIdAsync(db), new ConfirmScenarioActionRequest(Guid.NewGuid().ToString()), default);

        Assert.True(result.Succeeded);
        var run = await db.AgentRuns.SingleAsync();
        using var document = JsonDocument.Parse(run.Result);
        Assert.Equal("success", document.RootElement.GetProperty("status").GetString());
        Assert.Equal(1, document.RootElement.GetProperty("success_count").GetInt64());
        Assert.Equal(1, document.RootElement.GetProperty("failed_count").GetInt64());
    }

    /// <summary>全部步骤失败 → Run 结果 failed，动作 failed，摘要面向用户。</summary>
    [Fact]
    public async Task Confirm_All_Failed_Produces_Failed()
    {
        await using var db = NewDb("confirm-failed");
        SeedTemplate(db, "goodnight", "晚安", GoodnightSteps);
        SeedDeviceContext(db);
        var executor = new FakeCommandExecutor { ShouldFail = _ => true };
        var services = new ScenarioWorkflowServices(db, NewRelay(executor));
        var runId = await CreatePendingRunAsync(services, db);

        var result = await services.ConfirmActionAsync(10, 1, runId, await ActionIdAsync(db), new ConfirmScenarioActionRequest(Guid.NewGuid().ToString()), default);

        Assert.Equal(502, result.StatusCode);
        var action = await db.ExpertRunActions.SingleAsync();
        Assert.Equal("failed", action.Status);
        var run = await db.AgentRuns.SingleAsync();
        using var document = JsonDocument.Parse(run.Result);
        Assert.Equal("failed", document.RootElement.GetProperty("status").GetString());
        Assert.Equal(0, document.RootElement.GetProperty("success_count").GetInt64());
        Assert.Contains("均未成功", run.ResultSummary);
    }

    /// <summary>同一幂等键重复确认重放首次结果，不重复执行设备命令。</summary>
    [Fact]
    public async Task Confirm_Replays_Same_Idempotency_Key()
    {
        await using var db = NewDb("confirm-replay");
        SeedTemplate(db, "goodnight", "晚安", GoodnightSteps);
        SeedDeviceContext(db);
        var executor = new FakeCommandExecutor();
        var services = new ScenarioWorkflowServices(db, NewRelay(executor));
        var runId = await CreatePendingRunAsync(services, db);
        var key = Guid.NewGuid().ToString();

        var first = await services.ConfirmActionAsync(10, 1, runId, await ActionIdAsync(db), new ConfirmScenarioActionRequest(key), default);
        var second = await services.ConfirmActionAsync(10, 1, runId, await ActionIdAsync(db), new ConfirmScenarioActionRequest(key), default);

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.Equal(2, executor.ExecutedCount);
    }

    /// <summary>非法幂等键 422；非本人动作 404；已终态动作换键 409。</summary>
    [Fact]
    public async Task Confirm_Rejects_Invalid_Key_Missing_Action_And_Reprocessing()
    {
        await using var db = NewDb("confirm-errors");
        SeedTemplate(db, "goodnight", "晚安", GoodnightSteps);
        SeedDeviceContext(db);
        var services = new ScenarioWorkflowServices(db, NewRelay(new FakeCommandExecutor()));
        var runId = await CreatePendingRunAsync(services, db);
        var actionId = await ActionIdAsync(db);

        var invalidKey = await services.ConfirmActionAsync(10, 1, runId, actionId, new ConfirmScenarioActionRequest("not-a-uuid"), default);
        Assert.Equal(422, invalidKey.StatusCode);

        var missing = await services.ConfirmActionAsync(11, 1, runId, actionId, new ConfirmScenarioActionRequest(Guid.NewGuid().ToString()), default);
        Assert.Equal(404, missing.StatusCode);

        var first = await services.ConfirmActionAsync(10, 1, runId, actionId, new ConfirmScenarioActionRequest(Guid.NewGuid().ToString()), default);
        Assert.True(first.Succeeded);
        var reprocessed = await services.ConfirmActionAsync(10, 1, runId, actionId, new ConfirmScenarioActionRequest(Guid.NewGuid().ToString()), default);
        Assert.Equal(409, reprocessed.StatusCode);
    }

    /// <summary>禁用已启用实例：状态置为 disabled 并落库，返回实例视图。</summary>
    [Fact]
    public async Task Disable_Flips_Enabled_To_Disabled()
    {
        await using var db = NewDb("disable-flip");
        SeedTemplate(db, "goodnight", "晚安", GoodnightSteps);
        SeedDeviceContext(db);
        var services = new ScenarioWorkflowServices(db, NewRelay(new FakeCommandExecutor()));
        var instance = Assert.IsType<ScenarioInstanceView>((await services.EnableAsync(10, 1, "goodnight", default)).Data);

        var result = await services.DisableAsync(1, instance.Id, default);

        Assert.True(result.Succeeded);
        Assert.Equal("disabled", Assert.IsType<ScenarioInstanceView>(result.Data).Status);
        Assert.Equal("disabled", (await db.ScenarioInstances.SingleAsync()).Status);
    }

    /// <summary>重复禁用幂等：均返回 200 且实例状态不变。</summary>
    [Fact]
    public async Task Disable_Is_Idempotent()
    {
        await using var db = NewDb("disable-idempotent");
        SeedTemplate(db, "goodnight", "晚安", GoodnightSteps);
        SeedDeviceContext(db);
        var services = new ScenarioWorkflowServices(db, NewRelay(new FakeCommandExecutor()));
        var instance = Assert.IsType<ScenarioInstanceView>((await services.EnableAsync(10, 1, "goodnight", default)).Data);

        var first = await services.DisableAsync(1, instance.Id, default);
        var second = await services.DisableAsync(1, instance.Id, default);

        Assert.Equal(200, first.StatusCode);
        Assert.Equal(200, second.StatusCode);
        Assert.Equal("disabled", (await db.ScenarioInstances.SingleAsync()).Status);
    }

    /// <summary>禁用后触发新运行返回 404，不创建场景动作。</summary>
    [Fact]
    public async Task Disable_Then_Run_Returns_404()
    {
        await using var db = NewDb("disable-run");
        SeedTemplate(db, "goodnight", "晚安", GoodnightSteps);
        SeedDeviceContext(db);
        var services = new ScenarioWorkflowServices(db, NewRelay(new FakeCommandExecutor()));
        var instance = Assert.IsType<ScenarioInstanceView>((await services.EnableAsync(10, 1, "goodnight", default)).Data);
        await services.DisableAsync(1, instance.Id, default);

        var result = await services.RunAsync(10, 1, instance.Id, new ScenarioRunRequest(null), default);

        Assert.Equal(404, result.StatusCode);
        Assert.Equal(0, await db.ExpertRunActions.CountAsync());
    }

    /// <summary>实例不存在或跨租户禁用返回 404。</summary>
    [Fact]
    public async Task Disable_Missing_Or_Cross_Tenant_Returns_404()
    {
        await using var db = NewDb("disable-missing");
        SeedTemplate(db, "goodnight", "晚安", GoodnightSteps);
        SeedDeviceContext(db);
        var services = new ScenarioWorkflowServices(db, NewRelay(new FakeCommandExecutor()));
        var instance = Assert.IsType<ScenarioInstanceView>((await services.EnableAsync(10, 1, "goodnight", default)).Data);

        var missing = await services.DisableAsync(1, 9999, default);
        var crossTenant = await services.DisableAsync(2, instance.Id, default);

        Assert.Equal(404, missing.StatusCode);
        Assert.Equal(404, crossTenant.StatusCode);
    }

    /// <summary>禁用后重复启用恢复为 enabled，实例不重复创建。</summary>
    [Fact]
    public async Task Enable_Revives_Disabled_Instance()
    {
        await using var db = NewDb("enable-revive");
        SeedTemplate(db, "goodnight", "晚安", GoodnightSteps);
        SeedDeviceContext(db);
        var services = new ScenarioWorkflowServices(db, NewRelay(new FakeCommandExecutor()));
        var instance = Assert.IsType<ScenarioInstanceView>((await services.EnableAsync(10, 1, "goodnight", default)).Data);
        await services.DisableAsync(1, instance.Id, default);

        var result = await services.EnableAsync(10, 1, "goodnight", default);

        Assert.Equal(200, result.StatusCode);
        Assert.Equal("enabled", Assert.IsType<ScenarioInstanceView>(result.Data).Status);
        Assert.Equal(1, await db.ScenarioInstances.CountAsync());
    }

    /// <summary>旧场景路由兼容代理：懒启用实例并转调场景运行，自动化场景完成事件仍发布。</summary>
    [Fact]
    public async Task SceneProxy_Lazily_Enables_And_Runs()
    {
        await using var db = NewDb("proxy-lazy");
        SeedTemplate(db, "sleep", "睡眠", GoodnightSteps);
        SeedDeviceContext(db);
        var services = new ScenarioWorkflowServices(db, NewRelay(new FakeCommandExecutor()));
        var automation = new FakeAutomation();
        var proxy = new SmartHomeSceneServices(services, automation);

        var result = await proxy.RunAsync(10, 1, "sleep", new SceneRunRequest(null), default);

        Assert.True(result.Succeeded);
        Assert.Equal(1, await db.ScenarioInstances.CountAsync(x => x.TemplateCode == "sleep"));
        Assert.Equal(1, await db.ExpertRunActions.CountAsync(x => x.ActionType == "scenario"));
        Assert.Equal(1, automation.SceneCompletedCount);
    }

    private static async Task<long> CreatePendingRunAsync(ScenarioWorkflowServices services, HomeMindDbContext db)
    {
        var instance = Assert.IsType<ScenarioInstanceView>((await services.EnableAsync(10, 1, "goodnight", default)).Data);
        var created = await services.RunAsync(10, 1, instance.Id, new ScenarioRunRequest(null), default);
        Assert.True(created.Succeeded);
        return await db.ExpertRunActions.Where(x => x.ActionType == "scenario").Select(x => x.RunId).SingleAsync();
    }

    private static async Task<long> ActionIdAsync(HomeMindDbContext db) =>
        await db.ExpertRunActions.Where(x => x.ActionType == "scenario").Select(x => x.Id).SingleAsync();

    private static HomeMindDbContext NewDb(string name) =>
        new(new DbContextOptionsBuilder<HomeMindDbContext>()
            .UseInMemoryDatabase($"hm-b22-{name}-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private static CommandRelayService NewRelay(FakeCommandExecutor executor) =>
        new([new FakeAdapter()], [executor]);

    private static void SeedTemplate(HomeMindDbContext db, string code, string name, string stepsJson)
    {
        db.ScenarioTemplates.Add(new ScenarioTemplate
        {
            TenantId = 1,
            Code = code,
            Name = name,
            Status = "active",
            TriggerKeywords = """["晚安","我要睡觉了"]""",
            Steps = stepsJson,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        db.SaveChanges();
    }

    private static void SeedDeviceContext(HomeMindDbContext db)
    {
        var now = DateTime.UtcNow;
        db.SmartHomeSpaces.AddRange(
            new SmartHomeSpace { Id = 1, TenantId = 1, Name = "卧室", SpaceType = "bedroom", SortOrder = 1, CreatedAt = now, UpdatedAt = now },
            new SmartHomeSpace { Id = 2, TenantId = 1, Name = "客厅", SpaceType = "living_room", SortOrder = 2, CreatedAt = now, UpdatedAt = now });
        db.ConnectorProviders.Add(new ConnectorProvider { Id = 1, Code = "home_assistant", Name = "Home Assistant", Provider = "home_assistant", ConnectorType = "smart_home", Status = "active" });
        db.WorkspaceConnectors.Add(new WorkspaceConnector
        {
            Id = 1,
            TenantId = 1,
            ConnectorProviderId = 1,
            Name = "家庭 HA",
            Status = "connected",
            CredentialRef = "vault://tenants/1/ha",
            LastHealthAt = now,
            CreatedAt = now,
            UpdatedAt = now
        });
        db.SmartHomeDevices.AddRange(
            new SmartHomeDevice { Id = 1, TenantId = 1, WorkspaceConnectorId = 1, SpaceId = 1, Name = "卧室灯", DeviceType = "light", OnlineStatus = "online", LastSeenAt = now, CreatedAt = now, UpdatedAt = now },
            new SmartHomeDevice { Id = 2, TenantId = 1, WorkspaceConnectorId = 1, SpaceId = 1, Name = "卧室空调", DeviceType = "air_conditioner", OnlineStatus = "online", LastSeenAt = now, CreatedAt = now, UpdatedAt = now },
            new SmartHomeDevice { Id = 3, TenantId = 1, WorkspaceConnectorId = 1, SpaceId = 2, Name = "客厅灯", DeviceType = "light", OnlineStatus = "online", LastSeenAt = now, CreatedAt = now, UpdatedAt = now });
        db.DeviceCapabilities.AddRange(
            new DeviceCapability { Id = 1, DeviceId = 1, Capability = "power", ValueSchema = """{"type":"boolean"}""", Permission = "light.write", IsWritable = true, CreatedAt = now, UpdatedAt = now },
            new DeviceCapability { Id = 2, DeviceId = 2, Capability = "temperature", ValueSchema = """{"type":"integer"}""", Permission = "climate.write", IsWritable = true, CreatedAt = now, UpdatedAt = now },
            new DeviceCapability { Id = 3, DeviceId = 3, Capability = "power", ValueSchema = """{"type":"boolean"}""", Permission = "light.write", IsWritable = true, CreatedAt = now, UpdatedAt = now });
        db.UserConnectorAuthorizations.Add(new UserConnectorAuthorization
        {
            TenantId = 1,
            UserId = 10,
            WorkspaceConnectorId = 1,
            Scope = """["*.*"]""",
            CreatedAt = now,
            UpdatedAt = now
        });
        db.SaveChanges();
    }

    /// <summary>命令执行器测试替身：按能力条件注入失败，统计执行次数。</summary>
    private sealed class FakeCommandExecutor : IDeviceCommandExecutor
    {
        public string ProviderCode => "home_assistant";
        public int ExecutedCount { get; private set; }
        public Func<DeviceCommand, bool>? ShouldFail { get; set; }

        public Task<DeviceCommandResult> ExecuteCommandAsync(ConnectorReference connector, DeviceCommand command, CancellationToken cancellationToken = default)
        {
            ExecutedCount++;
            var fail = ShouldFail?.Invoke(command) ?? false;
            return Task.FromResult(fail
                ? new DeviceCommandResult(false, "failed", "execution_error", "模拟执行失败。")
                : new DeviceCommandResult(true, "executed"));
        }
    }

    /// <summary>适配器测试替身：健康检查通过，不读取设备状态。</summary>
    private sealed class FakeAdapter : IDeviceAdapter
    {
        public string ProviderCode => "home_assistant";

        public Task<ConnectorConnectionTestResult> TestConnectionAsync(ConnectorReference connector, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ConnectorConnectionTestResult(true));

        public Task<AdapterDeviceState?> ReadDeviceStateAsync(ConnectorReference connector, long deviceId, CancellationToken cancellationToken = default) =>
            Task.FromResult<AdapterDeviceState?>(null);
    }

    /// <summary>自动化规则服务测试替身，记录场景完成事件发布次数。</summary>
    private sealed class FakeAutomation : IAutomationRuleServices
    {
        public int SceneCompletedCount { get; private set; }

        public Task<ServiceResult> ListAsync(long tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ServiceResult(200, "ok"));
        public Task<ServiceResult> CreateAsync(long userId, long tenantId, AutomationRuleRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ServiceResult(201, "ok"));
        public Task<ServiceResult> UpdateAsync(long userId, long tenantId, long ruleId, UpdateAutomationRuleRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ServiceResult(200, "ok"));
        public Task<ServiceResult> HandleDeviceStateChangeAsync(long tenantId, long deviceId, string state, DateTime occurredAt, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ServiceResult(200, "ok"));
        public Task<ServiceResult> HandleSceneCompletedAsync(long tenantId, string sceneKey, DateTime occurredAt, CancellationToken cancellationToken = default)
        {
            SceneCompletedCount++;
            return Task.FromResult(new ServiceResult(200, "ok"));
        }
        public Task<ServiceResult> HandleSyncCompletedAsync(long tenantId, long connectorId, DateTime occurredAt, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ServiceResult(200, "ok"));
        public Task<int> ProcessDueSchedulesAsync(DateTime now, CancellationToken cancellationToken = default) =>
            Task.FromResult(0);
    }
}
