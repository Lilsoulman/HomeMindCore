using System;
using System.Threading;
using System.Threading.Tasks;
using HomeMind.Api.Controllers.Base;
using HomeMind.Api.Services;
using HomeMind.Business.IServices.AI;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.AI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HomeMind.Api.Controllers.AI;

/// <summary>AI 技能模块，控制器不直接访问数据库。</summary>
[Authorize]
[Route("api/v1/skills")]
public sealed class SkillsController : ApiControllerBase
{
    private readonly IAiSkillServices _skillServices;
    public SkillsController(IAiSkillServices skillServices) => _skillServices = skillServices;
    [Authorize(Policy = PermissionNames.AiSkillsRead)] [HttpGet]
    public async Task<ActionResult<ApiResponse<object>>> List() => ToResponse(await WithUserAsync((user, token) => _skillServices.ListAsync(user.UserId, user.TenantId, token)));
    [Authorize(Policy = PermissionNames.AiSkillsWrite)] [HttpPost]
    public async Task<ActionResult<ApiResponse<object>>> Create(SkillRequest request) => ToResponse(await WithUserAsync((user, token) => _skillServices.CreateAsync(user.UserId, user.TenantId, request, token)));
    [Authorize(Policy = PermissionNames.AiSkillsWrite)] [HttpPut("{id:long}")]
    public async Task<ActionResult<ApiResponse<object>>> Update(long id, SkillRequest request) => ToResponse(await WithUserAsync((user, token) => _skillServices.UpdateAsync(user.UserId, user.TenantId, id, request, token)));
    [Authorize(Policy = PermissionNames.AiSkillsWrite)] [HttpDelete("{id:long}")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id) => ToResponse(await WithUserAsync((user, token) => _skillServices.DeleteAsync(user.UserId, user.TenantId, id, token)));
    private async Task<ServiceResult> WithUserAsync(Func<UserContext, CancellationToken, Task<ServiceResult>> action) => TryGetUser(out var user) ? await action(user, HttpContext.RequestAborted) : new ServiceResult(401, "未提供访问令牌，或访问令牌已过期。");
    private ActionResult<ApiResponse<object>> ToResponse(ServiceResult result) => StatusCode(result.StatusCode, result.Succeeded ? new ApiResponse<object>(0, result.Message, result.Data) : ApiResponse<object>.Fail(result.StatusCode, result.Message));
}
