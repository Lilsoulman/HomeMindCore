using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.AI;

namespace HomeMind.Business.IServices.Expert;

/// <summary>版本化的多专家团队编排。客户端不得提交任意 Prompt 或工具调用；DAG 与成员 ExpertVersion 在创建时即被冻结。</summary>
public interface ITeamRunServices
{
    Task<ServiceResult> CreateAsync(long userId, long tenantId, TeamRunCreateRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult> GetAsync(long userId, long tenantId, long teamRunId, CancellationToken cancellationToken = default);
    Task<ServiceResult> ListEventsAsync(long userId, long tenantId, long teamRunId, CancellationToken cancellationToken = default);
    Task<ServiceResult> ListMembersAsync(long userId, long tenantId, long teamRunId, CancellationToken cancellationToken = default);
    Task<ServiceResult> GetSynthesisAsync(long userId, long tenantId, long teamRunId, CancellationToken cancellationToken = default);
    Task<ServiceResult> CancelAsync(long userId, long tenantId, long teamRunId, CancellationToken cancellationToken = default);
    Task<ServiceResult> RetryAsync(long userId, long tenantId, long teamRunId, CancellationToken cancellationToken = default);
}
