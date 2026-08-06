using System.Text.Json;
using HomeMind.Business.IServices.AI;
using HomeMind.Business.IServices.Connector;
using HomeMind.Business.IServices.SmartHome;
using HomeMind.Business.Services.Connectors.Adapters;
using HomeMind.Business.Services.Connectors.Bridge;
using HomeMind.Business.Services.SmartHome;
using HomeMind.Common.Model.Entities;
using HomeMind.Common.Model.Entities.SmartHome;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.AI;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;

namespace HomeMind.Business.Services.AI;

/// <summary>
/// Deterministic V1 housekeeper orchestration. It reads only the normalized
/// SmartHome model and persists suggestions; it never resolves or calls a connector.
/// 设备命令经 CommandRelayService 转发，确认、幂等与审计链路不被绕过。
/// </summary>
public sealed class HousekeeperRunServices : IHousekeeperRunServices
{
    private static readonly HashSet<string> AllowedIntents = new(StringComparer.Ordinal)
    {
        "sleep", "away", "arrive", "environment_review"
    };

    private readonly HomeMindDbContext _db;
    private readonly CommandRelayService _relay;

    public HousekeeperRunServices(HomeMindDbContext db, CommandRelayService relay)
    {
        _db = db;
        _relay = relay;
    }

    public async Task<ServiceResult> CreateAsync(long userId, long tenantId, HousekeeperRunRequest request, CancellationToken cancellationToken = default)
    {
        var intent = request.Intent?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(intent) || !AllowedIntents.Contains(intent))
        {
            return new ServiceResult(422, "仅支持 sleep、away、arrive 或 environment_review 家庭管家意图。");
        }

        if (request.SpaceId is not null && !await _db.SmartHomeSpaces.AnyAsync(x => x.Id == request.SpaceId && x.TenantId == tenantId && x.DeletedAt == null, cancellationToken))
        {
            return new ServiceResult(404, "请求的家庭空间不存在。");
        }

        var version = await FindHousekeeperVersionAsync(cancellationToken);
        if (version is null)
        {
            return new ServiceResult(503, "家庭管家专家尚未初始化，请先应用数据库迁移 009。");
        }

        var idempotencyKey = Guid.TryParse(request.IdempotencyKey, out var parsedKey) ? parsedKey.ToString() : Guid.NewGuid().ToString();
        var existing = await _db.AgentRuns.SingleOrDefaultAsync(
            x => x.TenantId == tenantId && x.UserId == userId && x.RequestIdempotencyKey == idempotencyKey,
            cancellationToken);
        if (existing is not null)
        {
            if (existing.ExpertVersionId != version.Id)
            {
                return new ServiceResult(409, "该幂等键已用于其他专家运行。");
            }

            return new ServiceResult(200, "家庭管家运行已存在。", await ToViewAsync(existing, cancellationToken));
        }

        var now = DateTime.UtcNow;
        var run = new AgentRun
        {
            TenantId = tenantId,
            UserId = userId,
            SourceType = "expert",
            ExpertVersionId = version.Id,
            RequestIdempotencyKey = idempotencyKey,
            Input = JsonSerializer.Serialize(new { intent, spaceId = request.SpaceId }),
            Status = "planning",
            Mode = HousekeeperRunPolicies.Steward,
            AutoConfirmPolicy = HousekeeperRunPolicies.L3Only,
            EstimatedCredits = version.EstimatedCredits,
            StartedAt = now,
            CreatedAt = now
        };
        _db.AgentRuns.Add(run);
        await _db.SaveChangesAsync(cancellationToken);

        var context = await ReadContextAsync(tenantId, request.SpaceId, cancellationToken);
        AddEvent(run, 1, "running", "正在收集已同步的家庭状态。", now);
        AddEvent(run, 2, "context_collected", $"已检查 {context.Spaces.Count} 个空间和 {context.Devices.Count} 台设备。", now);

