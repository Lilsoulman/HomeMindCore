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

/// <summary>家庭财务账单 API，家庭归属由 JWT 租户推导。</summary>
[Authorize]
[Route("api/v1/homes/{homeId:long}/finance")]
public sealed class FinanceController : ApiControllerBase
{
    private readonly IFinanceServices _finance;
    /// <summary>构造财务控制器。</summary>
    public FinanceController(IFinanceServices finance) => _finance = finance;

    /// <summary>导入本地解析的 CSV 账单；同一家庭内重复行安全去重。</summary>
    [Authorize(Policy = PermissionNames.FinanceWrite)]
    [RequireHomeOwner]
    [HttpPost("transactions/import")]
    public async Task<ActionResult<ApiResponse<object>>> Import(long homeId, FinanceImportRequest request) =>
        ToResponse(await WithUserAsync((user, token) => _finance.ImportAsync(homeId, user.UserId, request, token)));

    /// <summary>列出家庭账单条目，可按日期和分类过滤。</summary>
    [Authorize(Policy = PermissionNames.FinanceRead)]
    [RequireHomeOwner]
    [HttpGet("transactions")]
    public async Task<ActionResult<ApiResponse<object>>> List(long homeId, DateTime? from = null, DateTime? to = null, string? category = null) =>
        ToResponse(await WithUserAsync((user, token) => _finance.ListAsync(homeId, from, to, category, token)));

    /// <summary>生成 30 天默认窗口的支出聚合与确定性省钱建议。</summary>
    [Authorize(Policy = PermissionNames.FinanceRead)]
    [RequireHomeOwner]
    [HttpGet("summary")]
    public async Task<ActionResult<ApiResponse<object>>> Summary(long homeId, DateTime? from = null, DateTime? to = null) =>
        ToResponse(await WithUserAsync((user, token) => _finance.SummarizeAsync(homeId, from, to, token)));

    private async Task<ServiceResult> WithUserAsync(Func<UserContext, CancellationToken, Task<ServiceResult>> action) => TryGetUser(out var user) ? await action(user, HttpContext.RequestAborted) : new ServiceResult(401, "未提供有效访问令牌。");
    private static ActionResult<ApiResponse<object>> ToResponse(ServiceResult result) => result.Succeeded ? new ObjectResult(new ApiResponse<object>(0, result.Message, result.Data)) { StatusCode = result.StatusCode } : new ObjectResult(ApiResponse<object>.Fail(result.Code, result.Message)) { StatusCode = result.StatusCode };
}
