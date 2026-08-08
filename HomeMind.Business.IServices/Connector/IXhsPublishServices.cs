using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.Connectors;

namespace HomeMind.Business.IServices.Connector;

/// <summary>
/// 小红书（xhs）笔记发布服务契约：创建 L2 发布动作（ExpertRunAction，经确认中心逐项确认）与
/// 确认执行（幂等键 + ActionExecutionAudits 重放 + 权限快照复验，确认后经本地 MCP 发布并写
/// <c>xhs_note_published</c> 审计）。执行前校验连接器归属，未授权统一 404；响应不含凭据与 MCP 内部路径。
/// </summary>
public interface IXhsPublishServices
{
    /// <summary>创建小红书笔记发布动作（L2，等待确认），返回运行与动作视图。</summary>
    /// <param name="userId">当前用户主键。</param>
    /// <param name="tenantId">当前租户标识，来自 JWT。</param>
    /// <param name="request">发布请求（含可选幂等键）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>成功 201 返回动作视图；连接器未授权 404；参数非法 422；同键异类型 409。</returns>
    Task<ServiceResult> CreateAsync(long userId, long tenantId, XhsPublishRequest request, CancellationToken cancellationToken = default);

    /// <summary>确认并执行小红书发布动作：确认后经本地 MCP 发布笔记并写审计；同键重复确认重放首次结果。</summary>
    /// <param name="userId">当前用户主键。</param>
    /// <param name="tenantId">当前租户标识，来自 JWT。</param>
    /// <param name="actionId">发布动作主键。</param>
    /// <param name="request">确认请求（UUID 幂等键必填）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>成功 200 返回动作视图；非法幂等键 422；动作不存在或非本人 404；已终态换键 409；发布失败 502。</returns>
    Task<ServiceResult> ConfirmActionAsync(long userId, long tenantId, long actionId, XhsPublishConfirmRequest request, CancellationToken cancellationToken = default);
}
