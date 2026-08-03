using System;
using System.Threading.Tasks;
using Dapper;
using HomeMind.Api.Controllers.Base;
using HomeMind.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HomeMind.Api.Controllers.Productivity;

/// <summary>
/// 日历模块，管理用户日程及外部 iCalendar 订阅。
/// </summary>
[Authorize]
[Route("api/v1/calendar")]
public sealed class CalendarController : ApiControllerBase
{
    private readonly MySqlConnectionFactory _connections;
    private readonly SecretProtector _secrets;
    public CalendarController(MySqlConnectionFactory connections, SecretProtector secrets)
    {
        _connections = connections;
        _secrets = secrets;
    }

    /// <summary>
    /// 按可选的开始和结束时间查询当前用户的日程。
    /// </summary>
    [Authorize(Policy = PermissionNames.CalendarRead)]
    [HttpGet("events")]
    public async Task<ActionResult<ApiResponse<object>>> ListEvents(DateTime? from, DateTime? to)
    {
        if (!TryGetUser(out var user)) return UnauthorizedResult<object>();
        await using var db = _connections.Open();
        var items = await db.QueryAsync("SELECT id,title,description,location,start_at startAt,end_at endAt,timezone,all_day allDay,color,opacity,repeat_rule repeatRule,created_at createdAt,updated_at updatedAt FROM calendar_events WHERE user_id=@UserId AND tenant_id=@TenantId AND deleted_at IS NULL AND (@From IS NULL OR start_at>=@From) AND (@To IS NULL OR start_at<=@To) ORDER BY start_at", new { user.UserId, user.TenantId, From = from, To = to });
        return Ok(ApiResponse<object>.Ok(items));
    }

    /// <summary>
    /// 创建一条新的用户日程，标题和开始时间为必填项。
    /// </summary>
    [Authorize(Policy = PermissionNames.CalendarWrite)]
    [HttpPost("events")]
    public async Task<ActionResult<ApiResponse<object>>> CreateEvent(CalendarEventRequest request)
    {
        if (!TryGetUser(out var user)) return UnauthorizedResult<object>();
        if (string.IsNullOrWhiteSpace(request.Title) || request.StartAt is null) return BadRequest(ApiResponse<object>.Fail(422, "title and startAt are required."));
        await using var db = _connections.Open();
        var id = await db.QuerySingleAsync<long>("INSERT INTO calendar_events(tenant_id,user_id,title,description,location,start_at,end_at,timezone,all_day,color,opacity,repeat_rule) VALUES (@TenantId,@UserId,@Title,@Description,@Location,@StartAt,@EndAt,@Timezone,COALESCE(@AllDay,0),@Color,@Opacity,@RepeatRule); SELECT LAST_INSERT_ID();", new { user.TenantId, user.UserId, Title = request.Title.Trim(), request.Description, request.Location, request.StartAt, request.EndAt, request.Timezone, request.AllDay, request.Color, request.Opacity, request.RepeatRule });
        return Created($"/api/v1/calendar/events/{id}", ApiResponse<object>.Ok(await GetEvent(db, user, id)));
    }

