using System.Text.Json;
using System.Text.RegularExpressions;
using HomeMind.Business.IServices.AI;
using HomeMind.Business.IServices.Expert;
using HomeMind.Business.IServices.Family;
using HomeMind.Business.IServices.Media;
using HomeMind.Common.Model.Entities;
using HomeMind.Common.Model.Entities.Family;
using HomeMind.Common.Model.Entities.Steward;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.AI;
using HomeMind.Common.Model.ViewModel.Data.Media;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;

namespace HomeMind.Business.Services.AI;

/// <summary>
/// Skill 独立执行确定性编排（SkillExecutor 首个实现）：按 skillCode 解析平台级 Skill 目录，
/// 校验输入参数（素材位置必填）后确定性生成剪辑方案（片段序列/音频/时长摘要），产出单个
/// <c>draft_generate</c> Run Action（L1）等待用户确认；确认后经剪辑 MCP 客户端生成 .draft
/// 草稿内容并复用 <c>RegisterGeneratedFileAsync</c> 登记为生成文件。运行复用既有 AgentRun、
/// 确认、幂等与审计边界，不新建运行时；响应与审计不包含素材目录内容、MCP 内部路径、
/// 草稿绝对路径或 Prompt。
/// </summary>
public sealed class SkillRunServices : ISkillRunServices
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const int DefaultDurationSeconds = 15;
    private const int MaxDurationSeconds = 600;

    private readonly HomeMindDbContext _db;
    private readonly IFamilyAuditLogger _audit;
    private readonly IClippingMcpClient _clippingMcp;
    private readonly IExpertFileServices _files;
    private readonly IClippingPipelineServices _pipeline;

    /// <summary>构造 Skill 运行服务。</summary>
    /// <param name="db">数据库上下文。</param>
    /// <param name="audit">家庭域审计日志写入器，SkillRun 创建审计使用。</param>
    /// <param name="clippingMcp">剪辑 MCP 客户端，确认后生成 .draft 草稿内容。</param>
    /// <param name="files">专家文件服务，登记生成的草稿文件。</param>
    public SkillRunServices(HomeMindDbContext db, IFamilyAuditLogger audit, IClippingMcpClient clippingMcp, IExpertFileServices files, IClippingPipelineServices? pipeline = null)
    {
        _db = db;
        _audit = audit;
        _clippingMcp = clippingMcp;
        _files = files;
        _pipeline = pipeline ?? new DisabledClippingPipelineServices();
    }

    /// <inheritdoc />
    public async Task<ServiceResult> CreateAsync(long userId, long tenantId, string skillCode, SkillRunCreateRequest request, CancellationToken cancellationToken = default)
    {
        var skill = await _db.SkillCatalogs.SingleOrDefaultAsync(
            x => x.TenantId == 1 && x.Key == skillCode && x.Status == SkillCatalogStatus.Active && x.DeletedAt == null, cancellationToken);
        if (skill is null) return new ServiceResult(422, "未知或未启用的 Skill。");

        var input = ReadSkillInput(request.InputJson);
        if (input is null) return new ServiceResult(422, "Skill 输入必须为合法 JSON 且包含非空的 media_location。");

        var task = request.TaskId is long taskId
            ? await _db.ClippingTasks.SingleOrDefaultAsync(x => x.Id == taskId && x.TenantId == tenantId && x.CreatedByUserId == userId && x.DeletedAt == null, cancellationToken)
            : null;
        if (request.TaskId is not null && task is null) return new ServiceResult(404, "请求的剪辑任务不存在。");

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

        if (task is not null)
        {
            task.RunId = run.Id;
            task.Status = "reviewing";
            task.EngineStage = "planning";
            task.CurrentPlan = planJson;
            task.VersionHistory = JsonSerializer.Serialize(new[] { new { version = 1, plan = ToPlanJson(plan), change = "已生成初始方案", modifiedAt = now } }, JsonOptions);
            task.UpdatedAt = now;
            await _db.SaveChangesAsync(cancellationToken);
        }

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

    /// <inheritdoc />
    public async Task<ServiceResult> ConfirmActionAsync(long userId, long tenantId, long runId, long actionId, ConfirmSkillRunActionRequest request, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(request.IdempotencyKey, out _))
            return new ServiceResult(422, "确认 Skill 动作时必须提供有效的幂等键。");

        var action = await _db.ExpertRunActions.SingleOrDefaultAsync(x =>
            x.Id == actionId && x.RunId == runId && x.TenantId == tenantId && x.UserId == userId && x.ActionType == "draft_generate", cancellationToken);
        if (action is null) return new ServiceResult(404, "请求的 Skill 动作不存在。");

        var run = await _db.AgentRuns.SingleOrDefaultAsync(x => x.Id == runId && x.TenantId == tenantId, cancellationToken);
        if (run is null) return new ServiceResult(404, "请求的运行不存在。");
        if (!IsSnapshotAuthorized(run, userId))
            return new ServiceResult(403, "当前成员无权执行该运行的动作。");

        var idempotencyKey = request.IdempotencyKey;
        var previous = await _db.ActionExecutionAudits.SingleOrDefaultAsync(
            x => x.RunActionId == action.Id && x.IdempotencyKey == idempotencyKey, cancellationToken);
        if (previous is not null) return ReplayActionResult(action, previous);
        if (action.Status != "pending") return new ServiceResult(409, "该 Skill 动作已经确认或处理完成，不能再次执行。");

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
            Command = JsonSerializer.Serialize(new { action_type = "draft_generate" }),
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.ActionExecutionAudits.Add(audit);
        AddEvent(run, await NextSequenceAsync(runId, cancellationToken), "action_confirmed", "已确认快速剪辑方案。", now);
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync(tenantId, userId, FamilyAuditActions.SkillActionConfirmed, FamilyAuditTargetTypes.SkillRun,
            run.Id, null, new { action_id = action.Id }, null, run.Id, cancellationToken);

        var clippingTask = await _db.ClippingTasks.SingleOrDefaultAsync(x => x.RunId == run.Id && x.TenantId == tenantId && x.CreatedByUserId == userId && x.DeletedAt == null, cancellationToken);
        if (clippingTask is not null && _pipeline is not null && _pipeline is not DisabledClippingPipelineServices)
        {
            clippingTask.Status = ClippingTaskStatus.Rendering;
            clippingTask.EngineStage = "render";
            clippingTask.UpdatedAt = now;
            run.Status = "running";
            run.ResultSummary = "粗剪视频已排队，正在生成可预览产物。";
            run.Result = JsonSerializer.Serialize(new { skill_run = "quick_edit", status = run.Status, stage = "render" }, JsonOptions);
            AddEvent(run, await NextSequenceAsync(runId, cancellationToken), "render_queued", run.ResultSummary, now);
            await _db.SaveChangesAsync(cancellationToken);
            return new ServiceResult(202, run.ResultSummary, new { actionId = action.Id, status = action.Status, stage = "render" });
        }

        string? failureMessage = null;
        long? draftFileId = null;
        string draftFileName = $"quick_edit_{run.Id}.draft.json";
        long draftSizeBytes = 0;
        try
        {
            var content = await _clippingMcp.GenerateDraftAsync(action.RequestJson, cancellationToken);
            draftSizeBytes = content.Length;
            var registered = await _files.RegisterGeneratedFileAsync(userId, tenantId, draftFileName, "application/json", content, run.Id, cancellationToken);
            if (!registered.Succeeded)
            {
                failureMessage = registered.Message;
            }
            else
            {
                using var document = JsonDocument.Parse(JsonSerializer.Serialize(registered.Data, typeof(object), JsonOptions));
                if (document.RootElement.TryGetProperty("fileId", out var fileId) && fileId.TryGetInt64(out var parsed)) draftFileId = parsed;
                if (document.RootElement.TryGetProperty("sizeBytes", out var size) && size.TryGetInt64(out var parsedSize)) draftSizeBytes = parsedSize;
            }
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            failureMessage = "剪辑草稿生成或登记失败。";
        }

        var succeeded = draftFileId is not null;
        now = DateTime.UtcNow;
        action.Status = succeeded ? "executed" : "failed";
        action.Result = JsonSerializer.Serialize(succeeded
            ? (object)new { status = action.Status, draft_file_id = draftFileId, file_name = draftFileName, size_bytes = draftSizeBytes }
            : new { status = action.Status, error_code = "draft_generation_failed" });
        action.UpdatedAt = now;
        audit.Status = action.Status;
        audit.Result = action.Result;
        audit.UpdatedAt = now;

        var summary = succeeded
            ? "草稿已生成，打开剪映即可编辑。"
            : $"草稿生成失败：{failureMessage ?? "剪辑服务不可用"}。";
        run.Status = succeeded ? "completed" : "failed";
        run.FinishedAt = now;
        run.ResultSummary = summary;
        run.Result = JsonSerializer.Serialize(succeeded
            ? (object)new { skill_run = "quick_edit", status = run.Status, draft_file_id = draftFileId, file_name = draftFileName, size_bytes = draftSizeBytes }
            : new { skill_run = "quick_edit", status = run.Status, error_code = "draft_generation_failed" }, JsonOptions);
        AddEvent(run, await NextSequenceAsync(runId, cancellationToken), succeeded ? "action_executed" : "action_failed", summary, now);
        await _db.SaveChangesAsync(cancellationToken);

        if (succeeded)
        {
            await _audit.LogAsync(tenantId, userId, FamilyAuditActions.SkillDraftRegistered, FamilyAuditTargetTypes.SkillDraft,
                draftFileId, null, new { file_id = draftFileId, file_name = draftFileName, size_bytes = draftSizeBytes }, null, run.Id, cancellationToken);
            return new ServiceResult(200, summary, new { actionId = action.Id, status = action.Status, message = summary, fileId = draftFileId });
        }
        return new ServiceResult(502, summary);
    }

    /// <inheritdoc />
    public async Task<ServiceResult> ReviseAsync(long userId, long tenantId, long runId, ReviseSkillRunRequest request, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(request.IdempotencyKey, out _))
            return new ServiceResult(422, "修订剪辑方案时必须提供有效的幂等键。");

        var run = await _db.AgentRuns.SingleOrDefaultAsync(
            x => x.Id == runId && x.TenantId == tenantId && x.UserId == userId && x.SourceType == "skill", cancellationToken);
        if (run is null) return new ServiceResult(404, "请求的 Skill 运行不存在。");
        if (!IsSnapshotAuthorized(run, userId))
            return new ServiceResult(403, "当前成员无权修订该运行的方案。");

        var action = await _db.ExpertRunActions.SingleOrDefaultAsync(
            x => x.RunId == run.Id && x.TenantId == tenantId && x.ActionType == "draft_generate", cancellationToken);
        if (action is null) return new ServiceResult(404, "请求的 Skill 动作不存在。");

        // 幂等重放（与 B25 确认同机制）：同一修订幂等键仅返回当前视图，不重复生成事件/审计。
        var previous = await _db.ActionExecutionAudits.SingleOrDefaultAsync(
            x => x.RunActionId == action.Id && x.IdempotencyKey == request.IdempotencyKey, cancellationToken);
        if (previous is not null)
            return new ServiceResult(200, "该修订已生效，返回当前方案。", await ToViewAsync(run, cancellationToken));

        if (run.Status != "pending_actions" || action.Status != "pending")
            return new ServiceResult(409, "方案已确认或运行已终态，不能再次修订。");

        var input = ReadSkillInput(run.Input);
        if (input is null) return new ServiceResult(422, "运行输入解析失败，不能修订。");

        if (request.ReworkScope is not null && request.ReworkScope is not ("parameters" or "partial" or "full"))
            return new ServiceResult(422, "重做范围必须为 parameters、partial 或 full。");
        var plan = BuildPlan(input.MediaLocation, request.Instruction?.Trim());
        var planJson = JsonSerializer.Serialize(ToPlanJson(plan), JsonOptions);
        var now = DateTime.UtcNow;
        action.RequestJson = planJson;
        action.UpdatedAt = now;
        run.ResultSummary = $"快速剪辑方案已生成：素材「{plan.SourceName}」，总时长约 {plan.TotalDuration} 秒，确认后生成剪映草稿。";
        AddEvent(run, await NextSequenceAsync(runId, cancellationToken), "plan_revised", $"已按新创作目标重新生成方案，共 1 个片段，总时长约 {plan.TotalDuration} 秒。", now);
        _db.ActionExecutionAudits.Add(new ActionExecutionAudit
        {
            TenantId = tenantId,
            RunActionId = action.Id,
            OperatorUserId = userId,
            IdempotencyKey = request.IdempotencyKey,
            Status = "executed",
            Command = JsonSerializer.Serialize(new { action_type = "draft_generate_revise" }),
            CreatedAt = now,
            UpdatedAt = now
        });
        await _db.SaveChangesAsync(cancellationToken);
        var task = await _db.ClippingTasks.SingleOrDefaultAsync(x => x.RunId == run.Id && x.TenantId == tenantId && x.CreatedByUserId == userId && x.DeletedAt == null, cancellationToken);
        if (task is not null)
        {
            var history = JsonSerializer.Deserialize<List<ClippingTaskVersionEntry>>(task.VersionHistory, JsonOptions) ?? [];
            history.Add(new ClippingTaskVersionEntry(history.Count + 1, ToPlanJson(plan), request.Instruction?.Trim() ?? "已修改方案", now));
            task.Status = "reviewing";
            task.EngineStage = "planning";
            task.CurrentPlan = planJson;
            task.VersionHistory = JsonSerializer.Serialize(history, JsonOptions);
            task.UpdatedAt = now;
            await _db.SaveChangesAsync(cancellationToken);
            var reworkScope = request.ReworkScope ?? InferReworkScope(request.Instruction);
            if (reworkScope != "parameters")
                await _pipeline.QueueAsync(task.Id, tenantId, reworkScope == "full" ? "video_use" : "hyperframes", request.AllowSeedance, request.CostConfirmed, cancellationToken);
        }
        await _audit.LogAsync(tenantId, userId, FamilyAuditActions.SkillRunRevised, FamilyAuditTargetTypes.SkillRun,
            run.Id, null, new { run_id = run.Id, segment_count = 1, total_duration = plan.TotalDuration }, null, run.Id, cancellationToken);
        return new ServiceResult(200, "剪辑方案已修订。", await ToViewAsync(run, cancellationToken));
    }

    /// <summary>复验运行权限快照：快照缺失视为存量运行放行；SkillRun 为 household 快照且无连接器授权，仅校验归属。</summary>
    private static bool IsSnapshotAuthorized(AgentRun run, long userId)
    {
        if (string.IsNullOrWhiteSpace(run.PermissionSnapshot)) return true;
        try
        {
            using var document = JsonDocument.Parse(run.PermissionSnapshot);
            if (document.RootElement.TryGetProperty("bindingScope", out var scope) && scope.GetString() == "personal")
            {
                return document.RootElement.TryGetProperty("ownerUserId", out var owner) && owner.TryGetInt64(out var ownerId) && ownerId == userId;
            }
            return true;
        }
        catch (JsonException)
        {
            return true;
        }
    }

    /// <summary>重放既有确认结果：同一幂等键仅返回首次执行结果，不重复生成或登记草稿。</summary>
    private static ServiceResult ReplayActionResult(ExpertRunAction action, ActionExecutionAudit audit)
    {
        var succeeded = audit.Status == "executed";
        return new ServiceResult(succeeded ? 200 : audit.Status == "executing" ? 202 : 502,
            succeeded ? "草稿已生成，打开剪映即可编辑。" : "Skill 动作正在处理或已执行失败。",
            succeeded ? new { actionId = action.Id, status = action.Status, message = "草稿已生成，打开剪映即可编辑。" } : null);
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

    /// <summary>从运行动作的剪辑方案读取片段序列与总时长（B30 结构化视图数据）；解析失败返回空值。</summary>
    private static SkillPlanData ReadPlan(string requestJson)
    {
        try
        {
            using var document = JsonDocument.Parse(requestJson);
            var root = document.RootElement;
            var segments = new List<SkillPlanSegmentView>();
            if (ReadValue(root, "segments") is { ValueKind: JsonValueKind.Array } segmentArray)
            {
                foreach (var segment in segmentArray.EnumerateArray())
                {
                    var index = segment.TryGetProperty("index", out var indexElement) && indexElement.TryGetInt32(out var parsedIndex) ? parsedIndex : segments.Count + 1;
                    var source = segment.TryGetProperty("source", out var sourceElement) ? sourceElement.GetString() ?? "" : "";
                    var segmentDuration = segment.TryGetProperty("duration", out var durationElement) && durationElement.TryGetInt32(out var parsedDuration) ? parsedDuration : 0;
                    segments.Add(new SkillPlanSegmentView(index, source, segmentDuration));
                }
            }
            object? audio = null;
            if (ReadValue(root, "audio") is { ValueKind: JsonValueKind.Object } audioElement) audio = audioElement;
            var totalDuration = ReadValue(root, "total_duration") is { ValueKind: JsonValueKind.Number } duration && duration.TryGetInt32(out var parsedTotal) ? parsedTotal : 0;
            return new SkillPlanData(segments, audio, totalDuration);
        }
        catch (JsonException)
        {
            return new SkillPlanData([], null, 0);
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
        var task = await _db.ClippingTasks.SingleOrDefaultAsync(x => x.RunId == run.Id && x.TenantId == run.TenantId && x.CreatedByUserId == run.UserId && x.DeletedAt == null, cancellationToken);
        var history = task is null ? null : JsonSerializer.Deserialize<List<ClippingTaskVersionEntry>>(task.VersionHistory, JsonOptions);
        return new SkillRunView(
            run.Id,
            run.Status,
            run.ResultSummary,
            run.CreatedAt,
            run.FinishedAt,
            events.Select(x => new SkillRunEventView(x.Sequence, x.EventType, ReadMessage(x.Payload), x.CreatedAt)).ToArray(),
            actions.Select(ToActionView).ToArray(),
            task?.EngineStage,
            history?.Count,
            history?.Select(x => new ClippingTaskVersionView(x.Version, x.Plan, x.Change, x.ModifiedAt)).ToArray());
    }

    /// <summary>从动作的剪辑方案读取片段序列与总时长生成动作视图（B30 结构化输出）；内容非法时回退为默认值。</summary>
    private static SkillRunActionView ToActionView(ExpertRunAction action)
    {
        var plan = ReadPlan(action.RequestJson);
        var description = plan.Segments.Count == 0
            ? "生成剪映 .draft 草稿文件。"
            : $"共 {plan.Segments.Count} 个片段，总时长约 {plan.TotalDuration} 秒，风险等级 {ConfirmationRiskLevel.L1}。";
        return new SkillRunActionView(action.Id, action.ActionType, action.Status, "快速剪辑方案", description, ConfirmationRiskLevel.L1, plan.Segments, plan.Audio, plan.TotalDuration);
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

    private sealed record SkillInput(string MediaLocation, string? Instruction);
    private sealed record ClippingTaskVersionEntry(int Version, object Plan, string Change, DateTime ModifiedAt);
    private sealed record SkillPlanInfo(string MediaLocation, string? Instruction, string SourceName, int TotalDuration);
    private sealed record SkillPlanData(IReadOnlyList<SkillPlanSegmentView> Segments, object? Audio, int TotalDuration);

    /// <summary>依据修改文本在未显式指定时推断重做粒度。</summary>
    private static string InferReworkScope(string? instruction) => instruction?.Contains("全量", StringComparison.Ordinal) == true || instruction?.Contains("重新剪", StringComparison.Ordinal) == true
        ? "full" : instruction?.Contains("片头", StringComparison.Ordinal) == true || instruction?.Contains("转场", StringComparison.Ordinal) == true || instruction?.Contains("标题", StringComparison.Ordinal) == true
            ? "partial" : "parameters";

    /// <summary>供既有直接构造测试使用的禁用流水线，避免测试触发后台进程。</summary>
    private sealed class DisabledClippingPipelineServices : IClippingPipelineServices
    {
        public Task<ServiceResult> QueueAsync(long taskId, long tenantId, string startStage, bool allowSeedance, bool costConfirmed, CancellationToken cancellationToken = default) => Task.FromResult(new ServiceResult(202, "剪辑引擎任务已排队。"));
        public Task<int> ProcessNextAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
    }
}
