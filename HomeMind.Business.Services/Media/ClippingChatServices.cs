using System.Text.Json;
using System.Text.RegularExpressions;
using HomeMind.Business.IServices.AI;
using HomeMind.Business.IServices.Media;
using HomeMind.Common.Infrastructure;
using HomeMind.Common.Model.Entities;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.Media;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;

namespace HomeMind.Business.Services.Media;

/// <summary>
/// 剪辑对话引导服务：B32 的无状态模板引导与 B39 的受控 LLM 参数解析。
/// LLM 仅在用户已启用并完整配置 AI 时解析已选素材的一句话目标；结构化结果经服务端校验后写入任务，
/// 不记录或返回 Prompt。调用失败、超时或 AI 不可用时保持 B32 模板引导，不创建运行或执行外部动作。
/// </summary>
public sealed class ClippingChatServices : IClippingChatServices
{
    /// <summary>结构化目标允许的最短时长秒数。</summary>
    private const int MinDurationSeconds = 1;
    /// <summary>结构化目标允许的最长时长秒数，与快速剪辑方案上限保持一致。</summary>
    private const int MaxDurationSeconds = 600;
    private readonly HomeMindDbContext _db;
    private readonly ILLMClient? _llm;
    private readonly SecretProtector? _secretProtector;

    /// <summary>构造剪辑对话服务。</summary>
    /// <param name="db">数据库上下文，用于持久化 V2.8 剪辑任务。</param>
    /// <param name="llm">LLM 客户端；未注册时仅保留模板引导。</param>
    /// <param name="secretProtector">AI 密钥解密器；未注册时仅保留模板引导。</param>
    public ClippingChatServices(HomeMindDbContext db, ILLMClient? llm = null, SecretProtector? secretProtector = null)
    {
        _db = db;
        _llm = llm;
        _secretProtector = secretProtector;
    }
    private const string StepCollectingMaterials = "collecting_materials";
    private const string StepGeneratingPlan = "generating_plan";
    private const string StepReviewing = "reviewing";
    private const string StepDone = "done";

    private static readonly HashSet<string> AllowedSteps = new(StringComparer.Ordinal)
    {
        StepCollectingMaterials, StepGeneratingPlan, StepReviewing, StepDone
    };
    private static readonly Regex IntentPattern = new(@"剪辑|剪视频|剪映|剪片子|视频剪辑|快速剪辑|剪一剪|剪一下|视频处理", RegexOptions.IgnoreCase);

