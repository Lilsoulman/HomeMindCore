using System;
using System.Threading;
using System.Threading.Tasks;
using HomeMind.Api.Authorization;
using HomeMind.Api.Controllers.Base;
using HomeMind.Api.Services;
using HomeMind.Business.IServices.Identity;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HomeMind.Api.Controllers.Identity;

/// <summary>
/// 家庭成员邀请控制器（V2.4 B19）：创建/列表/撤销邀请与受邀人接受。
/// 管理路由 <c>{homeId}</c> 必须等于 JWT 租户主键；接受路由以当前用户为主体，不依赖 homeId。
/// </summary>
[Authorize]
[Route("api/v1/homes/{homeId:long}/invitations")]
public sealed class TenantMemberInvitationsController : ApiControllerBase
{
    private readonly ITenantMemberInvitationServices _invitations;

    /// <summary>构造家庭成员邀请控制器。</summary>
    /// <param name="invitations">家庭成员邀请服务。</param>
    public TenantMemberInvitationsController(ITenantMemberInvitationServices invitations) => _invitations = invitations;

    /// <summary>创建一条家庭成员邀请（按手机号 SHA-256 匹配已验证账户）。</summary>
    /// <remarks>权限：<c>tenant.member.manage</c>（owner/admin）。同手机号当前家庭已存在 pending 邀请返回 409。</remarks>
    /// <param name="homeId">家庭主键，必须等于当前 JWT tenant_id。</param>
    /// <param name="request">邀请创建请求体。</param>
    /// <returns>新邀请视图统一响应。</returns>
    [RequireHomeOwner]
    [Authorize(Policy = PermissionNames.TenantMemberManage)]
    [HttpPost]
    public async Task<ActionResult<ApiResponse<object>>> Create(long homeId, TenantMemberInvitationCreateRequest request) =>
        ToResponse(await WithUserAsync((user, token) => _invitations.CreateAsync(user.TenantId, user.UserId, request, token)));

    /// <summary>列出当前家庭邀请，可按状态过滤。</summary>
    /// <remarks>权限：<c>tenant.read</c>。状态过滤 pending/accepted/expired/revoked；过期项按计算语义不写回填。</remarks>
    /// <param name="homeId">家庭主键，必须等于当前 JWT tenant_id。</param>
    /// <param name="status">可选状态过滤。</param>
    /// <returns>邀请列表分页视图统一响应。</returns>
    [RequireHomeOwner]
    [Authorize(Policy = PermissionNames.TenantRead)]
    [HttpGet]
    public async Task<ActionResult<ApiResponse<object>>> List(long homeId, [FromQuery] string status) =>
        ToResponse(await WithUserAsync((user, token) => _invitations.ListAsync(user.TenantId, user.UserId, status, token)));

    /// <summary>撤销一条 pending 邀请。</summary>
    /// <remarks>权限：<c>tenant.member.manage</c>（owner/admin）。已终态邀请返回 409。</remarks>
    /// <param name="homeId">家庭主键，必须等于当前 JWT tenant_id。</param>
    /// <param name="invitationId">邀请主键。</param>
    /// <returns>撤销后邀请视图统一响应。</returns>
    [RequireHomeOwner]
    [Authorize(Policy = PermissionNames.TenantMemberManage)]
    [HttpDelete("{invitationId:long}")]
    public async Task<ActionResult<ApiResponse<object>>> Revoke(long homeId, long invitationId) =>
        ToResponse(await WithUserAsync((user, token) => _invitations.RevokeAsync(user.TenantId, user.UserId, invitationId, token)));

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
