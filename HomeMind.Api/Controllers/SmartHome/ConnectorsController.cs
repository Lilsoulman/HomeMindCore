using System;
using System.Threading;
using System.Threading.Tasks;
using HomeMind.Api.Controllers.Base;
using HomeMind.Api.Services;
using HomeMind.Business.IServices.SmartHome;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.SmartHome;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HomeMind.Api.Controllers.SmartHome;

/// <summary>管理家庭的 Connector 目录、连接实例和成员授权范围；不会返回凭据引用或供应商认证数据。</summary>
[Authorize]
[Route("api/v1")]
public sealed class ConnectorsController : ApiControllerBase
{
    private readonly IConnectorServices _connectors;
    private readonly IConnectorRuntimeServices _runtime;

    public ConnectorsController(IConnectorServices connectors, IConnectorRuntimeServices runtime)
    {
        _connectors = connectors;
        _runtime = runtime;
    }

    [Authorize(Policy = PermissionNames.ConnectorRead)]
    [HttpGet("connector-providers")]
    public async Task<ActionResult<ApiResponse<object>>> ListProviders() =>
        ToResponse(await _connectors.ListProvidersAsync(HttpContext.RequestAborted));

    [Authorize(Policy = PermissionNames.ConnectorRead)]
    [HttpGet("connectors")]
    public async Task<ActionResult<ApiResponse<object>>> ListConnectors() =>
        ToResponse(await WithUserAsync((user, token) => _connectors.ListConnectorsAsync(user.UserId, user.TenantId, user.Role is "owner" or "admin", token)));

    [Authorize(Policy = PermissionNames.ConnectorWrite)]
    [HttpPost("connectors")]
    public async Task<ActionResult<ApiResponse<object>>> CreateConnector(CreateConnectorRequest request) =>
        ToResponse(await WithUserAsync((user, token) => _connectors.CreateConnectorAsync(user.UserId, user.TenantId, request, token)));

    [Authorize(Policy = PermissionNames.ConnectorWrite)]
    [HttpPost("connectors/{id:long}/test")]
    public async Task<ActionResult<ApiResponse<object>>> TestConnection(long id) =>
        ToResponse(await WithUserAsync((user, token) => _runtime.TestConnectionAsync(user.TenantId, id, token)));

    [Authorize(Policy = PermissionNames.ConnectorWrite)]
    [HttpPost("connectors/{id:long}/discovery")]
    public async Task<ActionResult<ApiResponse<object>>> DiscoverDevices(long id) =>
        ToResponse(await WithUserAsync((user, token) => _runtime.DiscoverDevicesAsync(user.TenantId, id, token)));

    [Authorize(Policy = PermissionNames.ConnectorWrite)]
    [HttpPost("connectors/{id:long}/sync")]
    public async Task<ActionResult<ApiResponse<object>>> SyncStates(long id) =>
        ToResponse(await WithUserAsync((user, token) => _runtime.SyncStatesAsync(user.TenantId, id, token)));

    [Authorize(Policy = PermissionNames.ConnectorRead)]
    [HttpGet("connectors/sync-jobs/{jobId:long}")]
    public async Task<ActionResult<ApiResponse<object>>> GetSyncJob(long jobId) =>
        ToResponse(await WithUserAsync((user, token) => _runtime.GetSyncJobAsync(user.TenantId, jobId, token)));

    [Authorize(Policy = PermissionNames.ConnectorRead)]
    [HttpGet("connectors/{id:long}/authorization")]
    public async Task<ActionResult<ApiResponse<object>>> GetMyAuthorization(long id) =>
        ToResponse(await WithUserAsync((user, token) => _connectors.GetMyAuthorizationAsync(user.UserId, user.TenantId, id, token)));

    [Authorize(Policy = PermissionNames.ConnectorWrite)]
    [HttpPut("connectors/{id:long}/authorizations/{memberUserId:long}")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateAuthorization(long id, long memberUserId, ConnectorAuthorizationRequest request) =>
        ToResponse(await WithUserAsync((user, token) => _connectors.UpdateAuthorizationAsync(user.TenantId, id, memberUserId, request, token)));

    private async Task<ServiceResult> WithUserAsync(Func<UserContext, CancellationToken, Task<ServiceResult>> action) => TryGetUser(out var user)
        ? await action(user, HttpContext.RequestAborted)
        : new ServiceResult(401, "未提供访问令牌，或访问令牌已过期。");

    private static ActionResult<ApiResponse<object>> ToResponse(ServiceResult result) => result.Succeeded
        ? new ObjectResult(new ApiResponse<object>(0, result.Message, result.Data)) { StatusCode = result.StatusCode }
        : new ObjectResult(ApiResponse<object>.Fail(result.StatusCode, result.Message)) { StatusCode = result.StatusCode };
}