    /// <inheritdoc />
    public async Task<ServiceResult> ChatAsync(long userId, long tenantId, ClippingChatRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return new ServiceResult(422, "对话消息不能为空。");

        var task = request.TaskId is long taskId
            ? await _db.ClippingTasks.SingleOrDefaultAsync(x => x.Id == taskId && x.TenantId == tenantId && x.CreatedByUserId == userId && x.DeletedAt == null, cancellationToken)
            : null;
        if (request.TaskId is not null && task is null) return new ServiceResult(404, "请求的剪辑任务不存在。");
        task ??= new ClippingTask { TenantId = tenantId, CreatedByUserId = userId, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        if (task.Id == 0) _db.ClippingTasks.Add(task);

        var context = request.Context ?? new ClippingChatContext(StepCollectingMaterials, null, null, null);
        if (!AllowedSteps.Contains(context.Step))
            return new ServiceResult(422, "对话上下文步骤非法。");

        var message = request.Message.Trim();
        var materials = context.Materials?.Where(m => !string.IsNullOrWhiteSpace(m)).ToList() ?? new List<string>();
        var goal = string.IsNullOrWhiteSpace(context.Goal) ? null : context.Goal.Trim();

        if (materials.Count > 0 && context.Step is StepCollectingMaterials or StepGeneratingPlan)
        {
            var parsing = await TryParseGoalAsync(userId, message, cancellationToken);
            if (parsing.Status == GoalParsingStatus.Invalid)
                return new ServiceResult(422, "AI 返回的剪辑参数不符合约束，请调整后重试。");
            if (parsing.Parameters is not null)
                return await PersistParsedGoalAndRespond(task, context, parsing.Parameters, cancellationToken);
        }

        // 意图匹配仅在素材尚未就绪的收集步骤生效：素材已就绪后用户消息（如「竖屏 30 秒」）直接作为创作目标处理。
        if (context.Step == StepCollectingMaterials && materials.Count == 0 && !IntentPattern.IsMatch(message))
            return await PersistAndRespond(task, context, "当前仅支持快速剪辑相关对话。", "抱歉，我目前只处理快速剪辑相关的请求（如「帮我剪视频」「剪一下这段素材」）。", Array.Empty<string>(), cancellationToken);

        switch (context.Step)
        {
            case StepCollectingMaterials:
                if (materials.Count == 0)
                    return await PersistAndRespond(task, context, "好的，我来帮你剪视频。请先上传素材（支持视频/音频文件），或填写素材路径。", "好的，我来帮你剪视频。请先上传素材（支持视频/音频文件），或填写素材路径。", new[] { "上传素材", "填写素材路径" }, cancellationToken);
                if (goal is null)
                {
                    goal = message.Length > 120 ? message[..120] : message;
                    return await PersistAndRespond(task, context with { Step = StepGeneratingPlan, Goal = goal }, $"素材已就绪（{materials.Count} 段），创作目标已记录：{goal}。确认后即可生成剪辑方案。", $"素材已就绪（{materials.Count} 段），创作目标已记录：{goal}。确认后即可生成剪辑方案。", new[] { "生成方案" }, cancellationToken);
                }
                return await PersistAndRespond(task, context with { Step = StepGeneratingPlan }, $"创作目标已记录：{goal}。确认后即可生成剪辑方案。", $"创作目标已记录：{goal}。确认后即可生成剪辑方案。", new[] { "生成方案" }, cancellationToken);

            case StepGeneratingPlan:
                if (goal is null)
                {
                    goal = message.Length > 120 ? message[..120] : message;
                    return await PersistAndRespond(task, context with { Goal = goal, Step = StepGeneratingPlan }, $"创作目标已记录：{goal}。确认后即可生成剪辑方案。", $"创作目标已记录：{goal}。确认后即可生成剪辑方案。", new[] { "生成方案" }, cancellationToken);
                }
                return await PersistAndRespond(task, context with { Step = StepGeneratingPlan }, $"创作目标已记录：{goal}。确认后即可生成剪辑方案。", $"创作目标已记录：{goal}。确认后即可生成剪辑方案。", new[] { "生成方案" }, cancellationToken);

            case StepReviewing:
                return await PersistAndRespond(task, context, "方案已生成，可以确认生成草稿，或修改创作目标重新生成方案。", "方案已生成，可以确认生成草稿，或修改创作目标重新生成方案。", new[] { "确认方案", "修改目标重新生成" }, cancellationToken);

            case StepDone:
                return await PersistAndRespond(task, context with { Step = StepCollectingMaterials, Materials = null, Goal = null, PlanGenerated = false }, "草稿已生成，打开剪映即可继续编辑。可以重新剪辑，或调整素材与目标再来一次。", "草稿已生成，打开剪映即可继续编辑。可以重新剪辑，或调整素材与目标再来一次。", new[] { "重新剪辑" }, cancellationToken);

            default:
                return new ServiceResult(422, "对话上下文步骤非法。");
        }
    }

    /// <summary>持久化通过 LLM schema 校验的参数，并返回可供客户端确认的卡片。</summary>
    private async Task<ServiceResult> PersistParsedGoalAndRespond(ClippingTask task, ClippingChatContext context, ClippingGoalParameters parameters, CancellationToken cancellationToken)
    {
        var summary = BuildGoalSummary(parameters);
        var persistedGoal = JsonSerializer.Serialize(new
        {
            target_duration = parameters.TargetDuration,
            aspect_ratio = parameters.AspectRatio,
            style = parameters.Style,
            subtitle = parameters.Subtitle,
            mood = parameters.Mood
        });
        var nextContext = context with { Step = StepGeneratingPlan, Goal = summary, PlanGenerated = false };
        var confirmation = new ClippingChatConfirmationCard("已理解", summary, new[]
        {
            $"时长：{parameters.TargetDuration} 秒",
            $"画幅：{ToAspectRatioLabel(parameters.AspectRatio)}",
            $"风格：{parameters.Style}",
            parameters.Subtitle ? "字幕：添加" : "字幕：不添加",
            $"氛围：{parameters.Mood}"
        });
        return await PersistAndRespond(task, nextContext, $"已理解：{summary}。确认后即可生成剪辑方案。", $"已理解：{summary}。确认后即可生成剪辑方案。", new[] { "确认生成方案", "修改需求" }, cancellationToken, persistedGoal, confirmation);
    }

    /// <summary>构造成功响应：模板回复或确认卡、suggestions 与推进后的上下文。</summary>
    private async Task<ServiceResult> PersistAndRespond(ClippingTask task, ClippingChatContext context, string message, string reply, IReadOnlyList<string> suggestions, CancellationToken cancellationToken, string? taskGoal = null, ClippingChatConfirmationCard? confirmation = null)
    {
        task.Materials = JsonSerializer.Serialize(context.Materials ?? Array.Empty<string>());
        task.Goal = taskGoal ?? context.Goal;
        task.Status = context.Step switch { StepCollectingMaterials => ClippingTaskStatus.Collecting, StepGeneratingPlan => ClippingTaskStatus.Generating, StepReviewing => ClippingTaskStatus.Reviewing, _ => ClippingTaskStatus.Done };
        task.EngineStage = task.Status == ClippingTaskStatus.Generating ? "planning" : null;
        task.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return new ServiceResult(200, message, new ClippingChatResponse(reply, suggestions, context, task.Id, confirmation));
    }

    /// <summary>在用户启用并完整配置 AI 时调用模型解析单句剪辑目标；异常与不可用均降级为模板引导。</summary>
    private async Task<GoalParsingResult> TryParseGoalAsync(long userId, string message, CancellationToken cancellationToken)
    {
        if (_llm is null || _secretProtector is null) return GoalParsingResult.Fallback;
        var config = await _db.AiConfigs.FindAsync(new object?[] { userId }, cancellationToken);
        if (config is null || !config.Enabled || string.IsNullOrWhiteSpace(config.Endpoint) || string.IsNullOrWhiteSpace(config.Model) || config.ApiKeyEncrypted.Length == 0)
            return GoalParsingResult.Fallback;

        try
        {
            var completion = await _llm.CompleteAsync(new LlmRequest(
                config.Endpoint,
                config.Model,
                _secretProtector.Decrypt(config.ApiKeyEncrypted),
                config.Temperature,
                "你是剪辑参数解析器。仅返回一个 JSON 对象，且必须包含 target_duration（1-600 的整数秒）、aspect_ratio（仅 9:16、16:9、1:1）、style（1-40 字符）、subtitle（布尔值）和 mood（1-40 字符）。不要输出解释、Markdown 或其他字段。",
                message,
                256), cancellationToken);
            if (!completion.Success) return GoalParsingResult.Fallback;
            return TryReadParameters(completion.Content, out var parameters)
                ? new GoalParsingResult(GoalParsingStatus.Succeeded, parameters)
                : new GoalParsingResult(GoalParsingStatus.Invalid, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return GoalParsingResult.Fallback;
        }
        catch
        {
            return GoalParsingResult.Fallback;
        }
    }

    /// <summary>严格读取 LLM JSON，拒绝缺字段、超出范围、错误类型或未知画幅。</summary>
    private static bool TryReadParameters(string content, out ClippingGoalParameters? parameters)
    {
        parameters = null;
        try
        {
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("target_duration", out var durationElement)
                || !durationElement.TryGetInt32(out var duration)
                || duration is < MinDurationSeconds or > MaxDurationSeconds
                || !root.TryGetProperty("aspect_ratio", out var aspectElement)
                || aspectElement.ValueKind != JsonValueKind.String
                || !root.TryGetProperty("style", out var styleElement)
                || styleElement.ValueKind != JsonValueKind.String
                || !root.TryGetProperty("subtitle", out var subtitleElement)
                || (subtitleElement.ValueKind is not JsonValueKind.True and not JsonValueKind.False)
                || !root.TryGetProperty("mood", out var moodElement)
                || moodElement.ValueKind != JsonValueKind.String)
                return false;

            foreach (var property in root.EnumerateObject())
            {
                if (property.Name is not ("target_duration" or "aspect_ratio" or "style" or "subtitle" or "mood"))
                    return false;
            }

            var aspectRatio = aspectElement.GetString();
            var style = styleElement.GetString()?.Trim();
            var mood = moodElement.GetString()?.Trim();
            if (aspectRatio is not ("9:16" or "16:9" or "1:1") || string.IsNullOrWhiteSpace(style) || style.Length > 40 || string.IsNullOrWhiteSpace(mood) || mood.Length > 40)
                return false;

            parameters = new ClippingGoalParameters(duration, aspectRatio, style, subtitleElement.GetBoolean(), mood);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>生成面向用户且可作为后续 Skill 指令回填的参数摘要。</summary>
    private static string BuildGoalSummary(ClippingGoalParameters parameters) =>
        $"{parameters.TargetDuration} 秒 / {ToAspectRatioLabel(parameters.AspectRatio)} / {parameters.Style} / {(parameters.Subtitle ? "加字幕" : "不加字幕")} / {parameters.Mood}";

    /// <summary>将受限画幅编码转换为中文展示文本。</summary>
    private static string ToAspectRatioLabel(string aspectRatio) => aspectRatio switch
    {
        "9:16" => "竖屏",
        "16:9" => "横屏",
        _ => "方形"
    };

    /// <summary>模型解析结果的内部状态：降级、合法或 schema 非法。</summary>
    private enum GoalParsingStatus { Fallback, Succeeded, Invalid }

    private sealed record GoalParsingResult(GoalParsingStatus Status, ClippingGoalParameters? Parameters)
    {
        public static GoalParsingResult Fallback { get; } = new(GoalParsingStatus.Fallback, null);
    }

    private sealed record ClippingGoalParameters(int TargetDuration, string AspectRatio, string Style, bool Subtitle, string Mood);
}
