namespace HomeMind.Common.Model.ViewModel.Data.Productivity;

/// <summary>新建或更新待办事项的请求参数。</summary>
/// <param name="Title">待办标题，可空表示不修改。</param>
/// <param name="Description">详细描述，可空。</param>
/// <param name="Type">待办类型，可空。</param>
/// <param name="Priority">优先级，可空。</param>
/// <param name="Color">展示色 HEX，可空。</param>
/// <param name="Status">状态，可空。</param>
/// <param name="DueAt">截止时间（UTC），可空。</param>
/// <param name="RemindAt">提醒时间（UTC），可空。</param>
/// <param name="Pinned">是否置顶，可空。</param>
/// <param name="SortOrder">排序值，可空。</param>
/// <param name="RepeatRule">重复规则，可空。</param>
/// <param name="ListId">所属列表主键，可空。</param>
/// <param name="ParentId">父待办主键（子任务），可空。</param>
public sealed record TodoWriteRequest(string? Title, string? Description, string? Type, string? Priority, string? Color, string? Status, DateTime? DueAt, DateTime? RemindAt, bool? Pinned, int? SortOrder, string? RepeatRule, long? ListId, long? ParentId);

/// <summary>新建或更新子任务的请求参数。</summary>
/// <param name="Text">子任务文本，可空表示不修改。</param>
/// <param name="Done">完成状态，可空表示不修改。</param>
/// <param name="Seq">展示顺序，可空表示不修改。</param>
public sealed record SubtaskWriteRequest(string? Text, bool? Done, int? Seq);
