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

/// <summary>Home+ 所需的标准化空间、设备与场景只读接口，以及场景运行的受控入口。</summary>
/// <remarks>读接口不返回凭据、厂商实体 ID、协议字段或原始设备状态。除非显式启用 Mock，否则不返回演示数据。</remarks>
[Authorize]
[Route("api/v1/smart-home")]
public sealed class SmartHomeController : ApiControllerBase
{
    private readonly ISmartHomeReadServices _smartHome;
    private readonly ISmartHomeSceneServices _scenes;

    /// <summary>构造智能家居只读控制器。</summary>
    /// <param name="smartHome">智能家居只读服务。</param>
    /// <param name="scenes">场景服务。</param>
    public SmartHomeController(ISmartHomeReadServices smartHome, ISmartHomeSceneServices scenes)
    {
        _smartHome = smartHome;
        _scenes = scenes;
    }

    /// <summary>列出当前租户下的空间。</summary>
    /// <remarks>权限：<c>smart_home.read</c>。</remarks>
    /// <returns>空间列表统一响应。</returns>
    [Authorize(Policy = PermissionNames.SmartHomeRead)]
    [HttpGet("spaces")]
    public async Task<ActionResult<ApiResponse<object>>> ListSpaces() => ToResponse(await WithUserAsync((user, token) => _smartHome.ListSpacesAsync(user.TenantId, token)));

    /// <summary>列出当前租户下、归一化后的设备视图；支持按空间筛选。</summary>
    /// <remarks>权限：<c>smart_home.read</c>。</remarks>
    /// <param name="spaceId">可选的空间主键过滤条件。</param>
    /// <returns>设备列表统一响应。</returns>
    [Authorize(Policy = PermissionNames.SmartHomeRead)]
    [HttpGet("devices")]
    public async Task<ActionResult<ApiResponse<object>>> ListDevices(long? spaceId) => ToResponse(await WithUserAsync((user, token) => _smartHome.ListDevicesAsync(user.TenantId, spaceId, token)));

    /// <summary>列出当前租户下可用的场景，包括内置场景。</summary>
    /// <remarks>权限：<c>smart_home.read</c>。</remarks>
    /// <returns>场景列表统一响应。</returns>
    [Authorize(Policy = PermissionNames.SmartHomeRead)]
    [HttpGet("scenes")]
    public async Task<ActionResult<ApiResponse<object>>> ListScenes() => ToResponse(await WithUserAsync((user, token) => _smartHome.ListScenesAsync(user.TenantId, token)));

    /// <summary>聚合设备健康摘要；支持按空间筛选。</summary>
    /// <remarks>权限：<c>smart_home.read</c>。</remarks>
    /// <param name="spaceId">可选的空间主键过滤条件。</param>
    /// <returns>设备健康摘要统一响应。</returns>
    [Authorize(Policy = PermissionNames.SmartHomeRead)]
    [HttpGet("devices/health")]
    public async Task<ActionResult<ApiResponse<object>>> GetDeviceHealth(long? spaceId) => ToResponse(await WithUserAsync((user, token) => _smartHome.GetDeviceHealthAsync(user.TenantId, spaceId, token)));

    /// <summary>获取单台设备的标准化健康详情。</summary>
    /// <remarks>权限：<c>smart_home.read</c>。跨家庭或不存在返回 404；采样时间为最近状态时间，过期状态不得描述为实时。</remarks>
    /// <param name="deviceId">目标设备主键。</param>
    /// <returns>设备健康详情统一响应；不存在返回 404。</returns>
    [Authorize(Policy = PermissionNames.SmartHomeRead)]
    [HttpGet("devices/{deviceId:long}/health")]
    public async Task<ActionResult<ApiResponse<object>>> GetDeviceHealthDetail(long deviceId) => ToResponse(await WithUserAsync((user, token) => _smartHome.GetDeviceHealthDetailAsync(user.TenantId, deviceId, token)));

    /// <summary>执行一个内置场景；将创建无凭据的管家运行并产生待确认动作。</summary>
    /// <remarks>权限：<c>ai.run</c>。支持的快捷方式：<c>arrive_home</c>、<c>leave_home</c>、<c>sleep</c>。</remarks>
    /// <param name="sceneKey">场景业务键或管家意图快捷方式。</param>
    /// <param name="request">运行请求体，可选幂等键。</param>
    /// <returns>运行创建结果统一响应。</returns>
    [Authorize(Policy = PermissionNames.AiRun)]
    [HttpPost("scenes/{sceneKey}/run")]
    public async Task<ActionResult<ApiResponse<object>>> RunScene(string sceneKey, SceneRunRequest request) =>
        ToResponse(await WithUserAsync((user, token) => _scenes.RunAsync(user.UserId, user.TenantId, sceneKey, request, token)));

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
