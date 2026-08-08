using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.AI;

namespace HomeMind.Business.IServices.AI;

/// <summary>
/// Skill 独立执行服务契约（SkillExecutor 首个实现）：按 skillCode 解析平台级 Skill 目录，
/// 确定性生成执行方案并创建 SourceType=skill 的 AgentRun（不绑定 Expert，同场景工作流先例）；
/// 执行、确认、幂等与审计全部复用 AgentRun / ExpertRunAction / ActionExecutionAudits 链路，
/// 不新建运行时。
/// </summary>
public interface ISkillRunServices
{
    /// <summary>
    /// 创建 Skill 运行：解析平台级 Skill（key=skillCode、active、未删除），校验输入参数，
    /// 确定性生成剪辑方案并产出单个 draft_generate Run Action（RiskLevel=L1），等待用户确认。
    /// </summary>
    /// <param name="userId">发起用户标识，由 JWT 推导。</param>
    /// <param name="tenantId">当前租户标识，由 JWT 推导。</param>
    /// <param name="skillCode">Skill 业务键，路由段。</param>
    /// <param name="request">运行请求体，含 UUID 幂等键与 Skill 输入参数。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>运行视图统一响应；未知/未启用 Skill 返回 422，输入非法返回 422。</returns>
    Task<ServiceResult> CreateAsync(long userId, long tenantId, string skillCode, SkillRunCreateRequest request, CancellationToken cancellationToken = default);

    /// <summary>按运行主键查询本人 Skill 运行视图；跨租户、跨用户或不存在返回 404。</summary>
    /// <param name="userId">查询用户标识，由 JWT 推导。</param>
    /// <param name="tenantId">当前租户标识，由 JWT 推导。</param>
    /// <param name="runId">运行主键。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>运行视图统一响应；不可见返回 404。</returns>
    Task<ServiceResult> GetAsync(long userId, long tenantId, long runId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 确认并执行 Skill 运行动作（draft_generate）：经剪辑 MCP 客户端生成 .draft 草稿内容，
    /// 复用 <c>RegisterGeneratedFileAsync</c> 登记为生成文件并写审计；同幂等键重放首次结果，
    /// 不重复登记。
    /// </summary>
    /// <param name="userId">确认用户标识，由 JWT 推导。</param>
    /// <param name="tenantId">当前租户标识，由 JWT 推导。</param>
    /// <param name="runId">运行主键。</param>
    /// <param name="actionId">动作主键。</param>
    /// <param name="request">确认请求体，含 UUID 幂等键。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>执行结果统一响应；非法幂等键返回 422，动作不存在返回 404，已终态返回 409。</returns>
    Task<ServiceResult> ConfirmActionAsync(long userId, long tenantId, long runId, long actionId, ConfirmSkillRunActionRequest request, CancellationToken cancellationToken = default);
}
