namespace HomeMind.Common.Model.ViewModel.Data.Productivity;

/// <summary>新建或更新日程的请求参数。</summary>
/// <param name="Title">事件标题，可空表示不修改。</param>
/// <param name="Description">事件描述，可空表示不修改。</param>
/// <param name="Location">事件地点，可空表示不修改。</param>
/// <param name="StartAt">开始时间（UTC），可空表示不修改。</param>
/// <param name="EndAt">结束时间（UTC），可空表示不修改或清空。</param>
/// <param name="Timezone">事件显示时区，IANA 标识，可空。</param>
/// <param name="AllDay">是否全天，可空表示不修改。</param>
/// <param name="Color">展示色 HEX，可空。</param>
/// <param name="Opacity">不透明度 0-1，可空。</param>
/// <param name="RepeatRule">重复规则 RFC 5545 子集，可空。</param>
public sealed record CalendarEventRequest(string? Title, string? Description, string? Location, DateTime? StartAt, DateTime? EndAt, string? Timezone, bool? AllDay, string? Color, decimal? Opacity, string? RepeatRule);

/// <summary>新建或更新日历订阅的请求参数。</summary>
/// <param name="Url">iCal 源 URL，明文仅在写入时使用，存储前加密。</param>
/// <param name="Name">订阅展示名，可空。</param>
/// <param name="Enabled">是否启用，可空表示不修改。</param>
/// <param name="RefreshIntervalMin">刷新间隔（分钟），可空表示不修改。</param>
public sealed record SubscriptionRequest(string? Url, string? Name, bool? Enabled, int? RefreshIntervalMin);