    /// <summary>
    /// 更新指定日程的可编辑字段，未提供的字段保持原值。
    /// </summary>
    [Authorize(Policy = PermissionNames.CalendarWrite)]
    [HttpPut("events/{id:long}")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateEvent(long id, CalendarEventRequest request)
    {
        if (!TryGetUser(out var user)) return UnauthorizedResult<object>();
        await using var db = _connections.Open();
        var changed = await db.ExecuteAsync("UPDATE calendar_events SET title=COALESCE(@Title,title),description=COALESCE(@Description,description),location=COALESCE(@Location,location),start_at=COALESCE(@StartAt,start_at),end_at=COALESCE(@EndAt,end_at),timezone=COALESCE(@Timezone,timezone),all_day=COALESCE(@AllDay,all_day),color=COALESCE(@Color,color),opacity=COALESCE(@Opacity,opacity),repeat_rule=COALESCE(@RepeatRule,repeat_rule) WHERE id=@Id AND user_id=@UserId AND tenant_id=@TenantId AND deleted_at IS NULL", new { Id = id, user.UserId, user.TenantId, request.Title, request.Description, request.Location, request.StartAt, request.EndAt, request.Timezone, request.AllDay, request.Color, request.Opacity, request.RepeatRule });
        return changed == 0 ? NotFoundResult<object>() : Ok(ApiResponse<object>.Ok(await GetEvent(db, user, id)));
    }

    /// <summary>
    /// 软删除指定日程。
    /// </summary>
    [Authorize(Policy = PermissionNames.CalendarWrite)]
    [HttpDelete("events/{id:long}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteEvent(long id)
    {
        if (!TryGetUser(out var user)) return UnauthorizedResult<object>();
        await using var db = _connections.Open();
        var changed = await db.ExecuteAsync("UPDATE calendar_events SET deleted_at=UTC_TIMESTAMP(3) WHERE id=@Id AND user_id=@UserId AND tenant_id=@TenantId AND deleted_at IS NULL", new { Id = id, user.UserId, user.TenantId });
        return changed == 0 ? NotFoundResult<object>() : Ok(ApiResponse<object>.Ok(new { id }));
    }

    /// <summary>
    /// 获取当前用户的外部日历订阅及其同步状态。
    /// </summary>
    [Authorize(Policy = PermissionNames.CalendarRead)]
    [HttpGet("subscriptions")]
    public async Task<ActionResult<ApiResponse<object>>> ListSubscriptions()
    {
        if (!TryGetUser(out var user)) return UnauthorizedResult<object>();
        await using var db = _connections.Open();
        var items = await db.QueryAsync("SELECT id,name,enabled,refresh_interval_min refreshIntervalMin,last_fetch_at lastFetchAt,last_error lastError,created_at createdAt FROM calendar_subscriptions WHERE user_id=@UserId AND tenant_id=@TenantId AND deleted_at IS NULL ORDER BY id DESC", new { user.UserId, user.TenantId });
        return Ok(ApiResponse<object>.Ok(items));
    }

    /// <summary>
    /// 创建外部 iCalendar 订阅，订阅地址必须是绝对 URL。
    /// </summary>
    [Authorize(Policy = PermissionNames.CalendarWrite)]
    [HttpPost("subscriptions")]
    public async Task<ActionResult<ApiResponse<object>>> CreateSubscription(SubscriptionRequest request)
    {
        if (!TryGetUser(out var user)) return UnauthorizedResult<object>();
        if (!Uri.TryCreate(request.Url, UriKind.Absolute, out _)) return BadRequest(ApiResponse<object>.Fail(422, "url must be absolute."));
        await using var db = _connections.Open();
        var id = await db.QuerySingleAsync<long>("INSERT INTO calendar_subscriptions(tenant_id,user_id,name,source_url_encrypted,source_url_hash,enabled,refresh_interval_min) VALUES (@TenantId,@UserId,@Name,@Cipher,UNHEX(SHA2(@Url,256)),COALESCE(@Enabled,1),COALESCE(@Interval,60)); SELECT LAST_INSERT_ID();", new { user.TenantId, user.UserId, Name = string.IsNullOrWhiteSpace(request.Name) ? "Calendar subscription" : request.Name.Trim(), Cipher = _secrets.Encrypt(request.Url!), Url = request.Url, request.Enabled, Interval = request.RefreshIntervalMin });
        return Created($"/api/v1/calendar/subscriptions/{id}", ApiResponse<object>.Ok(await db.QuerySingleAsync("SELECT id,name,enabled,refresh_interval_min refreshIntervalMin,created_at createdAt FROM calendar_subscriptions WHERE id=@Id", new { Id = id })));
    }

    /// <summary>
    /// 更新指定外部日历订阅的名称、启用状态或刷新频率。
    /// </summary>
    [Authorize(Policy = PermissionNames.CalendarWrite)]
    [HttpPut("subscriptions/{id:long}")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateSubscription(long id, SubscriptionRequest request)
    {
        if (!TryGetUser(out var user)) return UnauthorizedResult<object>();
        await using var db = _connections.Open();
        var changed = await db.ExecuteAsync("UPDATE calendar_subscriptions SET name=COALESCE(@Name,name),enabled=COALESCE(@Enabled,enabled),refresh_interval_min=COALESCE(@Interval,refresh_interval_min) WHERE id=@Id AND user_id=@UserId AND tenant_id=@TenantId AND deleted_at IS NULL", new { Id = id, user.UserId, user.TenantId, request.Name, request.Enabled, Interval = request.RefreshIntervalMin });
        return changed == 0 ? NotFoundResult<object>() : Ok(ApiResponse<object>.Ok(new { id }));
    }

    /// <summary>
    /// 软删除指定外部日历订阅。
    /// </summary>
    [Authorize(Policy = PermissionNames.CalendarWrite)]
    [HttpDelete("subscriptions/{id:long}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteSubscription(long id)
    {
        if (!TryGetUser(out var user)) return UnauthorizedResult<object>();
        await using var db = _connections.Open();
        var changed = await db.ExecuteAsync("UPDATE calendar_subscriptions SET deleted_at=UTC_TIMESTAMP(3) WHERE id=@Id AND user_id=@UserId AND tenant_id=@TenantId AND deleted_at IS NULL", new { Id = id, user.UserId, user.TenantId });
        return changed == 0 ? NotFoundResult<object>() : Ok(ApiResponse<object>.Ok(new { id }));
    }

    /// <summary>
    /// 触发 iCalendar 拉取；在 SSRF 白名单配置完成前该接口不可用。
    /// </summary>
    [Authorize(Policy = PermissionNames.CalendarWrite)]
    [HttpPost("ical/fetch")]
    public ActionResult<ApiResponse<object>> FetchIcal() => StatusCode(501, ApiResponse<object>.Fail(501, "iCal network fetch is disabled until SSRF allow-list rules are configured."));
    private static Task<dynamic> GetEvent(System.Data.IDbConnection db, UserContext user, long id) => db.QuerySingleAsync("SELECT id,title,description,location,start_at startAt,end_at endAt,timezone,all_day allDay,color,opacity,repeat_rule repeatRule FROM calendar_events WHERE id=@Id AND user_id=@UserId AND tenant_id=@TenantId", new { Id = id, user.UserId, user.TenantId });
    public sealed record CalendarEventRequest(string? Title, string? Description, string? Location, DateTime? StartAt, DateTime? EndAt, string? Timezone, bool? AllDay, string? Color, decimal? Opacity, string? RepeatRule);
    public sealed record SubscriptionRequest(string? Url, string? Name, bool? Enabled, int? RefreshIntervalMin);
}
