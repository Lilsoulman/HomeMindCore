namespace HomeMind.Common.Model.ViewModel.Data.Productivity;

/// <summary>新建或更新待办事项的请求参数。</summary>
public sealed record TodoWriteRequest(string? Title, string? Description, string? Type, string? Priority, string? Color, string? Status, DateTime? DueAt, DateTime? RemindAt, bool? Pinned, int? SortOrder, string? RepeatRule, long? ListId, long? ParentId);

/// <summary>新建或更新子任务的请求参数。</summary>
public sealed record SubtaskWriteRequest(string? Text, bool? Done, int? Seq);
