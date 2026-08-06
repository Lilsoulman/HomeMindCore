using System;
using System.Threading;
using System.Threading.Tasks;
using HomeMind.Api.Controllers.Base;
using HomeMind.Api.Services;
using HomeMind.Business.IServices.Travel;
using HomeMind.Common.Model.ViewModel.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HomeMind.Api.Controllers.Travel;

/// <summary>周末出行推荐：获取推荐与三选一反馈。偏好存于家庭知识库，反馈闭环更新偏好。</summary>
[Authorize]
[Route("api/v1")]
public sealed class TravelController : ApiControllerBase
{
    private readonly ITravelRecommendationServices _travel;

    /// <summary>构造出行推荐控制器。</summary>
    /// <param name="travel">出行推荐业务服务。</param>
    public TravelController(ITravelRecommendationServices travel) => _travel = travel;

    /// <summary>获取本周出行推荐 Top3。</summary>
    /// <remarks>权限：<c>ai.read</c>。按家庭偏好过滤并轮换，每次调用累加已推荐计数。</remarks>
    /// <returns>推荐列表的统一响应。</returns>
    [Authorize(Policy = PermissionNames.AiRead)]
    [HttpGet("travel/recommendations")]
    public async Task<ActionResult<ApiResponse<object>>> GetRecommendations() =>
        ToResponse(await WithUserAsync((user, token) => _travel.GetRecommendationsAsync(user.UserId, user.TenantId, token)));

    /// <summary>提交推荐反馈：选这个 / 换一个 / 不感兴趣。</summary>
    /// <remarks>权限：<c>ai.read</c>。不感兴趣会将景点加入排除集。</remarks>
    /// <param name="attractionId">景点主键。</param>
    /// <param name="choice">反馈取值：selected / alternative / not_interested。</param>
    /// <returns>反馈结果的统一响应。</returns>
    [Authorize(Policy = PermissionNames.AiRead)]
    [HttpPost("travel/recommendations/{attractionId:long}/feedback")]
    public async Task<ActionResult<ApiResponse<object>>> SubmitFeedback(long attractionId, [FromBody] TravelFeedbackRequest request) =>
        ToResponse(await WithUserAsync((user, token) => _travel.SubmitFeedbackAsync(user.UserId, user.TenantId, attractionId, request.Choice ?? "", token)));

    /// <summary>出行反馈请求体。</summary>
    public sealed class TravelFeedbackRequest
    {
        /// <summary>反馈取值：selected / alternative / not_interested。</summary>
        public string? Choice { get; set; }
    }

    /// <summary>在用户上下文就绪时执行给定的业务回调，否则返回 401。</summary>
    /// <param name="action">执行业务逻辑的回调。</param>
    /// <returns>业务执行结果 <see cref="ServiceResult"/>。</returns>
    private async Task<ServiceResult> WithUserAsync(Func<UserContext, CancellationToken, Task<ServiceResult>> action) =>
        TryGetUser(out var user)
            ? await action(user, HttpContext.RequestAborted)
            : new ServiceResult(401, "未提供访问令牌，或访问令牌已过期。");

    /// <summary>将 <see cref="ServiceResult"/> 转换为统一 HTTP 响应。</summary>
    /// <param name="result">业务执行结果。</param>
    /// <returns>统一响应体与对应状态码。</returns>
    private static ActionResult<ApiResponse<object>> ToResponse(ServiceResult result) =>
        new ObjectResult(result.Succeeded
            ? new ApiResponse<object>(0, result.Message, result.Data)
            : ApiResponse<object>.Fail(result.Code, result.Message))
        { StatusCode = result.StatusCode };
}
