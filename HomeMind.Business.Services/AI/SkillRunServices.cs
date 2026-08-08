using System.Text.Json;
using System.Text.RegularExpressions;
using HomeMind.Business.IServices.AI;
using HomeMind.Business.IServices.Family;
using HomeMind.Common.Model.Entities;
using HomeMind.Common.Model.Entities.Family;
using HomeMind.Common.Model.Entities.Steward;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.AI;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;

namespace HomeMind.Business.Services.AI;

/// <summary>
/// Skill 独立执行确定性编排（SkillExecutor 首个实现）：按 skillCode 解析平台级 Skill 目录，
/// 校验输入参数（素材位置必填）后确定性生成剪辑方案（片段序列/音频/时长摘要），产出单个
/// <c>draft_generate</c> Run Action（L1）等待用户确认。运行复用既有 AgentRun、确认、幂等与
/// 审计边界，不新建运行时；响应与审计不包含素材目录内容、MCP 内部路径、草稿绝对路径或 Prompt。
/// </summary>
public sealed class SkillRunServices : ISkillRunServices
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const int DefaultDurationSeconds = 15;
    private const int MaxDurationSeconds = 600;

    private readonly HomeMindDbContext _db;
    private readonly IFamilyAuditLogger _audit;

    /// <summary>构造 Skill 运行服务。</summary>
    /// <param name="db">数据库上下文。</param>
    /// <param name="audit">家庭域审计日志写入器，SkillRun 创建审计使用。</param>
    public SkillRunServices(HomeMindDbContext db, IFamilyAuditLogger audit)
    {
        _db = db;
        _audit = audit;
    }

    /// <inheritdoc />
    public async Task<ServiceResult> CreateAsync(long userId, long tenantId, string skillCode, SkillRunCreateRequest request, CancellationToken cancellationToken = default)
    {
        var skill = await _db.SkillCatalogs.SingleOrDefaultAsync(
            x => x.TenantId == 1 && x.Key == skillCode && x.Status == SkillCatalogStatus.Active && x.DeletedAt == null, cancellationToken);
        if (skill is null) return new ServiceResult(422, "未知或未启用的 Skill。");

        var input = ReadSkillInput(request.InputJson);
        if (input is null) return new ServiceResult(422, "Skill 输入必须为合法 JSON 且包含非空的 media_location。");

        var idempotencyKey = Guid.TryParse(request.IdempotencyKey, out var parsedKey) ? parsedKey.ToString() : Guid.NewGuid().ToString();
        var existing = await _db.AgentRuns.SingleOrDefaultAsync(
            x => x.TenantId == tenantId && x.UserId == userId && x.RequestIdempotencyKey == idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            if (existing.SourceType != "skill") return new ServiceResult(409, "该幂等键已用于其他运行类型。");
            return new ServiceResult(200, "Skill 运行已存在。", await ToViewAsync(existing, cancellationToken));
        }

        var plan = BuildPlan(input.MediaLocation, input.Instruction);
        var planJson = JsonSerializer.Serialize(ToPlanJson(plan), JsonOptions);
        var now = DateTime.UtcNow;
        var run = new AgentRun
        {
            TenantId = tenantId,
            UserId = userId,
            SourceType = "skill",
            ExpertVersionId = null,
            RequestIdempotencyKey = idempotencyKey,
            Input = request.InputJson,
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

        _db.ExpertRunActions.Add(new ExpertRunAction
        {
            RunId = run.Id,
            TenantId = tenantId,
            UserId = userId,
            ActionType = "draft_generate",
            RequestIdempotencyKey = Guid.NewGuid().ToString(),
            RequestJson = planJson,
            Status = "pending",
            CreatedAt = now,
            UpdatedAt = now
        });

        run.Status = "pending_actions";
        run.ResultSummary = $"快速剪辑方案已生成：素材「{plan.SourceName}」，总时长约 {plan.TotalDuration} 秒，确认后生成剪映草稿。";
        run.Result = JsonSerializer.Serialize(new { skill = skill.Key, segment_count = 1, total_duration = plan.TotalDuration }, JsonOptions);
        AddEvent(run, 1, "running", "正在解析素材与生成剪辑方案。", now);
        AddEvent(run, 2, "pending_actions", run.ResultSummary, now);
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync(tenantId, userId, FamilyAuditActions.SkillRunCreated, FamilyAuditTargetTypes.SkillRun,
            run.Id, null, new { skill = skill.Key, segment_count = 1, total_duration = plan.TotalDuration }, null, run.Id, cancellationToken);
        return new ServiceResult(201, run.ResultSummary, await ToViewAsync(run, cancellationToken));
    }

    /// <inheritdoc />
    public async Task<ServiceResult> GetAsync(long userId, long tenantId, long runId, CancellationToken cancellationToken = default)
    {
        var run = await _db.AgentRuns.SingleOrDefaultAsync(
            x => x.Id == runId && x.TenantId == tenantId && x.UserId == userId && x.SourceType == "skill", cancellationToken);
        if (run is null) return new ServiceResult(404, "请求的 Skill 运行不存在。");
        return new ServiceResult(200, "查询成功。", await ToViewAsync(run, cancellationToken));
    }

    /// <summary>解析 Skill 输入 JSON：media_location 必填非空，instruction 可选；非法或缺字段返回 null。</summary>
    private static SkillInput? ReadSkillInput(string inputJson)
    {
        try
        {
            using var document = JsonDocument.Parse(inputJson);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || ReadValue(root, "media_location") is not { ValueKind: JsonValueKind.String } location || string.IsNullOrWhiteSpace(location.GetString()))
                return null;
            var instruction = ReadValue(root, "instruction") is { ValueKind: JsonValueKind.String } instructionElement ? instructionElement.GetString() : null;
            return new SkillInput(location.GetString()!.Trim(), instruction);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>确定性生成剪辑方案：单片段 + 可选音频占位；总时长从指令中提取，无指令时默认 15 秒。</summary>
    private static SkillPlanInfo BuildPlan(string mediaLocation, string? instruction)
    {
        var duration = ParseDurationSeconds(instruction);
        return new SkillPlanInfo(mediaLocation, instruction, ExtractSourceName(mediaLocation), duration);
    }

    /// <summary>将方案信息序列化为蛇形键 JSON（与场景动作 metadata 同惯例，B25 确认执行时解析）。</summary>
    private static object ToPlanJson(SkillPlanInfo plan) => new
    {
        media_location = plan.MediaLocation,
        instruction = plan.Instruction,
        segments = new[]
        {
            new { index = 1, source = plan.SourceName, duration = plan.TotalDuration }
        },
        audio = (object?)null,
        total_duration = plan.TotalDuration
    };

    /// <summary>从创作指令中提取目标时长（N秒/N分钟），取 1-600 秒范围；无匹配返回默认 15 秒。</summary>
    private static int ParseDurationSeconds(string? instruction)
    {
        if (string.IsNullOrWhiteSpace(instruction)) return DefaultDurationSeconds;
        var match = Regex.Match(instruction, @"(\d+)\s*(秒|s|分钟|min)", RegexOptions.IgnoreCase);
        if (!match.Success) return DefaultDurationSeconds;
        if (!int.TryParse(match.Groups[1].Value, out var value)) return DefaultDurationSeconds;
        var seconds = match.Groups[2].Value is "分钟" or "min" ? value * 60 : value;
        return Math.Clamp(seconds, 1, MaxDurationSeconds);
    }

    /// <summary>从素材位置提取展示名：取路径最后一段；目录位置回退为「素材目录」。</summary>
    private static string ExtractSourceName(string mediaLocation)
    {
        var trimmed = mediaLocation.Trim();
        var isDirectory = trimmed.EndsWith('/') || trimmed.EndsWith('\\');
        if (isDirectory) trimmed = trimmed.TrimEnd('/', '\\');
        var index = Math.Max(trimmed.LastIndexOf('/'), trimmed.LastIndexOf('\\'));
        var name = index >= 0 ? trimmed[(index + 1)..] : trimmed;
        if (string.IsNullOrWhiteSpace(name)) return isDirectory ? "素材目录" : "素材";
        return name;
    }

    /// <summary>按蛇形键读取 JSON 属性字符串值；兼容 System.Text.Json 驼峰序列化形态。</summary>
    private static JsonElement? ReadValue(JsonElement element, string snakeName)
    {
        if (element.TryGetProperty(snakeName, out var value)) return value;
        var parts = snakeName.Split('_');
        var camelName = parts.Length == 1 ? parts[0] : parts[0] + string.Concat(parts.Skip(1).Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
        return element.TryGetProperty(camelName, out value) ? value : null;
    }

    /// <summary>从运行动作的剪辑方案读取片段与总时长；解析失败返回空值。</summary>
    private static (int SegmentCount, int TotalDuration) ReadPlanSummary(string requestJson)
    {
        try
        {
            using var document = JsonDocument.Parse(requestJson);
            var root = document.RootElement;
            var segmentCount = ReadValue(root, "segments") is { ValueKind: JsonValueKind.Array } segments ? segments.GetArrayLength() : 0;
            var totalDuration = ReadValue(root, "total_duration") is { ValueKind: JsonValueKind.Number } duration && duration.TryGetInt32(out var parsed) ? parsed : 0;
            return (segmentCount, totalDuration);
        }
        catch (JsonException)
        {
            return (0, 0);
        }
    }

    private async Task<SkillRunView> ToViewAsync(AgentRun run, CancellationToken cancellationToken)
    {
        var events = await _db.RunEvents
            .Where(x => x.RunId == run.Id && x.TenantId == run.TenantId)
            .OrderBy(x => x.Sequence)
            .ToListAsync(cancellationToken);
        var actions = await _db.ExpertRunActions
            .Where(x => x.RunId == run.Id && x.TenantId == run.TenantId && x.ActionType == "draft_generate")
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);
        return new SkillRunView(
            run.Id,
            run.Status,
            run.ResultSummary,
            run.CreatedAt,
            run.FinishedAt,
            events.Select(x => new SkillRunEventView(x.Sequence, x.EventType, ReadMessage(x.Payload), x.CreatedAt)).ToArray(),
            actions.Select(ToActionView).ToArray());
    }

    /// <summary>从动作的剪辑方案读取片段数与总时长生成动作视图；内容非法时回退为默认值。</summary>
    private static SkillRunActionView ToActionView(ExpertRunAction action)
    {
        var (segmentCount, totalDuration) = ReadPlanSummary(action.RequestJson);
        var description = segmentCount == 0
            ? "生成剪映 .draft 草稿文件。"
            : $"共 {segmentCount} 个片段，总时长约 {totalDuration} 秒，风险等级 {ConfirmationRiskLevel.L1}。";
        return new SkillRunActionView(action.Id, action.ActionType, action.Status, "快速剪辑方案", description, ConfirmationRiskLevel.L1);
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

    private static string ReadMessage(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        return document.RootElement.TryGetProperty("message", out var message) ? message.GetString() ?? "" : "";
    }

    private sealed record SkillInput(string MediaLocation, string? Instruction);
    private sealed record SkillPlanInfo(string MediaLocation, string? Instruction, string SourceName, int TotalDuration);
}
