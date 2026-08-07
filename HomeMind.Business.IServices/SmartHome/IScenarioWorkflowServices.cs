using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.SmartHome;

namespace HomeMind.Business.IServices.SmartHome;

/// <summary>
/// 场景工作流服务契约：平台模板 → 家庭实例 → Run 执行。
/// 执行、确认、幂等与审计全部复用 AgentRun / ExpertRunAction / ActionExecutionAudits 链路，
/// 不新增执行引擎与步骤表；步骤上下文由运行动作的 RequestJson 承载。
/// </summary>
public interface IScenarioWorkflowServices
{
    /// <summary>列出平台级场景模板（租户 1、active、未删除）。</summary>
    /// <param name="tenantId">当前租户标识，由 JWT 推导。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>模板列表统一响应。</returns>
    Task<ServiceResult> ListTemplatesAsync(long tenantId, CancellationToken cancellationToken = default);

    /// <summary>列出当前家庭的场景实例（未删除）。</summary>
    /// <param name="tenantId">当前租户标识，由 JWT 推导。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>实例列表统一响应。</returns>
    Task<ServiceResult> ListInstancesAsync(long tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 启用一个场景模板：按 device_type + room + capability 解析家庭设备生成实例；
    /// 无匹配设备的能力步骤标记 unavailable，启用仍成功（Enable-time tolerant）。
    /// </summary>
    /// <param name="userId">启用用户标识，由 JWT 推导。</param>
    /// <param name="tenantId">当前租户标识，由 JWT 推导。</param>
    /// <param name="templateCode">模板业务键。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>实例视图统一响应；模板不存在或已停用返回 404。</returns>
    Task<ServiceResult> EnableAsync(long userId, long tenantId, string templateCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// 运行一个已启用的场景实例：创建 AgentRun（SourceType=scenario）与单个
    /// ExpertRunAction（ActionType=scenario，步骤上下文承载于 RequestJson），等待确认。
    /// </summary>
    /// <param name="userId">运行用户标识，由 JWT 推导。</param>
    /// <param name="tenantId">当前租户标识，由 JWT 推导。</param>
    /// <param name="instanceId">场景实例主键。</param>
    /// <param name="request">运行请求体，可选幂等键。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>运行视图统一响应；实例不存在返回 404。</returns>
    Task<ServiceResult> RunAsync(long userId, long tenantId, long instanceId, ScenarioRunRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 确认并执行一个场景运行动作：逐步下发设备命令，required 失败后继续后续步骤；
    /// 结果按定稿状态规则汇总为 success / partial / failed 并写入运行结果。
    /// </summary>
    /// <param name="userId">确认用户标识，由 JWT 推导。</param>
    /// <param name="tenantId">当前租户标识，由 JWT 推导。</param>
    /// <param name="runId">运行主键。</param>
    /// <param name="actionId">动作主键。</param>
    /// <param name="request">确认请求体，含 UUID 幂等键。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>执行结果统一响应；非法幂等键 422；动作不存在 404；已终态 409。</returns>
    Task<ServiceResult> ConfirmActionAsync(long userId, long tenantId, long runId, long actionId, ConfirmScenarioActionRequest request, CancellationToken cancellationToken = default);
}
