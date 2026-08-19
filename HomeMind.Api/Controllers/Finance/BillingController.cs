using System;
using System.Threading;
using System.Threading.Tasks;
using HomeMind.Api.Authorization;
using HomeMind.Api.Controllers.Base;
using HomeMind.Api.Services;
using HomeMind.Business.IServices.Finance;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.Finance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HomeMind.Api.Controllers.Finance;

/// <summary>家庭缴费管家 API；仅管理本地账单日历与缴后记录，不提供第三方支付入口。</summary>
[Authorize]
[Route("api/v1/homes/{homeId:long}/billing")]
public sealed class BillingController : ApiControllerBase
{
    private readonly IBillingServices _billing;

    /// <summary>构造缴费管家控制器。</summary>
    /// <param name="billing">缴费管家服务。</param>
    public BillingController(IBillingServices billing) => _billing = billing;

    /// <summary>创建缴费账户并写入家庭到期日历。</summary>
    /// <remarks>权限：<c>finance.write</c>。<c>sourceType=ocr</c> 仅接收用户设备本地 OCR 后的结构化字段，不上传原始缴费单。</remarks>
    /// <param name="homeId">路径家庭标识，必须等于 JWT tenant_id。</param>
    /// <param name="request">缴费账户建档参数。</param>
    /// <returns>创建后的缴费账户视图；参数非法时返回 422。</returns>
    [Authorize(Policy = PermissionNames.FinanceWrite)]
    [RequireHomeOwner]
    [HttpPost("accounts")]
    public async Task<ActionResult<ApiResponse<object>>> CreateAccount(long homeId, BillingAccountCreateRequest request) =>
        ToResponse(await WithUserAsync((user, token) => _billing.CreateAccountAsync(homeId, user.UserId, request, token)));

    /// <summary>列出家庭缴费账户及其下一到期日。</summary>
    /// <remarks>权限：<c>finance.read</c>。响应不包含缴费账户号码、票据原文或第三方凭据。</remarks>
    /// <param name="homeId">路径家庭标识，必须等于 JWT tenant_id。</param>
    /// <returns>缴费账户到期日历视图。</returns>
    [Authorize(Policy = PermissionNames.FinanceRead)]
    [RequireHomeOwner]
    [HttpGet("accounts")]
    public async Task<ActionResult<ApiResponse<object>>> ListAccounts(long homeId) =>
        ToResponse(await WithUserAsync((_, token) => _billing.ListAccountsAsync(homeId, token)));

    /// <summary>登记一次已完成缴费，并同步写入家庭财务账单。</summary>
    /// <remarks>权限：<c>finance.write</c>。本操作不触发支付；同一账户和到期日重复登记返回 409。</remarks>
    /// <param name="homeId">路径家庭标识，必须等于 JWT tenant_id。</param>
    /// <param name="accountId">缴费账户主键。</param>
    /// <param name="request">缴费完成登记参数。</param>
    /// <returns>缴费记录视图；成功时返回 201。</returns>
    [Authorize(Policy = PermissionNames.FinanceWrite)]
    [RequireHomeOwner]
    [HttpPost("accounts/{accountId:long}/payments")]
    public async Task<ActionResult<ApiResponse<object>>> RecordPayment(long homeId, long accountId, BillingPaymentRecordRequest request) =>
        ToResponse(await WithUserAsync((user, token) => _billing.RecordPaymentAsync(homeId, user.UserId, accountId, request, token)));

    /// <summary>获取提前三天和提前一天的到期提醒。</summary>
    /// <remarks>权限：<c>finance.read</c>。推送通道未实现前，提醒幂等投影为既有确认中心的 L1 卡片，不会自动缴费。</remarks>
    /// <param name="homeId">路径家庭标识，必须等于 JWT tenant_id。</param>
    /// <param name="asOf">用于计算提醒日期的 UTC 日期，省略时使用当前日期。</param>
    /// <returns>到期提醒及对应确认卡主键。</returns>
    [Authorize(Policy = PermissionNames.FinanceRead)]
    [RequireHomeOwner]
    [HttpGet("reminders")]
    public async Task<ActionResult<ApiResponse<object>>> Reminders(long homeId, DateTime? asOf = null) =>
        ToResponse(await WithUserAsync((_, token) => _billing.ListRemindersAsync(homeId, asOf, token)));

    /// <summary>获取某年度的缴费金额趋势。</summary>
    /// <remarks>权限：<c>finance.read</c>。趋势只统计已登记缴费记录，按实际缴费月份聚合。</remarks>
    /// <param name="homeId">路径家庭标识，必须等于 JWT tenant_id。</param>
    /// <param name="year">四位年份，省略时使用当前年份。</param>
    /// <returns>年度总金额和月度聚合行。</returns>
    [Authorize(Policy = PermissionNames.FinanceRead)]
    [RequireHomeOwner]
    [HttpGet("trend")]
    public async Task<ActionResult<ApiResponse<object>>> Trend(long homeId, int? year = null) =>
        ToResponse(await WithUserAsync((_, token) => _billing.GetAnnualTrendAsync(homeId, year, token)));

    /// <summary>从认证上下文取得用户后执行服务调用。</summary>
    private async Task<ServiceResult> WithUserAsync(Func<UserContext, CancellationToken, Task<ServiceResult>> action) =>
        TryGetUser(out var user) ? await action(user, HttpContext.RequestAborted) : new ServiceResult(401, "未提供有效访问令牌。");

    /// <summary>转换业务服务结果为统一 API 响应。</summary>
    private static ActionResult<ApiResponse<object>> ToResponse(ServiceResult result) => result.Succeeded
        ? new ObjectResult(new ApiResponse<object>(0, result.Message, result.Data)) { StatusCode = result.StatusCode }
        : new ObjectResult(ApiResponse<object>.Fail(result.Code, result.Message)) { StatusCode = result.StatusCode };
}
