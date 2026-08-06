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
/// <remarks>凭据引用必须为 <c>vault://tenants/{tenantId}/...</c>；API 永不返回明文凭据。</remarks>
[Authorize]
[Route("api/v1")]
public sealed class ConnectorsController : ApiControllerBase
{
    private readonly IConnectorServices _connectors;
    private readonly IConnectorRuntimeServices _runtime;

    /// <summary>构造连接器控制器。</summary>
    /// <param name="connectors">连接器目录与授权服务。</param>
    /// <param name="runtime">连接器运行时（连接测试、发现、同步）服务。</param>
    public ConnectorsController(IConnectorServices connectors, IConnectorRuntimeServices runtime)
    {
        _connectors = connectors;
        _runtime = runtime;
    }

    /// <summary>列出平台支持的连接器提供方目录。</summary>
    /// <remarks>权限：<c>connector.read</c>。</remarks>
    /// <returns>提供方列表统一响应。</returns>
    [Authorize(Policy = PermissionNames.ConnectorRead)]
    [HttpGet("connector-providers")]
    public async Task<ActionResult<ApiResponse<object>>> ListProviders() =>
        ToResponse(await _connectors.ListProvidersAsync(HttpContext.RequestAborted));

    /// <summary>列出当前租户内可见的工作区连接器实例。</summary>
    /// <remarks>权限：<c>connector.read</c>。所有者或管理员可见全部连接器；其他成员仅可见自己被授权的连接器。</remarks>
    /// <returns>工作区连接器列表统一响应。</returns>
    [Authorize(Policy = PermissionNames.ConnectorRead)]
    [HttpGet("connectors")]
    public async Task<ActionResult<ApiResponse<object>>> ListConnectors() =>
        ToResponse(await WithUserAsync((user, token) => _connectors.ListConnectorsAsync(user.UserId, user.TenantId, user.Role is "owner" or "admin", token)));

    /// <summary>创建一个工作区连接器；凭据仅以 <c>credentialRef</c> 形式存储。</summary>
    /// <remarks>权限：<c>connector.write</c>。在 Vault 未启用时返回 503 + 50001。</remarks>
    /// <param name="request">连接器创建请求体。</param>
    /// <returns>新建连接器统一响应。</returns>
    [Authorize(Policy = PermissionNames.ConnectorWrite)]
    [HttpPost("connectors")]
    public async Task<ActionResult<ApiResponse<object>>> CreateConnector(CreateConnectorRequest request) =>
        ToResponse(await WithUserAsync((user, token) => _connectors.CreateConnectorAsync(user.UserId, user.TenantId, request, token)));

    /// <summary>对指定连接器执行连接测试，验证凭据与端点可达性。</summary>
    /// <remarks>权限：<c>connector.write</c>。仅更新最近一次健康探测时间，不返回凭据。</remarks>
    /// <param name="id">连接器主键。</param>
    /// <returns>连接测试结果统一响应。</returns>
    [Authorize(Policy = PermissionNames.ConnectorWrite)]
    [HttpPost("connectors/{id:long}/test")]
    public async Task<ActionResult<ApiResponse<object>>> TestConnection(long id) =>
        ToResponse(await WithUserAsync((user, token) => _runtime.TestConnectionAsync(user.TenantId, id, token)));

    /// <summary>触发一次设备发现并落库归一化设备表。</summary>
    /// <remarks>权限：<c>connector.write</c>。发现过程不返回厂商原始字段。</remarks>
    /// <param name="id">连接器主键。</param>
    /// <returns>发现结果统一响应。</returns>
    [Authorize(Policy = PermissionNames.ConnectorWrite)]
    [HttpPost("connectors/{id:long}/discovery")]
    public async Task<ActionResult<ApiResponse<object>>> DiscoverDevices(long id) =>
        ToResponse(await WithUserAsync((user, token) => _runtime.DiscoverDevicesAsync(user.TenantId, id, token)));

