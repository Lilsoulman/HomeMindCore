using System.Text.RegularExpressions;
using HomeMind.Business.IServices.Media;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.Media;

namespace HomeMind.Business.Services.Media;

/// <summary>
/// 剪辑对话引导服务（B32）：无状态 context 校验推进、规则式意图匹配（剪辑关键词）、
/// 模板回复 + suggestions 引导按钮。只引导不执行——方案生成/确认/下载仍走既有 Skill Run 链路；
/// 不落库、不新建会话表；响应不包含 MCP 内部路径或 Prompt。
/// </summary>
public sealed class ClippingChatServices : IClippingChatServices
{
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
    public Task<ServiceResult> ChatAsync(long userId, long tenantId, ClippingChatRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return Task.FromResult(new ServiceResult(422, "对话消息不能为空。"));

        var context = request.Context ?? new ClippingChatContext(StepCollectingMaterials, null, null, null);
        if (!AllowedSteps.Contains(context.Step))
            return Task.FromResult(new ServiceResult(422, "对话上下文步骤非法。"));

        var message = request.Message.Trim();
        // 意图匹配仅在初始素材收集步骤生效；进入目标/方案/结果步骤后消息按该步骤语义直接处理。
        if (context.Step == StepCollectingMaterials && !IntentPattern.IsMatch(message))
            return Task.FromResult(new ServiceResult(200, "当前仅支持快速剪辑相关对话。",
                new ClippingChatResponse("抱歉，我目前只处理快速剪辑相关的请求（如「帮我剪视频」「剪一下这段素材」）。", Array.Empty<string>(), context)));

        var materials = context.Materials?.Where(m => !string.IsNullOrWhiteSpace(m)).ToList() ?? new List<string>();
        var goal = string.IsNullOrWhiteSpace(context.Goal) ? null : context.Goal.Trim();

        switch (context.Step)
        {
            case StepCollectingMaterials:
                if (materials.Count == 0)
                    return Task.FromResult(Ok("好的，我来帮你剪视频。请先上传素材（支持视频/音频文件），或填写素材路径。",
                        new[] { "上传素材", "填写素材路径" }, context));
                if (goal is null)
                    return Task.FromResult(Ok($"素材已就绪（{materials.Count} 段）。请告诉我创作目标：如时长、画幅、配乐、字幕等要求。",
                        new[] { "竖屏 30 秒", "加字幕和配乐", "按默认剪辑" }, context with { Step = StepGeneratingPlan }));
                return Task.FromResult(Ok($"创作目标已记录：{goal}。确认后即可生成剪辑方案。", new[] { "生成方案" }, context with { Step = StepGeneratingPlan }));

            case StepGeneratingPlan:
                if (goal is null)
                {
                    goal = message.Length > 120 ? message[..120] : message;
                    return Task.FromResult(Ok($"创作目标已记录：{goal}。确认后即可生成剪辑方案。", new[] { "生成方案" }, context with { Goal = goal, Step = StepGeneratingPlan }));
                }
                return Task.FromResult(Ok($"创作目标已记录：{goal}。确认后即可生成剪辑方案。", new[] { "生成方案" }, context with { Step = StepGeneratingPlan }));

            case StepReviewing:
                return Task.FromResult(Ok("方案已生成，可以确认生成草稿，或修改创作目标重新生成方案。", new[] { "确认方案", "修改目标重新生成" }, context));

            case StepDone:
                return Task.FromResult(Ok("草稿已生成，打开剪映即可继续编辑。可以重新剪辑，或调整素材与目标再来一次。", new[] { "重新剪辑" }, context with { Step = StepCollectingMaterials, Materials = null, Goal = null, PlanGenerated = false }));

            default:
                return Task.FromResult(new ServiceResult(422, "对话上下文步骤非法。"));
        }
    }

    /// <summary>构造成功响应：模板回复 + suggestions + 推进后的上下文。</summary>
    private static ServiceResult Ok(string reply, IReadOnlyList<string> suggestions, ClippingChatContext context) =>
        new(200, reply, new ClippingChatResponse(reply, suggestions, context));
}