        var drafts = BuildDrafts(intent, context).Take(12).ToList();
        foreach (var draft in drafts)
        {
            _db.ExpertRunActions.Add(new ExpertRunAction
            {
                RunId = run.Id,
                TenantId = tenantId,
                UserId = userId,
                ActionType = "smart_home_device",
                RequestIdempotencyKey = Guid.NewGuid().ToString(),
                RequestJson = JsonSerializer.Serialize(draft),
                Status = "pending",
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        run.Status = "completed";
        run.FinishedAt = now;
        run.ResultSummary = drafts.Count == 0
            ? "已完成家庭状态分析，当前没有需要确认的设备行动。"
            : $"已完成家庭状态分析，并生成 {drafts.Count} 个待确认行动。";
        run.Result = JsonSerializer.Serialize(new { intent, analyzedSpaceCount = context.Spaces.Count, analyzedDeviceCount = context.Devices.Count, pendingActionCount = drafts.Count });
        AddEvent(run, 3, drafts.Count == 0 ? "completed" : "pending_actions", run.ResultSummary, now);
        await _db.SaveChangesAsync(cancellationToken);

        return new ServiceResult(201, "家庭管家分析完成。", await ToViewAsync(run, cancellationToken));
    }

    public async Task<ServiceResult> GetActionsAsync(long userId, long tenantId, long runId, CancellationToken cancellationToken = default)
    {
        var run = await _db.AgentRuns.SingleOrDefaultAsync(x => x.Id == runId && x.TenantId == tenantId && x.UserId == userId, cancellationToken);
        return run is null
            ? new ServiceResult(404, "请求的运行不存在。")
            : new ServiceResult(200, "查询成功。", await ToViewAsync(run, cancellationToken));
    }

    public async Task<ServiceResult> ConfirmActionAsync(long userId, long tenantId, long runId, long actionId, ConfirmHousekeeperActionRequest request, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(request.IdempotencyKey, out var parsedIdempotencyKey))
            return new ServiceResult(422, "确认设备行动时必须提供有效的幂等键。");

        var action = await _db.ExpertRunActions.SingleOrDefaultAsync(x =>
            x.Id == actionId && x.RunId == runId && x.TenantId == tenantId && x.UserId == userId && x.ActionType == "smart_home_device", cancellationToken);
        if (action is null) return new ServiceResult(404, "请求的家庭管家行动不存在。");

        var idempotencyKey = parsedIdempotencyKey.ToString();
        var previous = await _db.ActionExecutionAudits.SingleOrDefaultAsync(
            x => x.RunActionId == action.Id && x.IdempotencyKey == idempotencyKey, cancellationToken);
        if (previous is not null) return ReplayResult(action, previous);
        if (action.Status != "pending") return new ServiceResult(409, "该设备行动已经确认或处理完成，不能再次执行。");

        var draft = ReadDraft(action.RequestJson);
        if (draft is null) return new ServiceResult(422, "设备行动草案格式无效。");

        var context = await LoadExecutionContextAsync(userId, tenantId, draft, cancellationToken);
        if (context.Error is not null) return context.Error;

        var now = DateTime.UtcNow;
        var reference = new ConnectorReference(context.Connector!.Id, tenantId, context.Connector.CredentialRef!);
        var health = await _relay.TestConnectionAsync(context.ProviderCode!, reference, cancellationToken);
        context.Connector.LastHealthAt = now;
        context.Connector.Status = health.Succeeded ? "connected" : "failed";
        context.Connector.UpdatedAt = now;
        if (!health.Succeeded)
        {
            await _db.SaveChangesAsync(cancellationToken);
            return new ServiceResult(IsConfigurationError(health.ErrorCode) ? 503 : 502, health.Message ?? "连接器健康检查失败。");
        }

        action.Status = "executing";
        action.UpdatedAt = now;
        var audit = new ActionExecutionAudit
        {
            TenantId = tenantId,
            RunActionId = action.Id,
            OperatorUserId = userId,
            WorkspaceConnectorId = context.Connector!.Id,
            DeviceId = context.Device!.Id,
            IdempotencyKey = idempotencyKey,
            Status = "executing",
            Command = JsonSerializer.Serialize(new { deviceId = context.Device.Id, draft.Capability, targetValue = draft.TargetValue }),
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.ActionExecutionAudits.Add(audit);
        AddEvent(await _db.AgentRuns.SingleAsync(x => x.Id == runId, cancellationToken), await NextSequenceAsync(runId, cancellationToken), "action_confirmed", $"已确认设备行动：{draft.Title}。", now);
        await _db.SaveChangesAsync(cancellationToken);

        DeviceCommandResult result;
        try
        {
            using var targetDocument = JsonDocument.Parse(draft.TargetValue.GetRawText());
            result = await _relay.ExecuteAsync(
                context.ProviderCode!,
                reference,
                new DeviceCommand(context.Connector.Id, context.Device.Id, draft.Capability, targetDocument.RootElement.Clone(), userId, action.Id, idempotencyKey),
                cancellationToken);
        }
        catch (ConnectorAdapterException error)
        {
            result = new DeviceCommandResult(false, "failed", error.ErrorCode, error.Message);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            result = new DeviceCommandResult(false, "failed", "timeout", "设备行动执行超时。");
        }
        catch (Exception)
        {
            result = new DeviceCommandResult(false, "failed", "execution_error", "设备行动执行失败。");
        }

        now = DateTime.UtcNow;
        action.Status = result.Succeeded ? "executed" : "failed";
        action.Result = JsonSerializer.Serialize(new { status = action.Status, errorCode = result.ErrorCode });
        action.UpdatedAt = now;
        audit.Status = action.Status;
        audit.Result = JsonSerializer.Serialize(new { status = action.Status, errorCode = result.ErrorCode });
        audit.UpdatedAt = now;
        if (result.Succeeded)
        {
            _db.DeviceStates.Add(new DeviceState
            {
                DeviceId = context.Device.Id,
                State = await MergeStateAsync(context.Device.Id, draft.Capability, draft.TargetValue, cancellationToken),
                SampledAt = now,
                CreatedAt = now
            });
            context.Device.LastSeenAt = now;
            context.Device.UpdatedAt = now;
        }

        var run = await _db.AgentRuns.SingleAsync(x => x.Id == runId, cancellationToken);
        AddEvent(run, await NextSequenceAsync(runId, cancellationToken), result.Succeeded ? "action_executed" : "action_failed", result.Succeeded ? $"设备行动已执行：{draft.Title}。" : $"设备行动执行失败：{draft.Title}。", now);
        await _db.SaveChangesAsync(cancellationToken);

        var view = new HousekeeperActionExecutionView(action.Id, action.Status, result.Succeeded ? "设备行动已执行。" : result.Message ?? "设备行动执行失败。", action.UpdatedAt);
        return result.Succeeded
            ? new ServiceResult(200, "设备行动已执行。", view)
            : new ServiceResult(IsConfigurationError(result.ErrorCode) ? 503 : 502, result.Message ?? "设备行动执行失败。", view);
    }

    private async Task<ExpertVersion?> FindHousekeeperVersionAsync(CancellationToken cancellationToken) =>
        await (from expert in _db.Experts
               join version in _db.ExpertVersions on expert.Id equals version.ExpertId
               where expert.TenantId == 1 && expert.Code == "family-housekeeper" && expert.Status == "active" && version.Status == "published"
               orderby version.Version descending
               select version).FirstOrDefaultAsync(cancellationToken);

    private async Task<ReadContext> ReadContextAsync(long tenantId, long? spaceId, CancellationToken cancellationToken)
    {
        var spaces = await _db.SmartHomeSpaces
            .Where(x => x.TenantId == tenantId && x.DeletedAt == null && (spaceId == null || x.Id == spaceId))
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);
        var spaceIds = spaces.Select(x => x.Id).ToArray();
        var devices = await _db.SmartHomeDevices
            .Where(x => x.TenantId == tenantId && x.DeletedAt == null && x.SpaceId != null && spaceIds.Contains(x.SpaceId.Value))
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);
        var deviceIds = devices.Select(x => x.Id).ToArray();
        var capabilities = await _db.DeviceCapabilities
            .Where(x => deviceIds.Contains(x.DeviceId) && x.DeletedAt == null && x.IsWritable)
            .ToListAsync(cancellationToken);
        return new ReadContext(spaces, devices, capabilities);
    }

