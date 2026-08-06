using System;
using System.Threading;
using System.Threading.Tasks;
using HomeMind.Api.Controllers.Base;
using HomeMind.Api.Services;
using HomeMind.Business.IServices.Productivity;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.Productivity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HomeMind.Api.Controllers.Productivity;

/// <summary>日历模块，控制器不直接访问数据库。</summary>
/// <remarks>所有时间均使用 UTC；订阅源 URL 在存储前加密，API 不会返回明文。</remarks>
[Authorize]
[Route("api/v1/calendar")]
public sealed class CalendarController : ApiControllerBase
{
    private readonly ICalendarServices _services;

    /// <summary>构造日历控制器。</summary>
    /// <param name="services">日历业务服务。</param>
    public CalendarController(ICalendarServices services) => _services = services;

    /// <summary>按时间窗口列出当前用户与租户下的日历事件。</summary>
    /// <remarks>权限：<c>calendar.read</c>。窗口参数均为 UTC。</remarks>
    /// <param name="from">窗口起始时间（UTC），可空。</param>
    /// <param name="to">窗口结束时间（UTC），可空。</param>
    /// <returns>事件列表的统一响应。</returns>
    [Authorize(Policy = PermissionNames.CalendarRead)]
    [HttpGet("events")]
    public async Task<ActionResult<ApiResponse<object>>> ListEvents(DateTime? from, DateTime? to) => Reply(await UserAsync((u, t) => _services.ListEventsAsync(u.UserId, u.TenantId, from, to, t)));

    /// <summary>创建一个日历事件。</summary>
    /// <remarks>权限：<c>calendar.write</c>。</remarks>
    /// <param name="r">事件创建请求体。</param>
    /// <returns>新建事件统一响应。</returns>
    [Authorize(Policy = PermissionNames.CalendarWrite)]
    [HttpPost("events")]
    public async Task<ActionResult<ApiResponse<object>>> CreateEvent(CalendarEventRequest r) => Reply(await UserAsync((u, t) => _services.CreateEventAsync(u.UserId, u.TenantId, r, t)));

    /// <summary>按主键更新日历事件；可空字段表示不修改。</summary>
    /// <remarks>权限：<c>calendar.write</c>。跨用户或跨租户事件返回 404。</remarks>
    /// <param name="id">事件主键。</param>
    /// <param name="r">事件更新请求体。</param>
    /// <returns>更新结果统一响应。</returns>
    [Authorize(Policy = PermissionNames.CalendarWrite)]
    [HttpPut("events/{id:long}")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateEvent(long id, CalendarEventRequest r) => Reply(await UserAsync((u, t) => _services.UpdateEventAsync(u.UserId, u.TenantId, id, r, t)));

    /// <summary>软删除指定日历事件。</summary>
    /// <remarks>权限：<c>calendar.write</c>。</remarks>
    /// <param name="id">事件主键。</param>
    /// <returns>删除结果统一响应。</returns>
    [Authorize(Policy = PermissionNames.CalendarWrite)]
    [HttpDelete("events/{id:long}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteEvent(long id) => Reply(await UserAsync((u, t) => _services.DeleteEventAsync(u.UserId, u.TenantId, id, t)));

    /// <summary>列出当前用户与租户下的日历订阅。</summary>
    /// <remarks>权限：<c>calendar.read</c>。响应不包含源 URL 明文。</remarks>
    /// <returns>订阅列表统一响应。</returns>
    [Authorize(Policy = PermissionNames.CalendarRead)]
    [HttpGet("subscriptions")]
    public async Task<ActionResult<ApiResponse<object>>> ListSubscriptions() => Reply(await UserAsync((u, t) => _services.ListSubscriptionsAsync(u.UserId, u.TenantId, t)));

    /// <summary>创建一个日历订阅，源 URL 加密后存储。</summary>
    /// <remarks>权限：<c>calendar.write</c>。存储前对源 URL 进行加密并写入哈希索引。</remarks>
    /// <param name="r">订阅创建请求体。</param>
    /// <returns>新建订阅统一响应。</returns>
    [Authorize(Policy = PermissionNames.CalendarWrite)]
    [HttpPost("subscriptions")]
    public async Task<ActionResult<ApiResponse<object>>> CreateSubscription(SubscriptionRequest r) => Reply(await UserAsync((u, t) => _services.CreateSubscriptionAsync(u.UserId, u.TenantId, r, t)));

    /// <summary>按主键更新订阅；可空字段表示不修改。</summary>
    /// <remarks>权限：<c>calendar.write</c>。</remarks>
    /// <param name="id">订阅主键。</param>
    /// <param name="r">订阅更新请求体。</param>
    /// <returns>更新结果统一响应。</returns>
    [Authorize(Policy = PermissionNames.CalendarWrite)]
    [HttpPut("subscriptions/{id:long}")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateSubscription(long id, SubscriptionRequest r) => Reply(await UserAsync((u, t) => _services.UpdateSubscriptionAsync(u.UserId, u.TenantId, id, r, t)));

    /// <summary>软删除指定订阅。</summary>
    /// <remarks>权限：<c>calendar.write</c>。</remarks>
    /// <param name="id">订阅主键。</param>
    /// <returns>删除结果统一响应。</returns>
    [Authorize(Policy = PermissionNames.CalendarWrite)]
    [HttpDelete("subscriptions/{id:long}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteSubscription(long id) => Reply(await UserAsync((u, t) => _services.DeleteSubscriptionAsync(u.UserId, u.TenantId, id, t)));

    /// <summary>手动拉取并解析一个 iCal 源；在 SSRF 白名单策略配置完成前返回 501。</summary>
    /// <remarks>权限：<c>calendar.write</c>。未配置白名单时一律返回 501，绝不直接访问客户端提供的 URL。</remarks>
    /// <returns>固定 501 + 业务错误码 50000 的统一响应。</returns>
    [Authorize(Policy = PermissionNames.CalendarWrite)]
    [HttpPost("ical/fetch")]
    public ActionResult<ApiResponse<object>> FetchIcal() => StatusCode(501, ApiResponse<object>.Fail(ApiErrorCodes.NotImplemented, "请先配置访问地址白名单规则，才能启用日历订阅拉取功能。"));

    /// <summary>在用户上下文就绪时执行给定的业务回调，否则返回 401。</summary>
    /// <param name="f">执行业务逻辑的回调。</param>
    /// <returns>业务执行结果 <see cref="ServiceResult"/>。</returns>
    private async Task<ServiceResult> UserAsync(Func<UserContext, CancellationToken, Task<ServiceResult>> f) => TryGetUser(out var u) ? await f(u, HttpContext.RequestAborted) : new(401, "未提供访问令牌，或访问令牌已过期。");

    /// <summary>将 <see cref="ServiceResult"/> 转换为统一 HTTP 响应。</summary>
    /// <param name="r">业务执行结果。</param>
    /// <returns>统一响应体与对应状态码。</returns>
    private ActionResult<ApiResponse<object>> Reply(ServiceResult r) => StatusCode(r.StatusCode, r.Succeeded ? new ApiResponse<object>(0, r.Message, r.Data) : ApiResponse<object>.Fail(r.Code, r.Message));
}
