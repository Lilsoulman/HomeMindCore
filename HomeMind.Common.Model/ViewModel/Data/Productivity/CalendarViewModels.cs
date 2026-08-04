namespace HomeMind.Common.Model.ViewModel.Data.Productivity;

/// <summary>新建或更新日程的请求参数。</summary>
public sealed record CalendarEventRequest(string? Title, string? Description, string? Location, DateTime? StartAt, DateTime? EndAt, string? Timezone, bool? AllDay, string? Color, decimal? Opacity, string? RepeatRule);

/// <summary>新建或更新日历订阅的请求参数。</summary>
public sealed record SubscriptionRequest(string? Url, string? Name, bool? Enabled, int? RefreshIntervalMin);