    private static IEnumerable<ActionDraft> BuildDrafts(string intent, ReadContext context)
    {
        var spaces = context.Spaces.ToDictionary(x => x.Id);
        var devices = context.Devices.Where(x =>
            spaces.ContainsKey(x.SpaceId!.Value) &&
            x.OnlineStatus == "online" &&
            x.LastSeenAt != null);

        IEnumerable<SmartHomeDevice> candidates = intent switch
        {
            "sleep" => devices.Where(x => spaces[x.SpaceId!.Value].SpaceType.Contains("bedroom", StringComparison.OrdinalIgnoreCase)),
            "arrive" => devices.Where(x => spaces[x.SpaceId!.Value].SpaceType.Contains("living", StringComparison.OrdinalIgnoreCase)),
            "away" => devices,
            _ => Array.Empty<SmartHomeDevice>()
        };

        foreach (var device in candidates)
        {
            var capabilities = context.Capabilities.Where(x => x.DeviceId == device.Id).ToArray();
            if (intent == "sleep" && device.DeviceType == "light" && HasCapability(capabilities, "power"))
            {
                yield return Draft(device, "power", false, "关闭卧室照明", "睡眠准备建议关闭卧室照明。");
            }
            if (intent == "sleep" && device.DeviceType == "air_conditioner" && HasCapability(capabilities, "temperature"))
            {
                yield return Draft(device, "temperature", 26, "调整卧室空调温度", "睡眠准备建议将卧室空调设为 26 C。");
            }
            if (intent == "away" &&
                device.DeviceType is "light" or "switch" &&
                HasCapability(capabilities, "power"))
            {
                yield return Draft(device, "power", false, "关闭非必要设备", "离家建议关闭该设备。");
            }
            if (intent == "arrive" && device.DeviceType == "light" && HasCapability(capabilities, "power"))
            {
                yield return Draft(device, "power", true, "开启回家照明", "回家建议开启客厅照明。");
            }
        }
    }

