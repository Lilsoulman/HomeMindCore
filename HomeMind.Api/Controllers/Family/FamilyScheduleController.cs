using System;
using System.Threading;
using System.Threading.Tasks;
using HomeMind.Api.Authorization;
using HomeMind.Api.Controllers.Base;
using HomeMind.Api.Services;
using HomeMind.Business.IServices.Family;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.Family;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HomeMind.Api.Controllers.Family;

/// <summary>家庭日程协同管家 API，提供跨成员日历、空档、到期提醒和明日预览。</summary>
[Authorize]
[Route("api/v1/homes/{homeId:long}/schedule")]
public sealed class FamilyScheduleController : ApiControllerBase
{
    private readonly IFamilyScheduleServices _schedule;

    /// <summary>构造家庭日程协同控制器。</summary>
    /// <param name="schedule">家庭日程协同服务。</param>
    public FamilyScheduleController(IFamilyScheduleServices schedule) => _schedule = schedule;

    /// <summary>聚合家庭内活跃成员的日历事件。</summary>
    /// <param name="homeId">家庭主键，必须等于 JWT tenant_id。</param>
    /// <param name="from">窗口起始时间（UTC）。</param>
    /// <param name="to">窗口结束时间（UTC），最长 31 天。</param>
    [Authorize(Policy = PermissionNames.CalendarRead), RequireHomeOwner]
    [HttpGet("events")]
    public async Task<ActionResult<ApiResponse<object>>> Events(long homeId, DateTime? from, DateTime? to) => Reply(await UserAsync((_, token) => _schedule.ListEventsAsync(homeId, from, to, token)));

    /// <summary>检测家庭成员日历中相交的时间段。</summary>
    /// <param name="homeId">家庭主键，必须等于 JWT tenant_id。</param>
    /// <param name="from">窗口起始时间（UTC）。</param>
    /// <param name="to">窗口结束时间（UTC），最长 31 天。</param>
    [Authorize(Policy = PermissionNames.CalendarRead), RequireHomeOwner]
    [HttpGet("conflicts")]
    public async Task<ActionResult<ApiResponse<object>>> Conflicts(long homeId, DateTime? from, DateTime? to) => Reply(await UserAsync((_, token) => _schedule.ListConflictsAsync(homeId, from, to, token)));

    /// <summary>返回全体活跃成员无日程占用的候选空档。</summary>
    /// <param name="homeId">家庭主键，必须等于 JWT tenant_id。</param>
    /// <param name="from">窗口起始时间（UTC）。</param>
    /// <param name="to">窗口结束时间（UTC），最长 31 天。</param>
    /// <param name="durationMinutes">所需最小时长，范围 15 至 480 分钟。</param>
    [Authorize(Policy = PermissionNames.CalendarRead), RequireHomeOwner]
    [HttpGet("availability")]
    public async Task<ActionResult<ApiResponse<object>>> Availability(long homeId, DateTime? from, DateTime? to, int durationMinutes = 60) => Reply(await UserAsync((_, token) => _schedule.ListAvailabilityAsync(homeId, from, to, durationMinutes, token)));

    /// <summary>创建家庭证件到期提醒，不接受证件号码、照片或原件。</summary>
    /// <param name="homeId">家庭主键，必须等于 JWT tenant_id。</param>
    /// <param name="request">证件到期提醒请求体。</param>
    [Authorize(Policy = PermissionNames.CalendarWrite), RequireHomeOwner]
    [HttpPost("document-deadlines")]
    public async Task<ActionResult<ApiResponse<object>>> CreateDocumentDeadline(long homeId, FamilyDocumentDeadlineCreateRequest request) => Reply(await UserAsync((user, token) => _schedule.CreateDocumentDeadlineAsync(homeId, user.UserId, request, token)));

    /// <summary>列出家庭证件到期提醒记录。</summary>
    /// <param name="homeId">家庭主键，必须等于 JWT tenant_id。</param>
    [Authorize(Policy = PermissionNames.CalendarRead), RequireHomeOwner]
    [HttpGet("document-deadlines")]
    public async Task<ActionResult<ApiResponse<object>>> DocumentDeadlines(long homeId) => Reply(await UserAsync((_, token) => _schedule.ListDocumentDeadlinesAsync(homeId, token)));

    /// <summary>列出缴费和证件到期提醒，并幂等投影 L1 确认卡。</summary>
    /// <param name="homeId">家庭主键，必须等于 JWT tenant_id。</param>
    /// <param name="asOf">提醒计算基准日期（UTC），可用于预览。</param>
    [Authorize(Policy = PermissionNames.CalendarRead), RequireHomeOwner]
    [HttpGet("reminders")]
    public async Task<ActionResult<ApiResponse<object>>> Reminders(long homeId, DateTime? asOf = null) => Reply(await UserAsync((_, token) => _schedule.ListRemindersAsync(homeId, asOf, token)));

    /// <summary>生成睡前明日预览，包含日程、冲突和到期提醒。</summary>
    /// <param name="homeId">家庭主键，必须等于 JWT tenant_id。</param>
    /// <param name="asOf">预览基准日期（UTC），明日按该日期加一天计算。</param>
    [Authorize(Policy = PermissionNames.CalendarRead), RequireHomeOwner]
    [HttpGet("tomorrow-preview")]
    public async Task<ActionResult<ApiResponse<object>>> TomorrowPreview(long homeId, DateTime? asOf = null) => Reply(await UserAsync((_, token) => _schedule.GetTomorrowPreviewAsync(homeId, asOf, token)));

    private async Task<ServiceResult> UserAsync(Func<UserContext, CancellationToken, Task<ServiceResult>> action) => TryGetUser(out var user) ? await action(user, HttpContext.RequestAborted) : new ServiceResult(401, "未提供有效访问令牌。");
    private static ActionResult<ApiResponse<object>> Reply(ServiceResult result) => result.Succeeded ? new ObjectResult(new ApiResponse<object>(0, result.Message, result.Data)) { StatusCode = result.StatusCode } : new ObjectResult(ApiResponse<object>.Fail(result.Code, result.Message)) { StatusCode = result.StatusCode };
}
