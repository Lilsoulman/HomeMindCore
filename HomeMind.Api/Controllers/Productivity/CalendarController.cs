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
[Authorize][Route("api/v1/calendar")]
public sealed class CalendarController:ApiControllerBase
{
 private readonly ICalendarServices _services; public CalendarController(ICalendarServices services)=>_services=services;
 [Authorize(Policy=PermissionNames.CalendarRead)][HttpGet("events")] public async Task<ActionResult<ApiResponse<object>>> ListEvents(DateTime? from,DateTime? to)=>Reply(await UserAsync((u,t)=>_services.ListEventsAsync(u.UserId,u.TenantId,from,to,t)));
 [Authorize(Policy=PermissionNames.CalendarWrite)][HttpPost("events")] public async Task<ActionResult<ApiResponse<object>>> CreateEvent(CalendarEventRequest r)=>Reply(await UserAsync((u,t)=>_services.CreateEventAsync(u.UserId,u.TenantId,r,t)));
 [Authorize(Policy=PermissionNames.CalendarWrite)][HttpPut("events/{id:long}")] public async Task<ActionResult<ApiResponse<object>>> UpdateEvent(long id,CalendarEventRequest r)=>Reply(await UserAsync((u,t)=>_services.UpdateEventAsync(u.UserId,u.TenantId,id,r,t)));
 [Authorize(Policy=PermissionNames.CalendarWrite)][HttpDelete("events/{id:long}")] public async Task<ActionResult<ApiResponse<object>>> DeleteEvent(long id)=>Reply(await UserAsync((u,t)=>_services.DeleteEventAsync(u.UserId,u.TenantId,id,t)));
 [Authorize(Policy=PermissionNames.CalendarRead)][HttpGet("subscriptions")] public async Task<ActionResult<ApiResponse<object>>> ListSubscriptions()=>Reply(await UserAsync((u,t)=>_services.ListSubscriptionsAsync(u.UserId,u.TenantId,t)));
 [Authorize(Policy=PermissionNames.CalendarWrite)][HttpPost("subscriptions")] public async Task<ActionResult<ApiResponse<object>>> CreateSubscription(SubscriptionRequest r)=>Reply(await UserAsync((u,t)=>_services.CreateSubscriptionAsync(u.UserId,u.TenantId,r,t)));
 [Authorize(Policy=PermissionNames.CalendarWrite)][HttpPut("subscriptions/{id:long}")] public async Task<ActionResult<ApiResponse<object>>> UpdateSubscription(long id,SubscriptionRequest r)=>Reply(await UserAsync((u,t)=>_services.UpdateSubscriptionAsync(u.UserId,u.TenantId,id,r,t)));
 [Authorize(Policy=PermissionNames.CalendarWrite)][HttpDelete("subscriptions/{id:long}")] public async Task<ActionResult<ApiResponse<object>>> DeleteSubscription(long id)=>Reply(await UserAsync((u,t)=>_services.DeleteSubscriptionAsync(u.UserId,u.TenantId,id,t)));
 [Authorize(Policy=PermissionNames.CalendarWrite)][HttpPost("ical/fetch")] public ActionResult<ApiResponse<object>> FetchIcal()=>StatusCode(501,ApiResponse<object>.Fail(501,"请先配置访问地址白名单规则，才能启用日历订阅拉取功能。"));
 private async Task<ServiceResult>UserAsync(Func<UserContext,CancellationToken,Task<ServiceResult>> f)=>TryGetUser(out var u)?await f(u,HttpContext.RequestAborted):new(401,"未提供访问令牌，或访问令牌已过期。");
 private ActionResult<ApiResponse<object>>Reply(ServiceResult r)=>StatusCode(r.StatusCode,r.Succeeded?new ApiResponse<object>(0,r.Message,r.Data):ApiResponse<object>.Fail(r.StatusCode,r.Message));
}
