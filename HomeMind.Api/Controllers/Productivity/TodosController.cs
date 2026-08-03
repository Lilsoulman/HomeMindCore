using System;
using System.Threading.Tasks;
using Dapper;
using HomeMind.Api.Controllers.Base;
using HomeMind.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HomeMind.Api.Controllers.Productivity;

/// <summary>
/// 待办模块，管理待办事项及其子任务。
/// </summary>
[Authorize]
[Route("api/v1/todos")]
public sealed class TodosController : ApiControllerBase
{
    private readonly MySqlConnectionFactory _connections;
    public TodosController(MySqlConnectionFactory connections) => _connections = connections;

    /// <summary>
    /// 按状态、类型或到期时间范围查询当前用户的待办事项。
    /// </summary>
    [Authorize(Policy = PermissionNames.TodoRead)]
    [HttpGet]
    public async Task<ActionResult<ApiResponse<object>>> List(string? status, string? type, DateTime? from, DateTime? to)
    {
        if (!TryGetUser(out var user)) return UnauthorizedResult<object>();
        await using var db = _connections.Open();
        var items = await db.QueryAsync("SELECT id,title,description,type,priority,color,status,due_at dueAt,remind_at remindAt,completed_at completedAt,pinned,sort_order sortOrder,repeat_rule repeatRule,created_at createdAt,updated_at updatedAt FROM todos WHERE user_id=@UserId AND tenant_id=@TenantId AND deleted_at IS NULL AND (@Status IS NULL OR status=@Status) AND (@Type IS NULL OR type=@Type) AND (@From IS NULL OR due_at>=@From) AND (@To IS NULL OR due_at<=@To) ORDER BY pinned DESC, due_at IS NULL, due_at, sort_order, id", new { user.UserId, user.TenantId, Status = status, Type = type, From = from, To = to });
        return Ok(ApiResponse<object>.Ok(items));
    }

    /// <summary>
    /// 创建一条待办事项，标题为必填项。
    /// </summary>
    [Authorize(Policy = PermissionNames.TodoWrite)]
    [HttpPost]
    public async Task<ActionResult<ApiResponse<object>>> Create(TodoWriteRequest request)
    {
        if (!TryGetUser(out var user)) return UnauthorizedResult<object>();
        if (string.IsNullOrWhiteSpace(request.Title)) return BadRequest(ApiResponse<object>.Fail(422, "title is required."));
        await using var db = _connections.Open();
        var id = await db.QuerySingleAsync<long>("INSERT INTO todos(tenant_id,user_id,list_id,parent_id,title,description,type,priority,color,status,due_at,remind_at,pinned,sort_order,repeat_rule) VALUES (@TenantId,@UserId,@ListId,@ParentId,@Title,@Description,@Type,@Priority,@Color,COALESCE(@Status,'pending'),@DueAt,@RemindAt,COALESCE(@Pinned,0),COALESCE(@SortOrder,0),@RepeatRule); SELECT LAST_INSERT_ID();", new { user.TenantId, user.UserId, request.ListId, request.ParentId, Title = request.Title.Trim(), request.Description, request.Type, request.Priority, request.Color, request.Status, request.DueAt, request.RemindAt, request.Pinned, request.SortOrder, request.RepeatRule });
        return Created($"/api/v1/todos/{id}", ApiResponse<object>.Ok(await GetTodo(db, user, id)));
    }

    /// <summary>
    /// 更新指定待办事项，状态变更会同步处理完成时间。
    /// </summary>
    [Authorize(Policy = PermissionNames.TodoWrite)]
    [HttpPut("{id:long}")]
    public async Task<ActionResult<ApiResponse<object>>> Update(long id, TodoWriteRequest request)
    {
        if (!TryGetUser(out var user)) return UnauthorizedResult<object>();
        await using var db = _connections.Open();
        var changed = await db.ExecuteAsync("UPDATE todos SET title=COALESCE(@Title,title),description=COALESCE(@Description,description),type=COALESCE(@Type,type),priority=COALESCE(@Priority,priority),color=COALESCE(@Color,color),status=COALESCE(@Status,status),due_at=COALESCE(@DueAt,due_at),remind_at=COALESCE(@RemindAt,remind_at),pinned=COALESCE(@Pinned,pinned),sort_order=COALESCE(@SortOrder,sort_order),repeat_rule=COALESCE(@RepeatRule,repeat_rule),completed_at=CASE WHEN @Status='completed' THEN UTC_TIMESTAMP(3) WHEN @Status='pending' THEN NULL ELSE completed_at END WHERE id=@Id AND user_id=@UserId AND tenant_id=@TenantId AND deleted_at IS NULL", new { Id = id, user.UserId, user.TenantId, request.Title, request.Description, request.Type, request.Priority, request.Color, request.Status, request.DueAt, request.RemindAt, request.Pinned, request.SortOrder, request.RepeatRule });
        return changed == 0 ? NotFoundResult<object>() : Ok(ApiResponse<object>.Ok(await GetTodo(db, user, id)));
    }