    /// <summary>提交一次连接器状态同步任务；返回 202 与同步任务视图。</summary>
    /// <remarks>权限：<c>connector.write</c>。任务由 <c>AutomationWorker</c> 异步消费，最多 3 次重试与 30 秒操作超时。</remarks>
    /// <param name="id">连接器主键。</param>
    /// <returns>同步任务视图统一响应。</returns>
    [Authorize(Policy = PermissionNames.ConnectorWrite)]
    [HttpPost("connectors/{id:long}/sync")]
    public async Task<ActionResult<ApiResponse<object>>> SyncStates(long id) =>
        ToResponse(await WithUserAsync((user, token) => _runtime.SyncStatesAsync(user.TenantId, id, token)));

    /// <summary>按主键获取连接器同步任务状态。</summary>
    /// <remarks>权限：<c>connector.read</c>。跨租户任务 ID 返回 404。</remarks>
    /// <param name="jobId">同步任务主键。</param>
    /// <returns>同步任务视图统一响应。</returns>
    [Authorize(Policy = PermissionNames.ConnectorRead)]
    [HttpGet("connectors/sync-jobs/{jobId:long}")]
    public async Task<ActionResult<ApiResponse<object>>> GetSyncJob(long jobId) =>
        ToResponse(await WithUserAsync((user, token) => _runtime.GetSyncJobAsync(user.TenantId, jobId, token)));

    /// <summary>获取当前用户对指定连接器的授权范围。</summary>
    /// <remarks>权限：<c>connector.read</c>。</remarks>
    /// <param name="id">连接器主键。</param>
    /// <returns>当前用户授权视图统一响应。</returns>
    [Authorize(Policy = PermissionNames.ConnectorRead)]
    [HttpGet("connectors/{id:long}/authorization")]
    public async Task<ActionResult<ApiResponse<object>>> GetMyAuthorization(long id) =>
        ToResponse(await WithUserAsync((user, token) => _connectors.GetMyAuthorizationAsync(user.UserId, user.TenantId, id, token)));

    /// <summary>更新指定成员对连接器的授权范围。仅所有者或管理员可调用。</summary>
    /// <remarks>权限：<c>connector.write</c>。会写入授权审计条目。</remarks>
    /// <param name="id">连接器主键。</param>
    /// <param name="memberUserId">被授权成员的用户主键。</param>
    /// <param name="request">授权范围请求体。</param>
    /// <returns>更新后授权视图统一响应。</returns>
    [Authorize(Policy = PermissionNames.ConnectorWrite)]
    [HttpPut("connectors/{id:long}/authorizations/{memberUserId:long}")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateAuthorization(long id, long memberUserId, ConnectorAuthorizationRequest request) =>
        ToResponse(await WithUserAsync((user, token) => _connectors.UpdateAuthorizationAsync(user.TenantId, id, memberUserId, request, token)));

    /// <summary>在用户上下文就绪时执行给定的业务回调，否则返回 401。</summary>
    /// <param name="action">执行业务逻辑的回调。</param>
    /// <returns>业务执行结果 <see cref="ServiceResult"/>。</returns>
    private async Task<ServiceResult> WithUserAsync(Func<UserContext, CancellationToken, Task<ServiceResult>> action) => TryGetUser(out var user)
        ? await action(user, HttpContext.RequestAborted)
        : new ServiceResult(401, "未提供访问令牌，或访问令牌已过期。");

    /// <summary>将 <see cref="ServiceResult"/> 转换为统一 HTTP 响应。</summary>
    /// <param name="result">业务执行结果。</param>
    /// <returns>统一响应体与对应状态码。</returns>
    private static ActionResult<ApiResponse<object>> ToResponse(ServiceResult result) => result.Succeeded
        ? new ObjectResult(new ApiResponse<object>(0, result.Message, result.Data)) { StatusCode = result.StatusCode }
        : new ObjectResult(ApiResponse<object>.Fail(result.Code, result.Message)) { StatusCode = result.StatusCode };
}
