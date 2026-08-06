using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.Life;

namespace HomeMind.Business.IServices.Life;

/// <summary>
/// 个人生活专家运行服务抽象。翻牌（recommend）与行程（plan）为确定性编排，
/// 读取个人偏好收藏并复用既有 Run/确认/审计边界；不新建运行时。
/// </summary>
public interface ILifeExpertRunServices
{
    /// <summary>创建一个个人生活专家运行：翻牌返回 Top1-2 建议（只读 L1），行程生成待确认动作。</summary>
    /// <param name="userId">当前操作用户标识。</param>
    /// <param name="tenantId">租户主键，由 JWT 推导。</param>
    /// <param name="request">运行请求体。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>创建成功返回 201；专家未初始化返回 503；输入非法返回 422。</returns>
    Task<ServiceResult> CreateAsync(long userId, long tenantId, LifeExpertRunRequest request, CancellationToken cancellationToken = default);

    /// <summary>确认并执行一个待确认的行程动作（calendar_create_event），复用确认、幂等与审计链路。</summary>
    /// <param name="userId">当前操作用户标识。</param>
    /// <param name="tenantId">租户主键，由 JWT 推导。</param>
    /// <param name="runId">运行主键。</param>
    /// <param name="actionId">动作主键。</param>
    /// <param name="request">确认请求体，含 UUID 幂等键。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>执行成功返回 200；幂等键非法返回 422；动作不存在返回 404；已终态返回 409。</returns>
    Task<ServiceResult> ConfirmActionAsync(long userId, long tenantId, long runId, long actionId, ConfirmLifeExpertActionRequest request, CancellationToken cancellationToken = default);
}