    private static bool HasCapability(IEnumerable<DeviceCapability> capabilities, string name) =>
        capabilities.Any(x => x.Capability == name);

    private static ActionDraft Draft(SmartHomeDevice device, string capability, object targetValue, string title, string description) =>
        new(title, description, device.Id, device.Name, capability, JsonSerializer.SerializeToElement(targetValue));

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

    private async Task<HousekeeperRunView> ToViewAsync(AgentRun run, CancellationToken cancellationToken)
    {
        var events = await _db.RunEvents
            .Where(x => x.RunId == run.Id && x.TenantId == run.TenantId)
            .OrderBy(x => x.Sequence)
            .ToListAsync(cancellationToken);
        var actions = await _db.ExpertRunActions
            .Where(x => x.RunId == run.Id && x.TenantId == run.TenantId && x.ActionType == "smart_home_device")
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);

        return new HousekeeperRunView(
            run.Id,
            run.Status,
            run.ResultSummary,
            run.CreatedAt,
            run.FinishedAt,
            events.Select(x => new HousekeeperRunEventView(x.Sequence, x.EventType, ReadMessage(x.Payload), x.CreatedAt)).ToArray(),
            actions.Select(ToActionView).ToArray(),
            run.Mode,
            run.AutoConfirmPolicy);
    }

    private static HousekeeperRunActionView ToActionView(ExpertRunAction action)
    {
        var draft = ReadDraft(action.RequestJson)
            ?? throw new InvalidOperationException("家庭管家行动草案格式无效。");
        return new HousekeeperRunActionView(action.Id, action.ActionType, action.Status, draft.Title, draft.Description, draft.DeviceId, draft.DeviceName, draft.Capability, draft.TargetValue);
    }

    private async Task<ExecutionContext> LoadExecutionContextAsync(long userId, long tenantId, ActionDraft draft, CancellationToken cancellationToken)
    {
        var device = await _db.SmartHomeDevices.SingleOrDefaultAsync(x => x.Id == draft.DeviceId && x.TenantId == tenantId && x.DeletedAt == null, cancellationToken);
        if (device is null || device.WorkspaceConnectorId is null) return ExecutionContext.Failure(404, "设备不存在或未关联连接器。");
        if (device.OnlineStatus != "online") return ExecutionContext.Failure(409, "设备当前离线，不能执行该行动。");
        var capability = await _db.DeviceCapabilities.SingleOrDefaultAsync(x => x.DeviceId == device.Id && x.Capability == draft.Capability && x.DeletedAt == null && x.IsWritable, cancellationToken);
        if (capability is null || !TargetMatchesSchema(draft.TargetValue, capability.ValueSchema)) return ExecutionContext.Failure(422, "设备能力或目标值已失效，不能执行该行动。");
        var connector = await _db.WorkspaceConnectors.SingleOrDefaultAsync(x => x.Id == device.WorkspaceConnectorId && x.TenantId == tenantId && x.DeletedAt == null, cancellationToken);
        if (connector is null || connector.Status != "connected" || connector.LastHealthAt is null || string.IsNullOrWhiteSpace(connector.CredentialRef)) return ExecutionContext.Failure(409, "连接器当前不可用，请先完成连接测试。");
        var authorized = await _db.UserConnectorAuthorizations.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.UserId == userId && x.WorkspaceConnectorId == connector.Id && x.DeletedAt == null, cancellationToken);
        if (authorized is null || !ScopeAllows(authorized.Scope, capability.Permission)) return ExecutionContext.Failure(403, "当前成员未获该设备能力的执行授权。");
        var providerCode = await _db.ConnectorProviders.Where(x => x.Id == connector.ConnectorProviderId && x.DeletedAt == null && x.Status == "active").Select(x => x.Code).SingleOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(providerCode) || !_relay.SupportsProvider(providerCode)) return ExecutionContext.Failure(501, "该连接器尚未提供设备行动适配器。");
        return new ExecutionContext(device, connector, providerCode, null);
    }

    private async Task<int> NextSequenceAsync(long runId, CancellationToken cancellationToken) =>
        (await _db.RunEvents.Where(x => x.RunId == runId).MaxAsync(x => (int?)x.Sequence, cancellationToken) ?? 0) + 1;

    private async Task<string> MergeStateAsync(long deviceId, string capability, JsonElement targetValue, CancellationToken cancellationToken)
    {
        var state = await _db.DeviceStates
            .Where(x => x.DeviceId == deviceId)
            .OrderByDescending(x => x.SampledAt).ThenByDescending(x => x.Id)
            .Select(x => x.State)
            .FirstOrDefaultAsync(cancellationToken);
        var values = new Dictionary<string, JsonElement>();
        if (!string.IsNullOrWhiteSpace(state))
        {
            try
            {
                using var document = JsonDocument.Parse(state);
                if (document.RootElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var item in document.RootElement.EnumerateObject()) values[item.Name] = item.Value.Clone();
                }
            }
            catch (JsonException) { }
        }

        values[capability] = targetValue.Clone();
        return JsonSerializer.Serialize(values);
    }

    private static ActionDraft? ReadDraft(string requestJson)
    {
        try { return JsonSerializer.Deserialize<ActionDraft>(requestJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
        catch (JsonException) { return null; }
    }

    private static bool TargetMatchesSchema(JsonElement value, string schema)
    {
        try
        {
            using var document = JsonDocument.Parse(schema);
            if (!document.RootElement.TryGetProperty("type", out var type)) return false;
            return type.GetString() switch
            {
                "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
                "integer" => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _),
                "number" => value.ValueKind == JsonValueKind.Number,
                "string" => value.ValueKind == JsonValueKind.String,
                _ => false
            };
        }
        catch (JsonException) { return false; }
    }

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
        catch (JsonException) { return false; }
    }

    private static bool IsConfigurationError(string? errorCode) => errorCode is "secret_vault_unavailable" or "invalid_secret" or "not_available";

    private static ServiceResult ReplayResult(ExpertRunAction action, ActionExecutionAudit audit)
    {
        var succeeded = audit.Status == "executed";
        var view = new HousekeeperActionExecutionView(action.Id, action.Status, succeeded ? "设备行动已执行。" : "设备行动正在处理或已执行失败。", audit.UpdatedAt);
        return new ServiceResult(succeeded ? 200 : audit.Status == "executing" ? 202 : 502, view.Message, view);
    }

    private static string ReadMessage(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        return document.RootElement.TryGetProperty("message", out var message) ? message.GetString() ?? "" : "";
    }

    private sealed record ReadContext(IReadOnlyList<SmartHomeSpace> Spaces, IReadOnlyList<SmartHomeDevice> Devices, IReadOnlyList<DeviceCapability> Capabilities);
    private sealed record ActionDraft(string Title, string Description, long DeviceId, string DeviceName, string Capability, JsonElement TargetValue);
    private sealed record ExecutionContext(SmartHomeDevice? Device, WorkspaceConnector? Connector, string? ProviderCode, ServiceResult? Error)
    {
        public static ExecutionContext Failure(int statusCode, string message) => new(null, null, null, new ServiceResult(statusCode, message));
    }
}
