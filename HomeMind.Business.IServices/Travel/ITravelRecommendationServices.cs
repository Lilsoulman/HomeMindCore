using HomeMind.Common.Model.ViewModel.Common;

namespace HomeMind.Business.IServices.Travel;

/// <summary>周末出行推荐服务：确定性偏好过滤 + 轮换 + 反馈闭环，偏好存于 family_knowledge（travel 档）。</summary>
public interface ITravelRecommendationServices
{
    /// <summary>按偏好返回 Top3 推荐，并累加已推荐计数实现轮换。</summary>
    Task<ServiceResult> GetRecommendationsAsync(long userId, long tenantId, CancellationToken cancellationToken = default);

    /// <summary>提交三选一反馈：selected/alternative/not_interested，更新偏好与排除集。</summary>
    Task<ServiceResult> SubmitFeedbackAsync(long userId, long tenantId, long attractionId, string choice, CancellationToken cancellationToken = default);
}
