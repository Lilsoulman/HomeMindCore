using System;
using System.Threading;
using System.Threading.Tasks;
using HomeMind.Api.Controllers.Base;
using HomeMind.Api.Services;
using HomeMind.Business.IServices.Identity;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HomeMind.Api.Controllers.Identity;

/// <summary>
/// 家庭成员邀请接受控制器（V2.4 B19）：受邀人接受邀请并加入家庭。
/// 家庭（租户）由邀请记录推导，不接受客户端传入 homeId，不套 <c>RequireHomeOwner</c>。
/// </summary>
[Authorize]
[Route("api/v1/invitations")]
public sealed class TenantMemberInvitationAcceptController : ApiControllerBase
{
    private readonly ITenantMemberInvitationServices _invitations;

    /// <summary>构造家庭成员邀请接受控制器。</summary>
    /// <param name="invitations">家庭成员邀请服务。</param>
    public TenantMemberInvitationAcceptController(ITenantMemberInvitationServices invitations) => _invitations = invitations;

    /// <summary>当前用户接受邀请并加入家庭。</summary>
    /// <remarks>
    /// 权限：<c>tenant.read</c>。服务端重新计算手机号 SHA-256 并匹配当前账户已验证的
    /// <c>user_identities</c>；邀请不存在 / 哈希不匹配 / 未验证统一返回 404。
    /// </remarks>
    /// <param name="request">接受请求体，含邀请主键与手机号原文。</param>
    /// <returns>接受结果统一响应。</returns>
    [Authorize(Policy = PermissionNames.TenantRead)]
    [HttpPost("accept")]
    public async Task<ActionResult<ApiResponse<object>>> Accept(TenantMemberInvitationAcceptRequest request) =>
        ToResponse(await WithUserAsync((user, token) => _invitations.AcceptAsync(user.UserId, request, token)));

    /// <summary>在用户上下文就绪时执行给定的业务回调，否则返回 401。</summary>
    /// <param name="action">执行业务逻辑的回调。</param>
    /// <returns>业务执行结果 <see cref="ServiceResult"/>。</returns>
    private async Task<ServiceResult> WithUserAsync(Func<UserContext, CancellationToken, Task<ServiceResult>> action) => TryGetUser(out var user)
        ? await action(user, HttpContext.RequestAborted)
        : new ServiceResult(401, "未提供访问令牌，或访问令牌已过期。");

    /// <summary>将 <see cref="ServiceResult"/> 转换为统一 HTTP 响应。</summary>
    /// <param name="result">业务执行结果。</param>
    /// <returns>统一响应体与对应状态码。</returns>
    private static ActionResult<ApiResponse<object>> ToResponse(ServiceResult result) => result.Succeeded
        ? new ObjectResult(new ApiResponse<object>(0, result.Message, result.Data)) { StatusCode = result.StatusCode }
        : new ObjectResult(ApiResponse<object>.Fail(result.Code, result.Message)) { StatusCode = result.StatusCode };
}
