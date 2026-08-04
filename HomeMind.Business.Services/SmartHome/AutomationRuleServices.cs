using System.Globalization;
using System.Text.Json;
using HomeMind.Business.IServices.AI;
using HomeMind.Business.IServices.SmartHome;
using HomeMind.Common.Model.Entities.SmartHome;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.AI;
using HomeMind.Common.Model.ViewModel.Data.SmartHome;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HomeMind.Business.Services.SmartHome;

/// <summary>Evaluates authorized rules and delegates effects to the audited housekeeper workflow.</summary>
public sealed class AutomationRuleServices : IAutomationRuleServices
{
    private static readonly HashSet<string> TriggerTypes = new(StringComparer.Ordinal)
    {
        "time_schedule", "device_state_change", "scene_completed", "sync_completed"
    };
    private static readonly HashSet<string> ApprovalPolicies = new(StringComparer.Ordinal)
    {
        "manual_confirmation", "auto_execute"
    };

    private readonly HomeMindDbContext _db;
    private readonly IHousekeeperRunServices _housekeeperRuns;
    private readonly ILogger<AutomationRuleServices> _logger;

    public AutomationRuleServices(HomeMindDbContext db, IHousekeeperRunServices housekeeperRuns, ILogger<AutomationRuleServices> logger)
    {
        _db = db;
        _housekeeperRuns = housekeeperRuns;
        _logger = logger;
    }

    public async Task<ServiceResult> ListAsync(long tenantId, CancellationToken cancellationToken = default)
    {
        var rules = await _db.AutomationRules.Where(x => x.TenantId == tenantId)
            .OrderByDescending(x => x.UpdatedAt).ThenByDescending(x => x.Id).ToListAsync(cancellationToken);
        return new ServiceResult(200, "查询成功。", rules.Select(ToView).ToArray());
    }

    public async Task<ServiceResult> CreateAsync(long userId, long tenantId, AutomationRuleRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await ValidateCreateAsync(tenantId, request, cancellationToken);
        if (validation.Error is not null) return validation.Error;
        var now = DateTime.UtcNow;
        var rule = new AutomationRule
        {
            TenantId = tenantId,
            OwnerUserId = userId,
            Name = request.Name!.Trim(),
            TriggerType = request.TriggerType!.Trim().ToLowerInvariant(),
            TriggerConfig = validation.Trigger!,
            Conditions = validation.Conditions!,
            Actions = validation.Actions!,
            ApprovalPolicy = request.ApprovalPolicy?.Trim().ToLowerInvariant() ?? "manual_confirmation",
            Enabled = request.Enabled ?? true,
            CreatedAt = now,
            UpdatedAt = now,
            RowVersion = 1
        };
        _db.AutomationRules.Add(rule);
        await _db.SaveChangesAsync(cancellationToken);
        return new ServiceResult(201, "自动化规则已创建。", ToView(rule));
    }

    public async Task<ServiceResult> UpdateAsync(long userId, long tenantId, long ruleId, UpdateAutomationRuleRequest request, CancellationToken cancellationToken = default)
    {
        var rule = await _db.AutomationRules.SingleOrDefaultAsync(x => x.Id == ruleId && x.TenantId == tenantId, cancellationToken);
        if (rule is null) return new ServiceResult(404, "请求的自动化规则不存在。");
        if (request.RowVersion != rule.RowVersion) return new ServiceResult(409, "自动化规则已被其他请求更新，请刷新后重试。");
        if (request.Name is { } name)
        {
            if (string.IsNullOrWhiteSpace(name)) return new ServiceResult(422, "规则名称不能为空。");
            rule.Name = name.Trim();
        }
        if (request.ApprovalPolicy is { } policy)
        {
            policy = policy.Trim().ToLowerInvariant();
            if (!ApprovalPolicies.Contains(policy)) return new ServiceResult(422, "approvalPolicy 仅支持 manual_confirmation 或 auto_execute。");
            rule.ApprovalPolicy = policy;
        }
        if (request.Trigger is { } trigger)
        {
            var validated = ValidateTrigger(rule.TriggerType, trigger);
            if (validated.Error is not null) return validated.Error;
            rule.TriggerConfig = validated.Value!;
        }
        if (request.Conditions is { } conditions)
        {
            var validated = await ValidateConditionsAsync(tenantId, conditions, cancellationToken);
            if (validated.Error is not null) return validated.Error;
            rule.Conditions = validated.Value!;
        }
        if (request.Actions is { } actions)
        {
            var validated = ValidateActions(actions);
            if (validated.Error is not null) return validated.Error;
            rule.Actions = validated.Value!;
        }
        if (request.Enabled is not null) rule.Enabled = request.Enabled.Value;
        rule.UpdatedAt = DateTime.UtcNow;
        rule.RowVersion++;
        await _db.SaveChangesAsync(cancellationToken);
        return new ServiceResult(200, "自动化规则已更新。", ToView(rule));
    }

