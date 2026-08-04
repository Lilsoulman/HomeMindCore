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

/// <summary>Home+ 所需的标准化空间、设备与场景只读接口。</summary>
[Authorize]
[Route("api/v1/smart-home")]
public sealed class SmartHomeController : ApiControllerBase
{
    private readonly ISmartHomeReadServices _smartHome;
    private readonly ISmartHomeSceneServices _scenes;

    public SmartHomeController(ISmartHomeReadServices smartHome, ISmartHomeSceneServices scenes)
    {
        _smartHome = smartHome;
        _scenes = scenes;
    }

    [Authorize(Policy = PermissionNames.SmartHomeRead)]
    [HttpGet("spaces")]
    public async Task<ActionResult<ApiResponse<object>>> ListSpaces() => ToResponse(await WithUserAsync((user, token) => _smartHome.ListSpacesAsync(user.TenantId, token)));

    [Authorize(Policy = PermissionNames.SmartHomeRead)]
    [HttpGet("devices")]
    public async Task<ActionResult<ApiResponse<object>>> ListDevices(long? spaceId) => ToResponse(await WithUserAsync((user, token) => _smartHome.ListDevicesAsync(user.TenantId, spaceId, token)));

    [Authorize(Policy = PermissionNames.SmartHomeRead)]
    [HttpGet("scenes")]
    public async Task<ActionResult<ApiResponse<object>>> ListScenes() => ToResponse(await WithUserAsync((user, token) => _smartHome.ListScenesAsync(user.TenantId, token)));

    [Authorize(Policy = PermissionNames.AiRun)]
    [HttpPost("scenes/{sceneKey}/run")]
    public async Task<ActionResult<ApiResponse<object>>> RunScene(string sceneKey, SceneRunRequest request) =>
        ToResponse(await WithUserAsync((user, token) => _scenes.RunAsync(user.UserId, user.TenantId, sceneKey, request, token)));

    private async Task<ServiceResult> WithUserAsync(Func<UserContext, CancellationToken, Task<ServiceResult>> action) => TryGetUser(out var user)
        ? await action(user, HttpContext.RequestAborted)
        : new ServiceResult(401, "未提供访问令牌，或访问令牌已过期。");

    private static ActionResult<ApiResponse<object>> ToResponse(ServiceResult result) => result.Succeeded
        ? new ObjectResult(new ApiResponse<object>(0, result.Message, result.Data)) { StatusCode = result.StatusCode }
        : new ObjectResult(ApiResponse<object>.Fail(result.StatusCode, result.Message)) { StatusCode = result.StatusCode };
}
