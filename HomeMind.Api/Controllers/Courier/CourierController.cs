using System;
using System.Threading;
using System.Threading.Tasks;
using HomeMind.Api.Authorization;
using HomeMind.Api.Controllers.Base;
using HomeMind.Api.Services;
using HomeMind.Business.IServices.Courier;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.Courier;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HomeMind.Api.Controllers.Courier;

/// <summary>个人快递管家 API；运单只对登记用户可见。</summary>
[Authorize]
[Route("api/v1/homes/{homeId:long}/courier")]
public sealed class CourierController : ApiControllerBase
{
    private readonly ICourierServices _courier;
    /// <summary>构造快递管家控制器。</summary>
    public CourierController(ICourierServices courier) => _courier = courier;
    /// <summary>登记个人运单；家庭归属来自 JWT 租户，完整运单号不进入响应。</summary>
    /// <param name="homeId">路径家庭标识，必须等于 JWT tenant_id。</param>
    /// <param name="request">运单登记参数。</param>
    [Authorize(Policy = PermissionNames.ConnectorWrite), RequireHomeOwner, HttpPost("shipments")]
    public async Task<ActionResult<ApiResponse<object>>> Create(long homeId, CourierShipmentCreateRequest request) => ToResponse(await WithUserAsync((user, token) => _courier.CreateAsync(homeId, user.UserId, request, token)));
    /// <summary>列出当前用户的个人运单。</summary>
    /// <param name="homeId">路径家庭标识，必须等于 JWT tenant_id。</param>
    [Authorize(Policy = PermissionNames.ConnectorRead), RequireHomeOwner, HttpGet("shipments")]
    public async Task<ActionResult<ApiResponse<object>>> List(long homeId) => ToResponse(await WithUserAsync((user, token) => _courier.ListAsync(homeId, user.UserId, token)));
    /// <summary>刷新运单物流状态并生成异常建议卡。</summary>
    /// <param name="homeId">路径家庭标识，必须等于 JWT tenant_id。</param>
    /// <param name="shipmentId">当前用户的运单主键。</param>
    [Authorize(Policy = PermissionNames.ConnectorRead), RequireHomeOwner, HttpPost("shipments/{shipmentId:long}/refresh")]
    public async Task<ActionResult<ApiResponse<object>>> Refresh(long homeId, long shipmentId) => ToResponse(await WithUserAsync((user, token) => _courier.RefreshAsync(homeId, user.UserId, shipmentId, token)));
    /// <summary>列出当前用户的快递异常建议。</summary>
    /// <param name="homeId">路径家庭标识，必须等于 JWT tenant_id。</param>
    [Authorize(Policy = PermissionNames.ConfirmationRead), RequireHomeOwner, HttpGet("anomalies")]
    public async Task<ActionResult<ApiResponse<object>>> Anomalies(long homeId) => ToResponse(await WithUserAsync((user, token) => _courier.ListAnomaliesAsync(homeId, user.UserId, token)));
    private async Task<ServiceResult> WithUserAsync(Func<UserContext, CancellationToken, Task<ServiceResult>> action) => TryGetUser(out var user) ? await action(user, HttpContext.RequestAborted) : new ServiceResult(401, "未提供有效访问令牌。");
    private static ActionResult<ApiResponse<object>> ToResponse(ServiceResult result) => result.Succeeded ? new ObjectResult(new ApiResponse<object>(0, result.Message, result.Data)) { StatusCode = result.StatusCode } : new ObjectResult(ApiResponse<object>.Fail(result.Code, result.Message)) { StatusCode = result.StatusCode };
}