    public Task<ServiceResult> HandleDeviceStateChangeAsync(long tenantId, long deviceId, string state, DateTime occurredAt, CancellationToken cancellationToken = default) =>
        HandleEventAsync(tenantId, "device_state_change", rule => ConfigMatchesDevice(rule.TriggerConfig, deviceId), occurredAt, cancellationToken);

    public Task<ServiceResult> HandleSceneCompletedAsync(long tenantId, string sceneKey, DateTime occurredAt, CancellationToken cancellationToken = default) =>
        HandleEventAsync(tenantId, "scene_completed", rule => ConfigStringEquals(rule.TriggerConfig, "sceneKey", sceneKey), occurredAt, cancellationToken);

    public Task<ServiceResult> HandleSyncCompletedAsync(long tenantId, long connectorId, DateTime occurredAt, CancellationToken cancellationToken = default) =>
        HandleEventAsync(tenantId, "sync_completed", rule => ConfigMatchesConnector(rule.TriggerConfig, connectorId), occurredAt, cancellationToken);

    public async Task<int> ProcessDueSchedulesAsync(DateTime now, CancellationToken cancellationToken = default)
    {
        var rules = await _db.AutomationRules.Where(x => x.Enabled && x.TriggerType == "time_schedule").ToListAsync(cancellationToken);
        var count = 0;
        foreach (var rule in rules)
        {
            if (IsDue(rule, now) && await ExecuteAsync(rule, now, cancellationToken)) count++;
        }
        return count;
    }

    private async Task<ServiceResult> HandleEventAsync(long tenantId, string triggerType, Func<AutomationRule, bool> matches, DateTime occurredAt, CancellationToken cancellationToken)
    {
        var rules = await _db.AutomationRules.Where(x => x.TenantId == tenantId && x.Enabled && x.TriggerType == triggerType).ToListAsync(cancellationToken);
        var executed = 0;
        foreach (var rule in rules.Where(matches)) if (await ExecuteAsync(rule, occurredAt, cancellationToken)) executed++;
        return new ServiceResult(202, "自动化事件已处理。", new { triggeredRuleCount = executed });
    }

    private async Task<bool> ExecuteAsync(AutomationRule rule, DateTime occurredAt, CancellationToken cancellationToken)
    {
        if (!await ConditionsMatchAsync(rule.TenantId, rule.Conditions, cancellationToken)) return false;
        var actions = ReadArray(rule.Actions);
        var succeeded = false;
        foreach (var action in actions)
        {
            if (!action.TryGetProperty("sceneKey", out var sceneKey) || !SmartHomeSceneDefinitions.TryGetIntent(sceneKey.GetString(), out var intent)) continue;
            var create = await _housekeeperRuns.CreateAsync(rule.OwnerUserId, rule.TenantId,
                new HousekeeperRunRequest(intent, null, Guid.NewGuid().ToString()), cancellationToken);
            if (!create.Succeeded || create.Data is not HousekeeperRunView run) continue;
            succeeded = true;
            if (rule.ApprovalPolicy != "auto_execute") continue;
            foreach (var pending in run.Actions.Where(x => x.Status == "pending"))
            {
                await _housekeeperRuns.ConfirmActionAsync(rule.OwnerUserId, rule.TenantId, run.Id, pending.Id,
                    new ConfirmHousekeeperActionRequest(Guid.NewGuid().ToString()), cancellationToken);
            }
        }
        if (!succeeded) return false;
        rule.LastTriggeredAt = occurredAt;
        rule.UpdatedAt = DateTime.UtcNow;
        rule.RowVersion++;
        await _db.SaveChangesAsync(cancellationToken);
        AutomationMetrics.RuleTriggered.Add(1, new KeyValuePair<string, object?>("trigger_type", rule.TriggerType));
        _logger.LogInformation("Automation rule {RuleId} triggered for tenant {TenantId} with {ApprovalPolicy}", rule.Id, rule.TenantId, rule.ApprovalPolicy);
        return true;
    }

