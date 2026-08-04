using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.AI;

namespace HomeMind.Business.IServices.Agent;

/// <summary>AI Agent 的运行生命周期服务；所有 AI 运行均由该服务创建和管理。</summary>
public interface IAgentRunServices
{
    Task<ServiceResult> CreateAsync(long userId, long tenantId, AgentRunCreateRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult> GetAsync(long userId, long tenantId, long runId, CancellationToken cancellationToken = default);
    Task<ServiceResult> ListEventsAsync(long userId, long tenantId, long runId, CancellationToken cancellationToken = default);
    Task<ServiceResult> CancelAsync(long userId, long tenantId, long runId, CancellationToken cancellationToken = default);
    Task<ServiceResult> RetryAsync(long userId, long tenantId, long runId, CancellationToken cancellationToken = default);
    Task<ServiceResult> CreateActionAsync(long userId, long tenantId, long runId, AgentRunActionRequest request, CancellationToken cancellationToken = default);
}
