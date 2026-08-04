using System;
using System.Threading;
using System.Threading.Tasks;
using HomeMind.Api.Controllers.Base;
using HomeMind.Api.Services;
using HomeMind.Business.IServices.Productivity;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.Productivity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HomeMind.Api.Controllers.Productivity;

/// <summary>待办模块，只负责 HTTP 协议；待办和子任务规则由业务服务处理。</summary>
[Authorize]
[Route("api/v1/todos")]
public sealed class TodosController : ApiControllerBase
{
    private readonly ITodoServices _todoServices;
    public TodosController(ITodoServices todoServices) => _todoServices = todoServices;

    [Authorize(Policy = PermissionNames.TodoRead)]
    [HttpGet]
    public async Task<ActionResult<ApiResponse<object>>> List(string? status, string? type, DateTime? from, DateTime? to)
        => ToResponse(await WithUserAsync((user, token) => _todoServices.ListAsync(user.UserId, user.TenantId, status, type, from, to, token)));

    [Authorize(Policy = PermissionNames.TodoWrite)]
    [HttpPost]
    public async Task<ActionResult<ApiResponse<object>>> Create(TodoWriteRequest request)
        => ToResponse(await WithUserAsync((user, token) => _todoServices.CreateAsync(user.UserId, user.TenantId, request, token)));

    [Authorize(Policy = PermissionNames.TodoWrite)]
    [HttpPut("{id:long}")]
    public async Task<ActionResult<ApiResponse<object>>> Update(long id, TodoWriteRequest request)
        => ToResponse(await WithUserAsync((user, token) => _todoServices.UpdateAsync(user.UserId, user.TenantId, id, request, token)));

    [Authorize(Policy = PermissionNames.TodoWrite)]
    [HttpDelete("{id:long}")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id)
        => ToResponse(await WithUserAsync((user, token) => _todoServices.DeleteAsync(user.UserId, user.TenantId, id, token)));

    [Authorize(Policy = PermissionNames.TodoWrite)]
    [HttpPost("{id:long}/subtasks")]
    public async Task<ActionResult<ApiResponse<object>>> AddSubtask(long id, SubtaskWriteRequest request)
        => ToResponse(await WithUserAsync((user, token) => _todoServices.AddSubtaskAsync(user.UserId, user.TenantId, id, request, token)));

    [Authorize(Policy = PermissionNames.TodoWrite)]
    [HttpPut("{id:long}/subtasks/{subId:long}")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateSubtask(long id, long subId, SubtaskWriteRequest request)
        => ToResponse(await WithUserAsync((user, token) => _todoServices.UpdateSubtaskAsync(user.UserId, user.TenantId, id, subId, request, token)));

    [Authorize(Policy = PermissionNames.TodoWrite)]
    [HttpDelete("{id:long}/subtasks/{subId:long}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteSubtask(long id, long subId)
        => ToResponse(await WithUserAsync((user, token) => _todoServices.DeleteSubtaskAsync(user.UserId, user.TenantId, id, subId, token)));

    private async Task<ServiceResult> WithUserAsync(Func<UserContext, CancellationToken, Task<ServiceResult>> action)
    {
        return TryGetUser(out var user)
            ? await action(user, HttpContext.RequestAborted)
            : new ServiceResult(401, "未提供访问令牌，或访问令牌已过期。");
    }

    private ActionResult<ApiResponse<object>> ToResponse(ServiceResult result) => StatusCode(result.StatusCode, result.Succeeded
        ? new ApiResponse<object>(0, result.Message, result.Data)
        : ApiResponse<object>.Fail(result.StatusCode, result.Message));
}
