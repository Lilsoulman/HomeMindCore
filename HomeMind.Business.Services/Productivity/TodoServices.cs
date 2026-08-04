using HomeMind.Business.IServices.Productivity;
using HomeMind.Common.Model.Entities;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.Productivity;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;

namespace HomeMind.Business.Services.Productivity;

/// <summary>待办事项业务实现，集中处理所属用户和租户的隔离规则。</summary>
public sealed class TodoServices : ITodoServices
{
    private readonly HomeMindDbContext _db;
    public TodoServices(HomeMindDbContext db) => _db = db;

    public async Task<ServiceResult> ListAsync(long userId, long tenantId, string? status, string? type, DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
    {
        var query = _db.Todos.Where(x => x.UserId == userId && x.TenantId == tenantId && x.DeletedAt == null);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        if (!string.IsNullOrWhiteSpace(type)) query = query.Where(x => x.Type == type);
        if (from is not null) query = query.Where(x => x.DueAt >= from);
        if (to is not null) query = query.Where(x => x.DueAt <= to);
        var items = await query.OrderByDescending(x => x.Pinned).ThenBy(x => x.DueAt == null).ThenBy(x => x.DueAt).ThenBy(x => x.SortOrder).ThenBy(x => x.Id).ToListAsync(cancellationToken);
        return new ServiceResult(200, "查询成功。", items.Select(ToView));
    }

    public async Task<ServiceResult> CreateAsync(long userId, long tenantId, TodoWriteRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Title)) return new ServiceResult(422, "请填写待办标题。");
        var item = new Todo { TenantId = tenantId, UserId = userId, ListId = request.ListId, ParentId = request.ParentId, Title = request.Title.Trim(), Description = request.Description, Type = request.Type, Priority = request.Priority, Color = request.Color, Status = request.Status ?? "pending", DueAt = request.DueAt, RemindAt = request.RemindAt, Pinned = request.Pinned ?? false, SortOrder = request.SortOrder ?? 0, RepeatRule = request.RepeatRule };
        _db.Todos.Add(item);
        await _db.SaveChangesAsync(cancellationToken);
        return new ServiceResult(201, "创建成功。", ToView(item));
    }

    public async Task<ServiceResult> UpdateAsync(long userId, long tenantId, long id, TodoWriteRequest request, CancellationToken cancellationToken = default)
    {
        var item = await FindTodoAsync(userId, tenantId, id, cancellationToken);
        if (item is null) return new ServiceResult(404, "请求的资源不存在。");
        item.Title = request.Title ?? item.Title; item.Description = request.Description ?? item.Description; item.Type = request.Type ?? item.Type; item.Priority = request.Priority ?? item.Priority; item.Color = request.Color ?? item.Color; item.DueAt = request.DueAt ?? item.DueAt; item.RemindAt = request.RemindAt ?? item.RemindAt; item.Pinned = request.Pinned ?? item.Pinned; item.SortOrder = request.SortOrder ?? item.SortOrder; item.RepeatRule = request.RepeatRule ?? item.RepeatRule;
        if (request.Status is not null) { item.Status = request.Status; item.CompletedAt = request.Status == "completed" ? DateTime.UtcNow : request.Status == "pending" ? null : item.CompletedAt; }
        await _db.SaveChangesAsync(cancellationToken);
        return new ServiceResult(200, "更新成功。", ToView(item));
    }

    public async Task<ServiceResult> DeleteAsync(long userId, long tenantId, long id, CancellationToken cancellationToken = default)
    {
        var item = await FindTodoAsync(userId, tenantId, id, cancellationToken);
        if (item is null) return new ServiceResult(404, "请求的资源不存在。");
        item.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return new ServiceResult(200, "删除成功。", new { id });
    }

    public async Task<ServiceResult> AddSubtaskAsync(long userId, long tenantId, long todoId, SubtaskWriteRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Text)) return new ServiceResult(422, "请填写子任务内容。");
        if (await FindTodoAsync(userId, tenantId, todoId, cancellationToken) is null) return new ServiceResult(404, "请求的资源不存在。");
        var item = new Subtask { TenantId = tenantId, TodoId = todoId, Text = request.Text.Trim(), Done = false, Seq = request.Seq ?? 0 };
        _db.Subtasks.Add(item);
        await _db.SaveChangesAsync(cancellationToken);
        return new ServiceResult(201, "创建成功。", ToView(item));
    }

    public async Task<ServiceResult> UpdateSubtaskAsync(long userId, long tenantId, long todoId, long subtaskId, SubtaskWriteRequest request, CancellationToken cancellationToken = default)
    {
        var item = await FindSubtaskAsync(userId, tenantId, todoId, subtaskId, cancellationToken);
        if (item is null) return new ServiceResult(404, "请求的资源不存在。");
        item.Text = request.Text ?? item.Text; item.Done = request.Done ?? item.Done; item.Seq = request.Seq ?? item.Seq;
        await _db.SaveChangesAsync(cancellationToken);
        return new ServiceResult(200, "更新成功。", ToView(item));
    }

    public async Task<ServiceResult> DeleteSubtaskAsync(long userId, long tenantId, long todoId, long subtaskId, CancellationToken cancellationToken = default)
    {
        var item = await FindSubtaskAsync(userId, tenantId, todoId, subtaskId, cancellationToken);
        if (item is null) return new ServiceResult(404, "请求的资源不存在。");
        item.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return new ServiceResult(200, "删除成功。", new { id = subtaskId });
    }

    private Task<Todo?> FindTodoAsync(long userId, long tenantId, long id, CancellationToken cancellationToken) => _db.Todos.SingleOrDefaultAsync(x => x.Id == id && x.UserId == userId && x.TenantId == tenantId && x.DeletedAt == null, cancellationToken);
    private Task<Subtask?> FindSubtaskAsync(long userId, long tenantId, long todoId, long subtaskId, CancellationToken cancellationToken) => (from subtask in _db.Subtasks join todo in _db.Todos on subtask.TodoId equals todo.Id where subtask.Id == subtaskId && subtask.TodoId == todoId && subtask.TenantId == tenantId && todo.UserId == userId && subtask.DeletedAt == null select subtask).SingleOrDefaultAsync(cancellationToken);
    private static object ToView(Todo x) => new { x.Id, x.Title, x.Description, x.Type, x.Priority, x.Color, x.Status, x.DueAt, x.RemindAt, x.CompletedAt, x.Pinned, x.SortOrder, x.RepeatRule, x.CreatedAt, x.UpdatedAt };
    private static object ToView(Subtask x) => new { x.Id, x.Text, x.Done, x.Seq };
}
