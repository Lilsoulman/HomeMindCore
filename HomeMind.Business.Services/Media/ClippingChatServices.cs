using System.Text.Json;
using System.Text.RegularExpressions;
using HomeMind.Business.IServices.Media;
using HomeMind.Common.Model.Entities;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.Media;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;

namespace HomeMind.Business.Services.Media;

/// <summary>
/// 剪辑对话引导服务（B32）：无状态 context 校验推进、规则式意图匹配（剪辑关键词）、
/// 模板回复 + suggestions 引导按钮。只引导不执行——方案生成/确认/下载仍走既有 Skill Run 链路；
/// 不落库、不新建会话表；响应不包含 MCP 内部路径或 Prompt。
/// </summary>
public sealed class ClippingChatServices : IClippingChatServices
{
    private readonly HomeMindDbContext _db;
    /// <summary>构造剪辑对话服务。</summary>
    /// <param name="db">数据库上下文，用于持久化 V2.8 剪辑任务。</param>
    public ClippingChatServices(HomeMindDbContext db) => _db = db;
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

    /// <summary>构造成功响应：模板回复 + suggestions + 推进后的上下文。</summary>
    private async Task<ServiceResult> PersistAndRespond(ClippingTask task, ClippingChatContext context, string message, string reply, IReadOnlyList<string> suggestions, CancellationToken cancellationToken)
    {
        task.Materials = JsonSerializer.Serialize(context.Materials ?? Array.Empty<string>());
        task.Goal = context.Goal;
        task.Status = context.Step switch { StepCollectingMaterials => ClippingTaskStatus.Collecting, StepGeneratingPlan => ClippingTaskStatus.Generating, StepReviewing => ClippingTaskStatus.Reviewing, _ => ClippingTaskStatus.Done };
        task.EngineStage = task.Status == ClippingTaskStatus.Generating ? "planning" : null;
        task.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return new ServiceResult(200, message, new ClippingChatResponse(reply, suggestions, context, task.Id));
    }
}