    /// <summary>
    /// 软删除指定待办事项。
    /// </summary>
    [Authorize(Policy = PermissionNames.TodoWrite)]
    [HttpDelete("{id:long}")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id)
    {
        if (!TryGetUser(out var user)) return UnauthorizedResult<object>();
        await using var db = _connections.Open();
        var changed = await db.ExecuteAsync("UPDATE todos SET deleted_at=UTC_TIMESTAMP(3) WHERE id=@Id AND user_id=@UserId AND tenant_id=@TenantId AND deleted_at IS NULL", new { Id = id, user.UserId, user.TenantId });
        return changed == 0 ? NotFoundResult<object>() : Ok(ApiResponse<object>.Ok(new { id }));
    }

    /// <summary>
    /// 为指定待办事项添加一条子任务。
    /// </summary>
    [Authorize(Policy = PermissionNames.TodoWrite)]
    [HttpPost("{id:long}/subtasks")]
    public async Task<ActionResult<ApiResponse<object>>> AddSubtask(long id, SubtaskWriteRequest request)
    {
        if (!TryGetUser(out var user)) return UnauthorizedResult<object>();
        if (string.IsNullOrWhiteSpace(request.Text)) return BadRequest(ApiResponse<object>.Fail(422, "text is required."));
        await using var db = _connections.Open();
        var exists = await db.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM todos WHERE id=@Id AND user_id=@UserId AND tenant_id=@TenantId AND deleted_at IS NULL", new { Id = id, user.UserId, user.TenantId });
        if (exists == 0) return NotFoundResult<object>();
        var subId = await db.QuerySingleAsync<long>("INSERT INTO subtasks(tenant_id,todo_id,text,done,seq) VALUES (@TenantId,@TodoId,@Text,0,COALESCE(@Seq,0)); SELECT LAST_INSERT_ID();", new { user.TenantId, TodoId = id, Text = request.Text.Trim(), request.Seq });
        var item = await db.QuerySingleAsync("SELECT id,text,done,seq FROM subtasks WHERE id=@SubId", new { SubId = subId });
        return Created($"/api/v1/todos/{id}/subtasks/{subId}", ApiResponse<object>.Ok(item));
    }

    /// <summary>
    /// 更新指定待办事项中的子任务内容、完成状态或排序。
    /// </summary>
    [Authorize(Policy = PermissionNames.TodoWrite)]
    [HttpPut("{id:long}/subtasks/{subId:long}")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateSubtask(long id, long subId, SubtaskWriteRequest request)
    {
        if (!TryGetUser(out var user)) return UnauthorizedResult<object>();
        await using var db = _connections.Open();
        var changed = await db.ExecuteAsync("UPDATE subtasks s JOIN todos t ON t.id=s.todo_id SET s.text=COALESCE(@Text,s.text),s.done=COALESCE(@Done,s.done),s.seq=COALESCE(@Seq,s.seq) WHERE s.id=@SubId AND s.todo_id=@TodoId AND s.tenant_id=@TenantId AND t.user_id=@UserId AND s.deleted_at IS NULL", new { SubId = subId, TodoId = id, user.UserId, user.TenantId, request.Text, request.Done, request.Seq });
        return changed == 0 ? NotFoundResult<object>() : Ok(ApiResponse<object>.Ok(await db.QuerySingleAsync("SELECT id,text,done,seq FROM subtasks WHERE id=@SubId", new { SubId = subId })));
    }

    /// <summary>
    /// 软删除指定待办事项中的子任务。
    /// </summary>
    [Authorize(Policy = PermissionNames.TodoWrite)]
    [HttpDelete("{id:long}/subtasks/{subId:long}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteSubtask(long id, long subId)
    {
        if (!TryGetUser(out var user)) return UnauthorizedResult<object>();
        await using var db = _connections.Open();
        var changed = await db.ExecuteAsync("UPDATE subtasks s JOIN todos t ON t.id=s.todo_id SET s.deleted_at=UTC_TIMESTAMP(3) WHERE s.id=@SubId AND s.todo_id=@TodoId AND s.tenant_id=@TenantId AND t.user_id=@UserId AND s.deleted_at IS NULL", new { SubId = subId, TodoId = id, user.UserId, user.TenantId });
        return changed == 0 ? NotFoundResult<object>() : Ok(ApiResponse<object>.Ok(new { id = subId }));
    }

    private static Task<dynamic> GetTodo(System.Data.IDbConnection db, UserContext user, long id) => db.QuerySingleAsync("SELECT id,title,description,type,priority,color,status,due_at dueAt,remind_at remindAt,completed_at completedAt,pinned,sort_order sortOrder,repeat_rule repeatRule,created_at createdAt,updated_at updatedAt FROM todos WHERE id=@Id AND user_id=@UserId AND tenant_id=@TenantId", new { Id = id, user.UserId, user.TenantId });
    public sealed record TodoWriteRequest(string? Title, string? Description, string? Type, string? Priority, string? Color, string? Status, DateTime? DueAt, DateTime? RemindAt, bool? Pinned, int? SortOrder, string? RepeatRule, long? ListId, long? ParentId);
    public sealed record SubtaskWriteRequest(string? Text, bool? Done, int? Seq);
}
