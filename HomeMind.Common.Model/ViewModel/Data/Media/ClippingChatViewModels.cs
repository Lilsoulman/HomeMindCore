namespace HomeMind.Common.Model.ViewModel.Data.Media;

/// <summary>剪辑对话引导请求（B32）：消息 + 无状态上下文回传。</summary>
/// <param name="Message">用户消息。</param>
/// <param name="Context">当前对话上下文；首次对话可为空（默认 collecting_materials）。</param>
public sealed record ClippingChatRequest(string Message, ClippingChatContext? Context, long? TaskId = null);

/// <summary>剪辑对话上下文（无状态，随请求回传由服务端校验推进，不落库、不新建会话表）。</summary>
/// <param name="Step">引导步骤：collecting_materials / generating_plan / reviewing / done。</param>
/// <param name="Materials">已收集素材路径列表（前端上传后回填），可为空。</param>
/// <param name="Goal">创作目标（自然语言），可为空。</param>
/// <param name="PlanGenerated">方案是否已生成（前端创建 Skill Run 后置 true），可为空。</param>
public sealed record ClippingChatContext(string Step, IReadOnlyList<string>? Materials, string? Goal, bool? PlanGenerated);

/// <summary>剪辑对话引导响应：模板回复 + suggestions 引导按钮 + 推进后的上下文。</summary>
/// <param name="Reply">引导回复文案。</param>
/// <param name="Suggestions">建议操作，前端渲染为快捷按钮。</param>
/// <param name="Context">推进后的上下文，前端原样回传下一次请求。</param>
public sealed record ClippingChatResponse(string Reply, IReadOnlyList<string> Suggestions, ClippingChatContext Context, long TaskId);

/// <summary>剪辑任务展示安全视图，供刷新或重进页面后恢复进度和版本历史。</summary>
public sealed record ClippingTaskView(long Id, long? RunId, string Status, string? EngineStage, IReadOnlyList<string> Materials, string? Goal, object? CurrentPlan, IReadOnlyList<ClippingTaskVersionView> VersionHistory, DateTime CreatedAt, DateTime UpdatedAt);

/// <summary>剪辑方案版本展示项。</summary>
public sealed record ClippingTaskVersionView(int Version, object? Plan, string Change, DateTime ModifiedAt);
