using System.Text.Json;
using HomeMind.Business.IServices.AI;
using HomeMind.Business.IServices.Connector;
using HomeMind.Business.IServices.SmartHome;
using HomeMind.Business.Services.Connectors.Adapters;
using HomeMind.Business.Services.Connectors.Bridge;
using HomeMind.Common.Model.Entities;
using HomeMind.Common.Model.Entities.SmartHome;
using HomeMind.Common.Model.Entities.Steward;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.SmartHome;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;

namespace HomeMind.Business.Services.SmartHome;

/// <summary>
/// 场景工作流确定性编排：平台模板 → 家庭实例 → Run 执行。场景运行创建单个
/// <c>scenario</c> 类型的 Run Action，步骤上下文承载于 RequestJson（即定稿的 metadata）；
/// 确认后逐步经 CommandRelayService 下发设备命令，required 步骤失败后继续后续步骤，
/// 按 success / partial / failed 汇总写入运行结果。所有运行复用既有 AgentRun、
/// 确认、幂等与审计边界，不新建运行时。
/// </summary>
public sealed class ScenarioWorkflowServices : IScenarioWorkflowServices
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> HighRiskDeviceTypes = new(StringComparer.OrdinalIgnoreCase) { "lock", "camera", "security_alarm" };
    private static readonly HashSet<string> HighRiskCapabilities = new(StringComparer.OrdinalIgnoreCase) { "lock" };

    private readonly HomeMindDbContext _db;
    private readonly CommandRelayService _relay;

    /// <summary>构造场景工作流服务。</summary>
    /// <param name="db">数据库上下文。</param>
    /// <param name="relay">命令转发桥接服务，健康检查通过后转发设备命令。</param>
    public ScenarioWorkflowServices(HomeMindDbContext db, CommandRelayService relay)
    {
        _db = db;
        _relay = relay;
    }

    /// <inheritdoc />
    public async Task<ServiceResult> ListTemplatesAsync(long tenantId, CancellationToken cancellationToken = default)
    {
        var templates = await _db.ScenarioTemplates
            .Where(x => x.TenantId == 1 && x.Status == ScenarioTemplateStatus.Active && x.DeletedAt == null)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);
        return new ServiceResult(200, "查询成功。", templates.Select(ToTemplateView).ToArray());
    }

    /// <inheritdoc />
    public async Task<ServiceResult> ListInstancesAsync(long tenantId, CancellationToken cancellationToken = default)
    {
        var instances = await _db.ScenarioInstances
            .Where(x => x.TenantId == tenantId && x.DeletedAt == null)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);
        return new ServiceResult(200, "查询成功。", instances.Select(ToInstanceView).ToArray());
    }

    /// <inheritdoc />
    public async Task<ServiceResult> EnableAsync(long userId, long tenantId, string templateCode, CancellationToken cancellationToken = default)
    {
        var template = await _db.ScenarioTemplates.SingleOrDefaultAsync(
            x => x.TenantId == 1 && x.Code == templateCode && x.Status == ScenarioTemplateStatus.Active && x.DeletedAt == null, cancellationToken);
        if (template is null) return new ServiceResult(404, "请求的场景模板不存在或已停用。");

        var existing = await _db.ScenarioInstances.SingleOrDefaultAsync(
            x => x.TenantId == tenantId && x.TemplateCode == templateCode && x.DeletedAt == null, cancellationToken);
        if (existing is not null)
        {
            if (existing.Status == ScenarioInstanceStatus.Disabled)
            {
                existing.Status = ScenarioInstanceStatus.Enabled;
                existing.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(cancellationToken);
                return new ServiceResult(200, "场景实例已重新启用。", ToInstanceView(existing));
            }
            return new ServiceResult(200, "场景实例已启用。", ToInstanceView(existing));
        }

        var context = await LoadDeviceContextAsync(tenantId, cancellationToken);
        var steps = ResolveSteps(template, context).ToArray();
        var now = DateTime.UtcNow;
        var instance = new ScenarioInstance
        {
            TenantId = tenantId,
            TemplateCode = template.Code,
            Name = template.Name,
            TriggerKeywords = template.TriggerKeywords,
            Steps = JsonSerializer.Serialize(steps, JsonOptions),
            Status = ScenarioInstanceStatus.Enabled,
            CreatedByUserId = userId,
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.ScenarioInstances.Add(instance);
        await _db.SaveChangesAsync(cancellationToken);
        return new ServiceResult(201, "场景实例已启用。", ToInstanceView(instance));
    }

    /// <inheritdoc />
    public async Task<ServiceResult> DisableAsync(long tenantId, long instanceId, CancellationToken cancellationToken = default)
    {
        var instance = await _db.ScenarioInstances.SingleOrDefaultAsync(
            x => x.Id == instanceId && x.TenantId == tenantId && x.DeletedAt == null, cancellationToken);
        if (instance is null) return new ServiceResult(404, "请求的场景实例不存在。");
        if (instance.Status != ScenarioInstanceStatus.Disabled)
        {
            instance.Status = ScenarioInstanceStatus.Disabled;
            instance.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }
        return new ServiceResult(200, "场景实例已禁用。", ToInstanceView(instance));
    }

    /// <inheritdoc />
    public async Task<ServiceResult> RunAsync(long userId, long tenantId, long instanceId, ScenarioRunRequest request, CancellationToken cancellationToken = default)
    {
        var instance = await _db.ScenarioInstances.SingleOrDefaultAsync(
            x => x.Id == instanceId && x.TenantId == tenantId && x.Status == ScenarioInstanceStatus.Enabled && x.DeletedAt == null, cancellationToken);
        if (instance is null) return new ServiceResult(404, "请求的场景实例不存在或未启用。");

        var idempotencyKey = Guid.TryParse(request?.IdempotencyKey, out var parsedKey) ? parsedKey.ToString() : Guid.NewGuid().ToString();
        var existing = await _db.AgentRuns.SingleOrDefaultAsync(
            x => x.TenantId == tenantId && x.UserId == userId && x.RequestIdempotencyKey == idempotencyKey, cancellationToken);
        if (existing is not null) return new ServiceResult(200, "场景运行已存在。", await ToViewAsync(existing, cancellationToken));

        var steps = ReadSteps(instance.Steps);
        var readySteps = steps.Where(x => x.StepStatus == ScenarioStepStatus.Ready).ToArray();
        var metadata = new { scenario_id = instance.Id, scenario_name = instance.Name, steps = readySteps.Select(ToMetadataStep).ToArray() };

        var now = DateTime.UtcNow;
        var run = new AgentRun
        {
            TenantId = tenantId,
            UserId = userId,
            SourceType = "scenario",
            ExpertVersionId = null,
            RequestIdempotencyKey = idempotencyKey,
            Input = JsonSerializer.Serialize(new { scenario_id = instance.Id, scenario_name = instance.Name }),
            Status = "planning",
            Mode = HousekeeperRunPolicies.Steward,
            AutoConfirmPolicy = HousekeeperRunPolicies.L3Only,
            PermissionSnapshot = JsonSerializer.Serialize(new { bindingScope = "household", ownerUserId = userId, connectorGrants = Array.Empty<object>() }),
            EstimatedCredits = 0,
            StartedAt = now,
            CreatedAt = now
        };
        _db.AgentRuns.Add(run);
        await _db.SaveChangesAsync(cancellationToken);

        var risk = readySteps.Length == 0 ? ConfirmationRiskLevel.L1 : readySteps.Max(x => StepRisk(x.DeviceType, x.Capability));
        _db.ExpertRunActions.Add(new ExpertRunAction
        {
            RunId = run.Id,
            TenantId = tenantId,
            UserId = userId,
            ActionType = "scenario",
            RequestIdempotencyKey = Guid.NewGuid().ToString(),
            RequestJson = JsonSerializer.Serialize(metadata, JsonOptions),
            Status = "pending",
            CreatedAt = now,
            UpdatedAt = now
        });

        run.Status = "pending_actions";
        run.ResultSummary = readySteps.Length == 0
            ? $"场景「{instance.Name}」已就绪，当前没有可执行的设备步骤。"
            : $"场景「{instance.Name}」已就绪，确认后执行 {readySteps.Length} 个设备步骤。";
        run.Result = JsonSerializer.Serialize(new { scenario_id = instance.Id, scenario_name = instance.Name, risk });
        AddEvent(run, 1, "running", "正在准备场景执行。", now);
        AddEvent(run, 2, "pending_actions", run.ResultSummary, now);
        await _db.SaveChangesAsync(cancellationToken);
        return new ServiceResult(201, run.ResultSummary, await ToViewAsync(run, cancellationToken));
    }

    /// <inheritdoc />
    public async Task<ServiceResult> ConfirmActionAsync(long userId, long tenantId, long runId, long actionId, ConfirmScenarioActionRequest request, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(request.IdempotencyKey, out _))
            return new ServiceResult(422, "确认场景动作时必须提供有效的幂等键。");

        var action = await _db.ExpertRunActions.SingleOrDefaultAsync(x =>
            x.Id == actionId && x.RunId == runId && x.TenantId == tenantId && x.UserId == userId && x.ActionType == "scenario", cancellationToken);
        if (action is null) return new ServiceResult(404, "请求的场景动作不存在。");

        var run = await _db.AgentRuns.SingleOrDefaultAsync(x => x.Id == runId && x.TenantId == tenantId, cancellationToken);
        if (run is null) return new ServiceResult(404, "请求的运行不存在。");
        if (!await IsSnapshotAuthorizedAsync(run, userId, cancellationToken))
            return new ServiceResult(403, "当前成员无权执行该运行的动作。");

        var idempotencyKey = request.IdempotencyKey;
        var previous = await _db.ActionExecutionAudits.SingleOrDefaultAsync(
            x => x.RunActionId == action.Id && x.IdempotencyKey == idempotencyKey, cancellationToken);
        if (previous is not null) return ReplayActionResult(action, previous);
        if (action.Status != "pending") return new ServiceResult(409, "该场景动作已经确认或处理完成，不能再次执行。");

        var metadata = ReadScenario(action.RequestJson);
        if (metadata is null) return new ServiceResult(422, "场景动作内容无效。");

        var now = DateTime.UtcNow;
        action.Status = "executing";
        action.UpdatedAt = now;
        var audit = new ActionExecutionAudit
        {
            TenantId = tenantId,
            RunActionId = action.Id,
            OperatorUserId = userId,
            IdempotencyKey = idempotencyKey,
            Status = "executing",
            Command = JsonSerializer.Serialize(new { scenario_id = metadata.ScenarioId, scenario_name = metadata.ScenarioName }),
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.ActionExecutionAudits.Add(audit);
        AddEvent(run, await NextSequenceAsync(runId, cancellationToken), "action_confirmed", $"已确认场景「{metadata.ScenarioName}」。", now);
        await _db.SaveChangesAsync(cancellationToken);

        foreach (var step in metadata.Steps.Where(x => x.Status == "pending"))
        {
            var result = await ExecuteStepAsync(step, userId, tenantId, action.Id, idempotencyKey, cancellationToken);
            step.Status = result.Succeeded ? "success" : result.Status;
            step.Reason = result.Succeeded ? null : result.Message;
        }

        var successCount = metadata.Steps.Count(x => x.Status == "success");
        var failedSteps = metadata.Steps.Where(x => x.Status is "failed" or "timeout").Select(x => new FailedStep(x.Name, x.Reason ?? "unknown_error")).ToArray();
        var requiredFailed = metadata.Steps.Count(x => (x.Status is "failed" or "timeout") && !x.Optional);
        var scenarioStatus = successCount == 0 ? "failed" : requiredFailed > 0 ? "partial" : "success";

        now = DateTime.UtcNow;
        action.RequestJson = JsonSerializer.Serialize(new { scenario_id = metadata.ScenarioId, scenario_name = metadata.ScenarioName, steps = metadata.Steps.Select(ToMetadataStep) }, JsonOptions);
        action.Status = successCount > 0 ? "executed" : "failed";
        action.Result = JsonSerializer.Serialize(new { status = action.Status, success_count = successCount, failed_count = failedSteps.Length, failed_steps = failedSteps }, JsonOptions);
        action.UpdatedAt = now;
        audit.Status = action.Status;
        audit.Result = action.Result;
        audit.UpdatedAt = now;

        var summary = BuildSummary(metadata.ScenarioName, scenarioStatus, successCount, metadata.Steps.Count(x => x.Status is "failed" or "timeout"), failedSteps);
        run.Status = "completed";
        run.FinishedAt = now;
        run.ResultSummary = summary;
        run.Result = JsonSerializer.Serialize(new { scenario = metadata.ScenarioName, status = scenarioStatus, summary, success_count = successCount, failed_count = failedSteps.Length, failed_steps = failedSteps }, JsonOptions);
        AddEvent(run, await NextSequenceAsync(runId, cancellationToken), successCount > 0 ? "action_executed" : "action_failed", summary, now);
        await _db.SaveChangesAsync(cancellationToken);
        return new ServiceResult(successCount > 0 ? 200 : 502, summary, new { actionId = action.Id, status = action.Status, message = summary });
    }

    /// <summary>逐步执行单条已就绪步骤；解析设备/连接器/授权失败与命令失败均记为步骤失败并继续后续步骤。</summary>
    private async Task<StepResult> ExecuteStepAsync(ScenarioStepData step, long userId, long tenantId, long actionId, string idempotencyKey, CancellationToken cancellationToken)
    {
        var device = await _db.SmartHomeDevices.SingleOrDefaultAsync(x => x.Id == step.DeviceId && x.TenantId == tenantId && x.DeletedAt == null, cancellationToken);
        if (device is null || device.WorkspaceConnectorId is null) return new StepResult(false, "failed", "device_not_found", "目标设备不存在或未关联连接器。");
        if (device.OnlineStatus != "online") return new StepResult(false, "failed", "device_offline", "设备当前离线，已跳过该步骤。");
        var capability = await _db.DeviceCapabilities.SingleOrDefaultAsync(x => x.DeviceId == device.Id && x.Capability == step.Capability && x.DeletedAt == null && x.IsWritable, cancellationToken);
        if (capability is null) return new StepResult(false, "failed", "capability_unavailable", "设备能力已失效，已跳过该步骤。");
        var connector = await _db.WorkspaceConnectors.SingleOrDefaultAsync(x => x.Id == device.WorkspaceConnectorId && x.TenantId == tenantId && x.DeletedAt == null, cancellationToken);
        if (connector is null || connector.Status != "connected" || connector.LastHealthAt is null || string.IsNullOrWhiteSpace(connector.CredentialRef))
            return new StepResult(false, "failed", "connector_unavailable", "连接器当前不可用，已跳过该步骤。");
        var authorized = await _db.UserConnectorAuthorizations.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.UserId == userId && x.WorkspaceConnectorId == connector.Id && x.DeletedAt == null, cancellationToken);
        if (authorized is null || !ScopeAllows(authorized.Scope, capability.Permission))
            return new StepResult(false, "failed", "not_authorized", "当前成员未获该设备能力的执行授权，已跳过该步骤。");
        var providerCode = await _db.ConnectorProviders.Where(x => x.Id == connector.ConnectorProviderId && x.DeletedAt == null && x.Status == "active").Select(x => x.Code).SingleOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(providerCode) || !_relay.SupportsProvider(providerCode))
            return new StepResult(false, "failed", "adapter_unavailable", "该连接器尚未提供设备行动适配器，已跳过该步骤。");

        var reference = new ConnectorReference(connector.Id, tenantId, connector.CredentialRef);
        try
        {
            var result = await _relay.ExecuteAsync(
                providerCode,
                reference,
                new DeviceCommand(connector.Id, device.Id, step.Capability, step.Value.Clone(), userId, actionId, idempotencyKey),
                cancellationToken);
            return new StepResult(result.Succeeded, result.Succeeded ? "success" : "failed", result.ErrorCode, result.Message);
        }
        catch (ConnectorAdapterException error)
        {
            return new StepResult(false, "failed", error.ErrorCode, error.Message);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new StepResult(false, "timeout", "timeout", "设备命令执行超时。");
        }
        catch (Exception)
        {
            return new StepResult(false, "failed", "execution_error", "设备命令执行失败。");
        }
    }

    /// <summary>按 device_type + room + capability 解析模板步骤为实例步骤；无匹配标记 unavailable 且不阻塞启用。</summary>
    private static IEnumerable<InstanceStepData> ResolveSteps(ScenarioTemplate template, DeviceContext context)
    {
        var templateSteps = ReadTemplateSteps(template.Steps);
        foreach (var templateStep in templateSteps)
        {
            var candidateDevices = context.Devices
                .Where(x => x.DeviceType == templateStep.DeviceType && (templateStep.Room == "*" || context.Spaces.TryGetValue(x.SpaceId ?? 0, out var space) && space.SpaceType.Contains(templateStep.Room, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(x => x.Id)
                .ToArray();
            var device = candidateDevices.FirstOrDefault();
            if (device is null)
            {
                yield return new InstanceStepData(templateStep.Id, templateStep.Name, templateStep.DeviceType, templateStep.Room, null, templateStep.Capability, templateStep.Value, templateStep.Optional, ScenarioStepStatus.Unavailable, "no matching device");
                continue;
            }
            var writable = context.Capabilities.Any(x => x.DeviceId == device.Id && x.Capability == templateStep.Capability && x.IsWritable);
            if (!writable)
            {
                yield return new InstanceStepData(templateStep.Id, templateStep.Name, templateStep.DeviceType, templateStep.Room, device.Id, templateStep.Capability, templateStep.Value, templateStep.Optional, ScenarioStepStatus.Unavailable, "no matching capability");
                continue;
            }
            yield return new InstanceStepData(templateStep.Id, templateStep.Name, templateStep.DeviceType, templateStep.Room, device.Id, templateStep.Capability, templateStep.Value, templateStep.Optional, ScenarioStepStatus.Ready, null);
        }
    }

    /// <summary>加载设备解析上下文：空间、设备与可写能力一次读入后内存匹配。</summary>
    private async Task<DeviceContext> LoadDeviceContextAsync(long tenantId, CancellationToken cancellationToken)
    {
        var spaces = await _db.SmartHomeSpaces
            .Where(x => x.TenantId == tenantId && x.DeletedAt == null)
            .ToListAsync(cancellationToken);
        var spaceIds = spaces.Select(x => x.Id).ToHashSet();
        var devices = await _db.SmartHomeDevices
            .Where(x => x.TenantId == tenantId && x.DeletedAt == null && x.SpaceId != null && spaceIds.Contains(x.SpaceId.Value))
            .ToListAsync(cancellationToken);
        var deviceIds = devices.Select(x => x.Id).ToArray();
        var capabilities = await _db.DeviceCapabilities
            .Where(x => deviceIds.Contains(x.DeviceId) && x.DeletedAt == null && x.IsWritable)
            .ToListAsync(cancellationToken);
        return new DeviceContext(spaces.ToDictionary(x => x.Id), devices, capabilities);
    }

    /// <summary>复验运行权限快照：快照缺失视为存量运行放行；personal 实例仅快照所有者可执行动作；household 逐条复验连接器授权。</summary>
    private async Task<bool> IsSnapshotAuthorizedAsync(AgentRun run, long userId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(run.PermissionSnapshot)) return true;
        RunPermissionSnapshot? snapshot;
        try
        {
            snapshot = JsonSerializer.Deserialize<RunPermissionSnapshot>(run.PermissionSnapshot, JsonOptions);
        }
        catch (JsonException)
        {
            return true;
        }
        if (snapshot is null) return true;

        if (snapshot.BindingScope == "personal")
            return snapshot.OwnerUserId == userId;

        var connectorIds = snapshot.ConnectorGrants?
            .Where(x => x is not null && x.ConnectorId > 0)
            .Select(x => x!.ConnectorId)
            .Distinct()
            .ToArray() ?? [];
        if (connectorIds.Length == 0) return true;

        var connectors = await _db.WorkspaceConnectors
            .Where(x => x.TenantId == run.TenantId && connectorIds.Contains(x.Id) && x.DeletedAt == null)
            .ToListAsync(cancellationToken);
        if (connectors.Count != connectorIds.Length) return false;
        foreach (var connector in connectors)
        {
            if (connector.BindingScope == "personal" && connector.OwnerUserId != userId) return false;
            var granted = await _db.UserConnectorAuthorizations.AnyAsync(x =>
                x.TenantId == run.TenantId && x.UserId == userId && x.WorkspaceConnectorId == connector.Id && x.DeletedAt == null, cancellationToken);
            if (!granted) return false;
        }
        return true;
    }

    /// <summary>重放既有确认结果：同一幂等键仅返回首次执行结果，不重复执行。</summary>
    private static ServiceResult ReplayActionResult(ExpertRunAction action, ActionExecutionAudit audit)
    {
        var succeeded = audit.Status == "executed";
        return new ServiceResult(succeeded ? 200 : audit.Status == "executing" ? 202 : 502, succeeded ? "场景执行已完成。" : "场景动作正在处理或已执行失败。", new { actionId = action.Id, status = action.Status, message = succeeded ? "场景执行已完成。" : "场景动作正在处理或已执行失败。" });
    }

    /// <summary>生成面向用户的场景执行摘要；失败步骤按名称与原因列出。</summary>
    private static string BuildSummary(string scenarioName, string scenarioStatus, int successCount, int failedCount, IReadOnlyList<FailedStep> failedSteps)
    {
        if (scenarioStatus == "success")
            return failedCount == 0
                ? $"场景「{scenarioName}」执行完成：{successCount} 项全部成功。"
                : $"场景「{scenarioName}」执行完成：{successCount} 项成功，{failedCount} 项可选步骤未生效。";
        if (scenarioStatus == "failed")
            return $"场景「{scenarioName}」执行失败：{failedCount} 项步骤均未成功。";
        var detail = string.Join("，", failedSteps.Select(x => $"{x.Name}（{x.Reason ?? "未知原因"}）"));
        return $"场景「{scenarioName}」执行完成：{successCount} 项成功，{failedCount} 项失败（{detail}）。";
    }

    /// <summary>计算单步风险等级：门锁/摄像头/安防类设备或 lock 能力为 L3，其余 L1。</summary>
    private static string StepRisk(string deviceType, string capability) =>
        HighRiskDeviceTypes.Contains(deviceType) || HighRiskCapabilities.Contains(capability) ? ConfirmationRiskLevel.L3 : ConfirmationRiskLevel.L1;

    /// <summary>校验连接器授权 scope 是否覆盖目标权限；支持通配符段。</summary>
    private static bool ScopeAllows(string scopeJson, string requiredScope)
    {
        try
        {
            return (JsonSerializer.Deserialize<string[]>(scopeJson) ?? []).Any(scope =>
            {
                var granted = scope.Split('.');
                var required = requiredScope.Split('.');
                return granted.Length == required.Length && granted.Zip(required).All(pair => pair.First == "*" || pair.First == pair.Second);
            });
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>解析模板步骤 JSON；解析失败或字段缺失时返回空列表。</summary>
    private static IReadOnlyList<TemplateStepData> ReadTemplateSteps(string stepsJson)
    {
        try
        {
            using var document = JsonDocument.Parse(stepsJson);
            if (document.RootElement.ValueKind != JsonValueKind.Array) return [];
            var steps = new List<TemplateStepData>();
            foreach (var element in document.RootElement.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object) continue;
                var id = ReadString(element, "id");
                var name = ReadString(element, "name");
                var deviceType = ReadString(element, "device_type");
                var room = ReadString(element, "room");
                var capability = ReadString(element, "capability");
                var optional = ReadValue(element, "optional") is { } optionalElement && optionalElement.ValueKind == JsonValueKind.True;
                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(deviceType) ||
                    string.IsNullOrWhiteSpace(room) || string.IsNullOrWhiteSpace(capability) || ReadValue(element, "value") is not { } valueElement)
                    continue;
                steps.Add(new TemplateStepData(id, name, deviceType, room, capability, valueElement.Clone(), optional));
            }
            return steps;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>解析实例步骤 JSON；解析失败时返回空列表。</summary>
    private static IReadOnlyList<InstanceStepData> ReadSteps(string stepsJson)
    {
        try
        {
            using var document = JsonDocument.Parse(stepsJson);
            if (document.RootElement.ValueKind != JsonValueKind.Array) return [];
            var steps = new List<InstanceStepData>();
            foreach (var element in document.RootElement.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object) continue;
                var id = ReadString(element, "id");
                var name = ReadString(element, "name");
                var deviceType = ReadString(element, "device_type");
                var room = ReadString(element, "room");
                var capability = ReadString(element, "capability");
                var optional = ReadValue(element, "optional") is { } optionalElement && optionalElement.ValueKind == JsonValueKind.True;
                var stepStatus = ReadString(element, "step_status") ?? ScenarioStepStatus.Unavailable;
                var reason = ReadString(element, "reason");
                var deviceId = ReadValue(element, "device_id") is { ValueKind: JsonValueKind.Number } deviceElement && deviceElement.TryGetInt64(out var parsedDeviceId)
                    ? parsedDeviceId
                    : (long?)null;
                var value = ReadValue(element, "value") is { } valueElement ? valueElement.Clone() : JsonSerializer.SerializeToElement(new { });
                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(deviceType) ||
                    string.IsNullOrWhiteSpace(room) || string.IsNullOrWhiteSpace(capability))
                    continue;
                steps.Add(new InstanceStepData(id, name, deviceType, room, deviceId, capability, value, optional, stepStatus, reason));
            }
            return steps;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>解析运行动作的场景 metadata；解析失败返回 null。</summary>
    private static ScenarioMetadata? ReadScenario(string requestJson)
    {
        try
        {
            using var document = JsonDocument.Parse(requestJson);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || ReadValue(root, "scenario_id") is not { } idElement || !idElement.TryGetInt64(out var scenarioId)) return null;
            var scenarioName = ReadString(root, "scenario_name") ?? "";
            var steps = new List<ScenarioStepData>();
            if (ReadValue(root, "steps") is { ValueKind: JsonValueKind.Array } stepsElement)
            {
                foreach (var element in stepsElement.EnumerateArray())
                {
                    if (element.ValueKind != JsonValueKind.Object) continue;
                    var id = ReadString(element, "id");
                    var name = ReadString(element, "name");
                    var deviceType = ReadString(element, "device_type");
                    var room = ReadString(element, "room");
                    var capability = ReadString(element, "capability");
                    var optional = ReadValue(element, "optional") is { } optionalElement && optionalElement.ValueKind == JsonValueKind.True;
                    var status = ReadString(element, "status") ?? "pending";
                    var reason = ReadString(element, "reason");
                    var deviceId = ReadValue(element, "device_id") is { ValueKind: JsonValueKind.Number } deviceElement && deviceElement.TryGetInt64(out var parsedDeviceId)
                        ? parsedDeviceId
                        : (long?)null;
                    var value = ReadValue(element, "value") is { } valueElement ? valueElement.Clone() : JsonSerializer.SerializeToElement(new { });
                    if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(deviceType) ||
                        string.IsNullOrWhiteSpace(room) || string.IsNullOrWhiteSpace(capability))
                        continue;
                    var stepData = new ScenarioStepData(id, name, deviceType, room, deviceId, capability, value, optional)
                    {
                        Status = status,
                        Reason = reason
                    };
                    steps.Add(stepData);
                }
            }
            return new ScenarioMetadata(scenarioId, scenarioName, steps);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>按蛇形键读取 JSON 属性字符串值；兼容 System.Text.Json 驼峰序列化形态。</summary>
    private static string? ReadString(JsonElement element, string snakeName) => ReadValue(element, snakeName) is { ValueKind: JsonValueKind.String } value ? value.GetString() : null;

    /// <summary>按蛇形键读取 JSON 属性值；兼容 System.Text.Json 驼峰序列化形态。</summary>
    private static JsonElement? ReadValue(JsonElement element, string snakeName)
    {
        if (element.TryGetProperty(snakeName, out var value)) return value;
        var parts = snakeName.Split('_');
        var camelName = parts.Length == 1 ? parts[0] : parts[0] + string.Concat(parts.Skip(1).Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
        return element.TryGetProperty(camelName, out value) ? value : null;
    }

    /// <summary>将实例步骤转换为运行动作 metadata 中的步骤（不含 step_status，以运行期 status 表示）。</summary>
    private static object ToMetadataStep(InstanceStepData step) => new
    {
        step.Id,
        step.Name,
        step.DeviceType,
        step.Room,
        step.DeviceId,
        step.Capability,
        step.Value,
        step.Optional,
        status = step.StepStatus == ScenarioStepStatus.Ready ? "pending" : "skipped"
    };

    private static object ToMetadataStep(ScenarioStepData step) => new
    {
        step.Id,
        step.Name,
        step.DeviceType,
        step.Room,
        step.DeviceId,
        step.Capability,
        step.Value,
        step.Optional,
        step.Status,
        step.Reason
    };

    private static ScenarioTemplateView ToTemplateView(ScenarioTemplate template) =>
        new(template.Id, template.Code, template.Name, template.Summary, template.Status,
            ReadTemplateSteps(template.Steps).Select(x => new ScenarioTemplateStepView(x.Id, x.Name, x.DeviceType, x.Room, x.Capability, x.Value, x.Optional)).ToArray());

    private static ScenarioInstanceView ToInstanceView(ScenarioInstance instance) =>
        new(instance.Id, instance.TemplateCode, instance.Name, instance.Status,
            ReadSteps(instance.Steps).Select(x => new ScenarioInstanceStepView(x.Id, x.Name, x.DeviceType, x.Room, x.DeviceId, x.Capability, x.Optional, x.StepStatus, x.Reason)).ToArray(),
            instance.CreatedAt);

    private async Task<ScenarioRunView> ToViewAsync(AgentRun run, CancellationToken cancellationToken)
    {
        var events = await _db.RunEvents
            .Where(x => x.RunId == run.Id && x.TenantId == run.TenantId)
            .OrderBy(x => x.Sequence)
            .ToListAsync(cancellationToken);
        var actions = await _db.ExpertRunActions
            .Where(x => x.RunId == run.Id && x.TenantId == run.TenantId && x.ActionType == "scenario")
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);
        return new ScenarioRunView(
            run.Id,
            run.Status,
            run.ResultSummary,
            run.CreatedAt,
            run.FinishedAt,
            events.Select(x => new ScenarioRunEventView(x.Sequence, x.EventType, ReadMessage(x.Payload), x.CreatedAt)).ToArray(),
            actions.Select(ToActionView).ToArray());
    }

    /// <summary>从动作 metadata 读取标题、说明与风险等级；内容非法时回退为默认值。</summary>
    private static ScenarioActionView ToActionView(ExpertRunAction action)
    {
        var metadata = ReadScenario(action.RequestJson);
        var title = metadata?.ScenarioName ?? "场景";
        var steps = metadata?.Steps ?? [];
        var risk = steps.Count == 0 ? ConfirmationRiskLevel.L1 : steps.Max(x => StepRisk(x.DeviceType, x.Capability));
        var description = metadata is null ? "场景动作。" : $"共 {steps.Count} 个设备步骤，风险等级 {risk}。";
        return new ScenarioActionView(action.Id, action.ActionType, action.Status, title, description, risk);
    }

    private void AddEvent(AgentRun run, int sequence, string type, string message, DateTime createdAt) =>
        _db.RunEvents.Add(new RunEvent
        {
            TenantId = run.TenantId,
            RunId = run.Id,
            Sequence = sequence,
            EventType = type,
            Payload = JsonSerializer.Serialize(new { message }),
            CreatedAt = createdAt
        });

    private async Task<int> NextSequenceAsync(long runId, CancellationToken cancellationToken) =>
        (await _db.RunEvents.Where(x => x.RunId == runId).MaxAsync(x => (int?)x.Sequence, cancellationToken) ?? 0) + 1;

    private static string ReadMessage(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        return document.RootElement.TryGetProperty("message", out var message) ? message.GetString() ?? "" : "";
    }

    private sealed record DeviceContext(IReadOnlyDictionary<long, SmartHomeSpace> Spaces, IReadOnlyList<SmartHomeDevice> Devices, IReadOnlyList<DeviceCapability> Capabilities);
    private sealed record TemplateStepData(string Id, string Name, string DeviceType, string Room, string Capability, JsonElement Value, bool Optional);
    private sealed record InstanceStepData(string Id, string Name, string DeviceType, string Room, long? DeviceId, string Capability, JsonElement Value, bool Optional, string StepStatus, string? Reason);
    private sealed record ScenarioStepData(string Id, string Name, string DeviceType, string Room, long? DeviceId, string Capability, JsonElement Value, bool Optional)
    {
        /// <summary>运行期步骤状态：pending / success / failed / timeout / skipped。</summary>
        public string Status { get; set; } = "pending";
        /// <summary>失败原因，仅失败步骤非空。</summary>
        public string? Reason { get; set; }
    }
    private sealed record ScenarioMetadata(long ScenarioId, string ScenarioName, IReadOnlyList<ScenarioStepData> Steps);
    private sealed record StepResult(bool Succeeded, string Status, string? ErrorCode, string? Message);
    private sealed record FailedStep(string Name, string? Reason);
    private sealed record RunPermissionSnapshot(string? BindingScope, long? OwnerUserId, IReadOnlyList<RunConnectorGrant>? ConnectorGrants);
    private sealed record RunConnectorGrant(long ConnectorId);
}