    private async Task<Validation> ValidateCreateAsync(long tenantId, AutomationRuleRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.TriggerType) || request.Trigger is null || request.Actions is null)
            return Validation.Fail("规则名称、triggerType、trigger 和 actions 均为必填项。");
        var triggerType = request.TriggerType.Trim().ToLowerInvariant();
        if (!TriggerTypes.Contains(triggerType)) return Validation.Fail("triggerType 不受支持。");
        var policy = request.ApprovalPolicy?.Trim().ToLowerInvariant() ?? "manual_confirmation";
        if (!ApprovalPolicies.Contains(policy)) return Validation.Fail("approvalPolicy 仅支持 manual_confirmation 或 auto_execute。");
        var trigger = ValidateTrigger(triggerType, request.Trigger.Value);
        if (trigger.Error is not null) return trigger;
        var conditions = request.Conditions is { } rawConditions
            ? await ValidateConditionsAsync(tenantId, rawConditions, cancellationToken)
            : Validation.Ok("[]");
        if (conditions.Error is not null) return conditions;
        var actions = ValidateActions(request.Actions.Value);
        return actions.Error is null ? Validation.Ok(trigger.Value!, conditions.Value!, actions.Value!) : actions;
    }

    private static Validation ValidateTrigger(string triggerType, JsonElement trigger)
    {
        if (trigger.ValueKind != JsonValueKind.Object) return Validation.Fail("trigger 必须是对象。");
        if (triggerType == "time_schedule")
        {
            if (!trigger.TryGetProperty("kind", out var kind)) return Validation.Fail("时间触发器必须包含 kind。");
            var value = kind.GetString();
            if (value is not ("fixed_time" or "sun" or "countdown")) return Validation.Fail("时间触发器 kind 仅支持 fixed_time、sun 或 countdown。");
            if (value == "fixed_time" && (!trigger.TryGetProperty("time", out var time) || !TimeOnly.TryParseExact(time.GetString(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))) return Validation.Fail("fixed_time 必须包含 HH:mm 格式的 time。");
            if (value == "sun" && (!trigger.TryGetProperty("event", out var solar) || solar.GetString() is not ("sunrise" or "sunset"))) return Validation.Fail("sun 必须指定 sunrise 或 sunset。");
            if (value == "countdown" && (!trigger.TryGetProperty("fireAt", out var fireAt) || !DateTime.TryParse(fireAt.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out _))) return Validation.Fail("countdown 必须包含 UTC fireAt。");
        }
        else if (triggerType == "device_state_change" && (!trigger.TryGetProperty("deviceId", out var deviceId) || !deviceId.TryGetInt64(out _))) return Validation.Fail("设备状态触发器必须包含 deviceId。");
        else if (triggerType == "scene_completed" && (!trigger.TryGetProperty("sceneKey", out var scene) || !SmartHomeSceneDefinitions.TryGetIntent(scene.GetString(), out _))) return Validation.Fail("场景触发器必须包含受支持的 sceneKey。");
        else if (triggerType == "sync_completed" && trigger.TryGetProperty("connectorId", out var connector) && !connector.TryGetInt64(out _)) return Validation.Fail("connectorId 必须是数字。");
        return Validation.Ok(trigger.GetRawText());
    }

    private async Task<Validation> ValidateConditionsAsync(long tenantId, JsonElement conditions, CancellationToken cancellationToken)
    {
        if (conditions.ValueKind != JsonValueKind.Array) return Validation.Fail("conditions 必须是数组。");
        foreach (var condition in conditions.EnumerateArray())
        {
            if (condition.ValueKind != JsonValueKind.Object || !condition.TryGetProperty("deviceId", out var deviceId) || !deviceId.TryGetInt64(out var id)) return Validation.Fail("每个条件必须包含 deviceId。");
            if (!await _db.SmartHomeDevices.AnyAsync(x => x.Id == id && x.TenantId == tenantId && x.DeletedAt == null, cancellationToken)) return Validation.Fail("条件中的设备不存在。");
        }
        return Validation.Ok(conditions.GetRawText());
    }

    private static Validation ValidateActions(JsonElement actions)
    {
        if (actions.ValueKind != JsonValueKind.Array || actions.GetArrayLength() == 0) return Validation.Fail("actions 必须是非空数组。");
        foreach (var action in actions.EnumerateArray())
        {
            if (action.ValueKind != JsonValueKind.Object || !action.TryGetProperty("sceneKey", out var scene) || !SmartHomeSceneDefinitions.TryGetIntent(scene.GetString(), out _)) return Validation.Fail("动作目前仅支持内建 sceneKey。");
        }
        return Validation.Ok(actions.GetRawText());
    }

    private async Task<bool> ConditionsMatchAsync(long tenantId, string conditionsJson, CancellationToken cancellationToken)
    {
        foreach (var condition in ReadArray(conditionsJson))
        {
            if (!condition.TryGetProperty("deviceId", out var deviceId) || !deviceId.TryGetInt64(out var id) || !condition.TryGetProperty("capability", out var capability)) return false;
            var state = await _db.DeviceStates.Where(x => x.DeviceId == id).OrderByDescending(x => x.SampledAt).Select(x => x.State).FirstOrDefaultAsync(cancellationToken);
            if (state is null || !StateMatches(state, capability.GetString(), condition)) return false;
        }
        return true;
    }

    private static bool StateMatches(string state, string? capability, JsonElement condition)
    {
        try
        {
            using var document = JsonDocument.Parse(state);
            if (capability is null || !document.RootElement.TryGetProperty(capability, out var actual) || !condition.TryGetProperty("value", out var expected)) return false;
            var equals = actual.GetRawText() == expected.GetRawText();
            return !condition.TryGetProperty("operator", out var op) || op.GetString() != "not_equals" ? equals : !equals;
        }
        catch (JsonException) { return false; }
    }

    private static bool IsDue(AutomationRule rule, DateTime now)
    {
        try
        {
            using var config = JsonDocument.Parse(rule.TriggerConfig);
            var root = config.RootElement;
            var kind = root.GetProperty("kind").GetString();
            if (kind == "countdown") return DateTime.Parse(root.GetProperty("fireAt").GetString()!, null, DateTimeStyles.AdjustToUniversal) <= now && rule.LastTriggeredAt is null;
            var zone = ReadTimeZone(root);
            var local = TimeZoneInfo.ConvertTimeFromUtc(now, zone);
            var due = kind == "fixed_time"
                ? TimeOnly.ParseExact(root.GetProperty("time").GetString()!, "HH:mm", CultureInfo.InvariantCulture).Hour == local.Hour && TimeOnly.ParseExact(root.GetProperty("time").GetString()!, "HH:mm", CultureInfo.InvariantCulture).Minute == local.Minute
                : SolarTime(local.Date, root.GetProperty("event").GetString() == "sunrise", root, zone).Hour == local.Hour && SolarTime(local.Date, root.GetProperty("event").GetString() == "sunrise", root, zone).Minute == local.Minute;
            return due && (rule.LastTriggeredAt is null || TimeZoneInfo.ConvertTimeFromUtc(rule.LastTriggeredAt.Value, zone).Date != local.Date);
        }
        catch (Exception) { return false; }
    }

    // NOAA's standard sunrise equation, sufficient for scheduling. Coordinates are optional and default to Beijing.
    private static DateTime SolarTime(DateTime date, bool sunrise, JsonElement config, TimeZoneInfo zone)
    {
        var latitude = config.TryGetProperty("latitude", out var lat) && lat.TryGetDouble(out var latitudeValue) ? latitudeValue : 39.9042;
        var longitude = config.TryGetProperty("longitude", out var lon) && lon.TryGetDouble(out var longitudeValue) ? longitudeValue : 116.4074;
        var day = date.DayOfYear;
        var gamma = 2 * Math.PI / 365 * (day - 1);
        var equation = 229.18 * (.000075 + .001868 * Math.Cos(gamma) - .032077 * Math.Sin(gamma) - .014615 * Math.Cos(2 * gamma) - .040849 * Math.Sin(2 * gamma));
        var declination = .006918 - .399912 * Math.Cos(gamma) + .070257 * Math.Sin(gamma) - .006758 * Math.Cos(2 * gamma) + .000907 * Math.Sin(2 * gamma) - .002697 * Math.Cos(3 * gamma) + .00148 * Math.Sin(3 * gamma);
        var angle = Math.Acos(Math.Clamp((Math.Cos(90.833 * Math.PI / 180) / (Math.Cos(latitude * Math.PI / 180) * Math.Cos(declination))) - Math.Tan(latitude * Math.PI / 180) * Math.Tan(declination), -1d, 1d));
        var minutes = 720 - 4 * (longitude + (sunrise ? angle : -angle) * 180 / Math.PI) - equation + zone.GetUtcOffset(date).TotalMinutes;
        return date.AddMinutes(minutes).AddMinutes(config.TryGetProperty("offsetMinutes", out var offset) && offset.TryGetInt32(out var value) ? value : 0);
    }

    private static TimeZoneInfo ReadTimeZone(JsonElement config)
    {
        try { return config.TryGetProperty("timeZone", out var zone) && !string.IsNullOrWhiteSpace(zone.GetString()) ? TimeZoneInfo.FindSystemTimeZoneById(zone.GetString()!) : TimeZoneInfo.Utc; }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.Utc; }
    }
    private static bool ConfigMatchesDevice(string json, long deviceId) => TryGetConfigLong(json, "deviceId") == deviceId;
    private static bool ConfigMatchesConnector(string json, long connectorId) { var expected = TryGetConfigLong(json, "connectorId"); return expected is null || expected == connectorId; }
    private static bool ConfigStringEquals(string json, string property, string value) { try { using var document = JsonDocument.Parse(json); return document.RootElement.TryGetProperty(property, out var actual) && string.Equals(actual.GetString(), value, StringComparison.OrdinalIgnoreCase); } catch (JsonException) { return false; } }
    private static long? TryGetConfigLong(string json, string property) { try { using var document = JsonDocument.Parse(json); return document.RootElement.TryGetProperty(property, out var value) && value.TryGetInt64(out var number) ? number : null; } catch (JsonException) { return null; } }
    private static IReadOnlyList<JsonElement> ReadArray(string json) { try { using var document = JsonDocument.Parse(json); return document.RootElement.ValueKind == JsonValueKind.Array ? document.RootElement.EnumerateArray().Select(x => x.Clone()).ToArray() : []; } catch (JsonException) { return []; } }
    private static AutomationRuleView ToView(AutomationRule rule) => new(rule.Id, rule.Name, rule.TriggerType, JsonDocument.Parse(rule.TriggerConfig).RootElement.Clone(), JsonDocument.Parse(rule.Conditions).RootElement.Clone(), JsonDocument.Parse(rule.Actions).RootElement.Clone(), rule.ApprovalPolicy, rule.Enabled, rule.LastTriggeredAt, rule.UpdatedAt, rule.RowVersion);
    private sealed record Validation(string? Value, string? Trigger, string? Conditions, string? Actions, ServiceResult? Error)
    {
        public static Validation Ok(string value) => new(value, null, null, null, null);
        public static Validation Ok(string trigger, string conditions, string actions) => new(null, trigger, conditions, actions, null);
        public static Validation Fail(string message) => new(null, null, null, null, new ServiceResult(422, message));
    }
}
