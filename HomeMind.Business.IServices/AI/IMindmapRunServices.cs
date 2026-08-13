using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.AI;

namespace HomeMind.Business.IServices.AI;

/// <summary>思维导图 Skill 运行服务：只记录 markdown 输入与展示安全摘要，不在服务端执行导图转换。</summary>
public interface IMindmapRunServices
{
    /// <summary>创建同步完成的 mindmap Skill 运行。</summary>
    /// <param name="userId">发起用户标识，由 JWT 推导。</param>
    /// <param name="tenantId">当前租户标识，由 JWT 推导。</param>
    /// <param name="request">请求体，包含 markdown 与可选幂等键。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>运行摘要；输入非法返回 422，幂等键冲突返回 409。</returns>
    Task<ServiceResult> CreateAsync(long userId, long tenantId, MindmapRunCreateRequest request, CancellationToken cancellationToken = default);

    /// <summary>查询当前用户可见的 mindmap Skill 运行。</summary>
    /// <param name="userId">查询用户标识，由 JWT 推导。</param>
    /// <param name="tenantId">当前租户标识，由 JWT 推导。</param>
    /// <param name="runId">运行主键。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>展示安全摘要；不可见时返回 404。</returns>
    Task<ServiceResult> GetAsync(long userId, long tenantId, long runId, CancellationToken cancellationToken = default);
}
