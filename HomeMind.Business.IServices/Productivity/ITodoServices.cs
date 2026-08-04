using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.Productivity;

namespace HomeMind.Business.IServices.Productivity;

/// <summary>待办事项和子任务的业务服务约定。</summary>
public interface ITodoServices
{
    Task<ServiceResult> ListAsync(long userId, long tenantId, string? status, string? type, DateTime? from, DateTime? to, CancellationToken cancellationToken = default);
    Task<ServiceResult> CreateAsync(long userId, long tenantId, TodoWriteRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult> UpdateAsync(long userId, long tenantId, long id, TodoWriteRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult> DeleteAsync(long userId, long tenantId, long id, CancellationToken cancellationToken = default);
    Task<ServiceResult> AddSubtaskAsync(long userId, long tenantId, long todoId, SubtaskWriteRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult> UpdateSubtaskAsync(long userId, long tenantId, long todoId, long subtaskId, SubtaskWriteRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult> DeleteSubtaskAsync(long userId, long tenantId, long todoId, long subtaskId, CancellationToken cancellationToken = default);
}
