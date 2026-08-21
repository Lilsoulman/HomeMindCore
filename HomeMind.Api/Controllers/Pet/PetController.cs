using System;
using System.Threading;
using System.Threading.Tasks;
using HomeMind.Api.Authorization;
using HomeMind.Api.Controllers.Base;
using HomeMind.Api.Services;
using HomeMind.Business.IServices.Pet;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.Pet;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HomeMind.Api.Controllers.Pet;

/// <summary>家庭宠物管家 API，提供档案、照护日历和用品预测。</summary>
[Authorize]
[Route("api/v1/homes/{homeId:long}/pets")]
public sealed class PetController : ApiControllerBase
{
    private readonly IPetServices _pets;
    /// <summary>构造宠物管家控制器。</summary>
    public PetController(IPetServices pets) => _pets = pets;

    /// <summary>创建宠物档案。</summary>
    [Authorize(Policy = PermissionNames.PetWrite), RequireHomeOwner]
    [HttpPost]
    public async Task<ActionResult<ApiResponse<object>>> Create(long homeId, PetCreateRequest request) => ToResponse(await WithUserAsync((user, token) => _pets.CreateAsync(homeId, user.UserId, request, token)));

    /// <summary>列出家庭宠物档案。</summary>
    [Authorize(Policy = PermissionNames.PetRead), RequireHomeOwner]
    [HttpGet]
    public async Task<ActionResult<ApiResponse<object>>> List(long homeId) => ToResponse(await WithUserAsync((_, token) => _pets.ListAsync(homeId, token)));

    /// <summary>新增疫苗或驱虫照护日历记录。</summary>
    [Authorize(Policy = PermissionNames.PetWrite), RequireHomeOwner]
    [HttpPost("{petId:long}/care-events")]
    public async Task<ActionResult<ApiResponse<object>>> AddCare(long homeId, long petId, PetCareEventCreateRequest request) => ToResponse(await WithUserAsync((user, token) => _pets.AddCareEventAsync(homeId, user.UserId, petId, request, token)));

    /// <summary>列出宠物照护日历。</summary>
    [Authorize(Policy = PermissionNames.PetRead), RequireHomeOwner]
    [HttpGet("{petId:long}/care-events")]
    public async Task<ActionResult<ApiResponse<object>>> Care(long homeId, long petId) => ToResponse(await WithUserAsync((_, token) => _pets.ListCareEventsAsync(homeId, petId, token)));

    /// <summary>更新宠物用品库存与日均消耗。</summary>
    [Authorize(Policy = PermissionNames.PetWrite), RequireHomeOwner]
    [HttpPut("{petId:long}/supplies")]
    public async Task<ActionResult<ApiResponse<object>>> Supply(long homeId, long petId, PetSupplyUpsertRequest request) => ToResponse(await WithUserAsync((user, token) => _pets.UpsertSupplyAsync(homeId, user.UserId, petId, request, token)));

    /// <summary>列出用品库存和预计剩余天数。</summary>
    [Authorize(Policy = PermissionNames.PetRead), RequireHomeOwner]
    [HttpGet("{petId:long}/supplies")]
    public async Task<ActionResult<ApiResponse<object>>> Supplies(long homeId, long petId) => ToResponse(await WithUserAsync((_, token) => _pets.ListSuppliesAsync(homeId, petId, token)));

    /// <summary>列出七天内照护与断粮提醒。</summary>
    [Authorize(Policy = PermissionNames.PetRead), RequireHomeOwner]
    [HttpGet("alerts")]
    public async Task<ActionResult<ApiResponse<object>>> Alerts(long homeId, DateTime? asOf = null) => ToResponse(await WithUserAsync((_, token) => _pets.ListAlertsAsync(homeId, asOf, token)));

    private async Task<ServiceResult> WithUserAsync(Func<UserContext, CancellationToken, Task<ServiceResult>> action) => TryGetUser(out var user) ? await action(user, HttpContext.RequestAborted) : new ServiceResult(401, "未提供有效访问令牌。");
    private static ActionResult<ApiResponse<object>> ToResponse(ServiceResult result) => result.Succeeded ? new ObjectResult(new ApiResponse<object>(0, result.Message, result.Data)) { StatusCode = result.StatusCode } : new ObjectResult(ApiResponse<object>.Fail(result.Code, result.Message)) { StatusCode = result.StatusCode };
}
