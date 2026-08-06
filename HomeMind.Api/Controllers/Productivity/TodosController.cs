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
/// <remarks>所有时间使用 UTC；软删除而非物理删除。</remarks>
[Authorize]
[Route("api/v1/todos")]
public sealed class TodosController : ApiControllerBase
{
    private readonly ITodoServices _todoServices;

    /// <summary>构造待办控制器。</summary>
    /// <param name="todoServices">待办业务服务。</param>
    public TodosController(ITodoServices todoServices) => _todoServices = todoServices;

    /// <summary>按状态、类型与时间窗口列出待办。</summary>
    /// <remarks>权限：<c>todo.read</c>。窗口参数均为 UTC。</remarks>
    /// <param name="status">按状态过滤，可空。</param>
    /// <param name="type">按类型过滤，可空。</param>
    /// <param name="from">窗口起始时间（UTC），可空。</param>
    /// <param name="to">窗口结束时间（UTC），可空。</param>
    /// <returns>待办列表的统一响应。</returns>
    [Authorize(Policy = PermissionNames.TodoRead)]
    [HttpGet]
    public async Task<ActionResult<ApiResponse<object>>> List(string? status, string? type, DateTime? from, DateTime? to)
        => ToResponse(await WithUserAsync((user, token) => _todoServices.ListAsync(user.UserId, user.TenantId, status, type, from, to, token)));

    /// <summary>创建一个待办。</summary>
    /// <remarks>权限：<c>todo.write</c>。</remarks>
    /// <param name="request">待办创建请求体。</param>
    /// <returns>新建待办的统一响应。</returns>
    [Authorize(Policy = PermissionNames.TodoWrite)]
    [HttpPost]
    public async Task<ActionResult<ApiResponse<object>>> Create(TodoWriteRequest request)
        => ToResponse(await WithUserAsync((user, token) => _todoServices.CreateAsync(user.UserId, user.TenantId, request, token)));

    /// <summary>按主键更新待办；可空字段表示不修改。</summary>
    /// <remarks>权限：<c>todo.write</c>。跨用户或跨租户待办返回 404。</remarks>
    /// <param name="id">待办主键。</param>
    /// <param name="request">待办更新请求体。</param>
    /// <returns>更新结果统一响应。</returns>
    [Authorize(Policy = PermissionNames.TodoWrite)]
    [HttpPut("{id:long}")]
    public async Task<ActionResult<ApiResponse<object>>> Update(long id, TodoWriteRequest request)
        => ToResponse(await WithUserAsync((user, token) => _todoServices.UpdateAsync(user.UserId, user.TenantId, id, request, token)));

    /// <summary>软删除指定待办。</summary>
    /// <remarks>权限：<c>todo.write</c>。</remarks>
    /// <param name="id">待办主键。</param>
    /// <returns>删除结果统一响应。</returns>
    [Authorize(Policy = PermissionNames.TodoWrite)]
    [HttpDelete("{id:long}")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id)
        => ToResponse(await WithUserAsync((user, token) => _todoServices.DeleteAsync(user.UserId, user.TenantId, id, token)));

    /// <summary>在指定待办下追加一个子任务。</summary>
    /// <remarks>权限：<c>todo.write</c>。</remarks>
    /// <param name="id">父待办主键。</param>
    /// <param name="request">子任务创建请求体。</param>
    /// <returns>新建子任务统一响应。</returns>
    [Authorize(Policy = PermissionNames.TodoWrite)]
    [HttpPost("{id:long}/subtasks")]
    public async Task<ActionResult<ApiResponse<object>>> AddSubtask(long id, SubtaskWriteRequest request)
        => ToResponse(await WithUserAsync((user, token) => _todoServices.AddSubtaskAsync(user.UserId, user.TenantId, id, request, token)));

    /// <summary>更新指定待办下的子任务；可空字段表示不修改。</summary>
    /// <remarks>权限：<c>todo.write</c>。</remarks>
    /// <param name="id">父待办主键。</param>
    /// <param name="subId">子任务主键。</param>
    /// <param name="request">子任务更新请求体。</param>
    /// <returns>更新结果统一响应。</returns>
    [Authorize(Policy = PermissionNames.TodoWrite)]
    [HttpPut("{id:long}/subtasks/{subId:long}")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateSubtask(long id, long subId, SubtaskWriteRequest request)
        => ToResponse(await WithUserAsync((user, token) => _todoServices.UpdateSubtaskAsync(user.UserId, user.TenantId, id, subId, request, token)));

    /// <summary>软删除指定待办下的子任务。</summary>
    /// <remarks>权限：<c>todo.write</c>。</remarks>
    /// <param name="id">父待办主键。</param>
    /// <param name="subId">子任务主键。</param>
    /// <returns>删除结果统一响应。</returns>
    [Authorize(Policy = PermissionNames.TodoWrite)]
    [HttpDelete("{id:long}/subtasks/{subId:long}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteSubtask(long id, long subId)
        => ToResponse(await WithUserAsync((user, token) => _todoServices.DeleteSubtaskAsync(user.UserId, user.TenantId, id, subId, token)));

    /// <summary>在用户上下文就绪时执行给定的业务回调，否则返回 401。</summary>
    /// <param name="action">执行业务逻辑的回调。</param>
    /// <returns>业务执行结果 <see cref="ServiceResult"/>。</returns>
    private async Task<ServiceResult> WithUserAsync(Func<UserContext, CancellationToken, Task<ServiceResult>> action)
    {
        return TryGetUser(out var user)
            ? await action(user, HttpContext.RequestAborted)
            : new ServiceResult(401, "未提供访问令牌，或访问令牌已过期。");
    }

    /// <summary>将 <see cref="ServiceResult"/> 转换为统一 HTTP 响应。</summary>
    /// <param name="result">业务执行结果。</param>
    /// <returns>统一响应体与对应状态码。</returns>
    private ActionResult<ApiResponse<object>> ToResponse(ServiceResult result) => StatusCode(result.StatusCode, result.Succeeded
        ? new ApiResponse<object>(0, result.Message, result.Data)
        : ApiResponse<object>.Fail(result.Code, result.Message));
}
