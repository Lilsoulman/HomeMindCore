using System;
using System.Threading;
using System.Threading.Tasks;
using HomeMind.Api.Controllers.Base;
using HomeMind.Api.Services;
using HomeMind.Business.IServices.Dashboard;
using HomeMind.Common.Model.ViewModel.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HomeMind.Api.Controllers.Dashboard;

/// <summary>Dashboard aggregation with independently degradable modules.</summary>
[Authorize]
[Route("api/v1/dashboard")]
public sealed class DashboardController : ApiControllerBase
{
    private readonly IDashboardServices _dashboard;

    public DashboardController(IDashboardServices dashboard) => _dashboard = dashboard;

    [Authorize(Policy = PermissionNames.SmartHomeRead)]
    [HttpGet]
    public async Task<ActionResult<ApiResponse<object>>> Get() =>
        ToResponse(await WithUserAsync((user, token) => _dashboard.GetAsync(user.UserId, user.TenantId, token)));

    private async Task<ServiceResult> WithUserAsync(Func<UserContext, CancellationToken, Task<ServiceResult>> action) =>
        TryGetUser(out var user)
            ? await action(user, HttpContext.RequestAborted)
            : new ServiceResult(401, "未提供访问令牌，或访问令牌已过期。");

    private static ActionResult<ApiResponse<object>> ToResponse(ServiceResult result) => result.Succeeded
        ? new ObjectResult(new ApiResponse<object>(0, result.Message, result.Data)) { StatusCode = result.StatusCode }
        : new ObjectResult(ApiResponse<object>.Fail(result.StatusCode, result.Message)) { StatusCode = result.StatusCode };
}
