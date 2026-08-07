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
/// 家庭成员受控管理控制器（V2.4 B19）：当前家庭成员列表、角色变更、状态停启、owner 转让。
/// 所有路由 <c>{homeId}</c> 必须等于 JWT 推导的租户主键；只 owner/admin 可写。
/// </summary>
[Authorize]
[Route("api/v1/homes/{homeId:long}")]
public sealed class TenantMembersController : ApiControllerBase
{
    private readonly ITenantMemberServices _members;

    /// <summary>构造家庭成员受控管理控制器。</summary>
    /// <param name="members">家庭成员受控管理服务。</param>
    public TenantMembersController(ITenantMemberServices members) => _members = members;

    /// <summary>列出当前家庭所有成员（含用户资料、角色、状态与行版本）。</summary>
    /// <remarks>权限：<c>tenant.read</c>。租户由 JWT 推导，homeId 必须与之相等。</remarks>
    /// <param name="homeId">家庭主键，必须等于当前 JWT tenant_id。</param>
    /// <returns>成员摘要列表统一响应。</returns>
    [RequireHomeOwner]
    [Authorize(Policy = PermissionNames.TenantRead)]
    [HttpGet("members")]
    public async Task<ActionResult<ApiResponse<object>>> ListMembers(long homeId) =>
        ToResponse(await WithUserAsync((user, token) => _members.ListMembersAsync(user.TenantId, user.UserId, token)));

    /// <summary>变更目标成员角色；新角色不得为 owner。</summary>
    /// <remarks>权限：<c>tenant.member.manage</c>（owner/admin）。乐观锁冲突返回 409。</remarks>
    /// <param name="homeId">家庭主键，必须等于当前 JWT tenant_id。</param>
    /// <param name="memberUserId">目标成员用户主键。</param>
    /// <param name="request">角色变更请求体。</param>
    /// <returns>更新后成员视图统一响应。</returns>
    [RequireHomeOwner]
    [Authorize(Policy = PermissionNames.TenantMemberManage)]
    [HttpPut("members/{memberUserId:long}/role")]
    public async Task<ActionResult<ApiResponse<object>>> ChangeRole(long homeId, long memberUserId, TenantMemberRoleUpdateRequest request) =>
        ToResponse(await WithUserAsync((user, token) => _members.ChangeRoleAsync(user.TenantId, user.UserId, memberUserId, request, token)));

    /// <summary>变更目标成员启用/停用状态；不能停用最后一名 active owner。</summary>
    /// <remarks>权限：<c>tenant.member.manage</c>（owner/admin）。停用时 reason 必填。</remarks>
    /// <param name="homeId">家庭主键，必须等于当前 JWT tenant_id。</param>
    /// <param name="memberUserId">目标成员用户主键。</param>
    /// <param name="request">状态变更请求体。</param>
    /// <returns>更新后成员视图统一响应。</returns>
    [RequireHomeOwner]
    [Authorize(Policy = PermissionNames.TenantMemberManage)]
    [HttpPut("members/{memberUserId:long}/status")]
    public async Task<ActionResult<ApiResponse<object>>> ChangeStatus(long homeId, long memberUserId, TenantMemberStatusUpdateRequest request) =>
        ToResponse(await WithUserAsync((user, token) => _members.ChangeStatusAsync(user.TenantId, user.UserId, memberUserId, request, token)));

    /// <summary>owner 转让：同事务更新 <c>tenants.owner_user_id</c> 与双方成员角色。</summary>
    /// <remarks>权限：<c>tenant.member.manage</c>，且发起人必须为当前 active owner。</remarks>
    /// <param name="homeId">家庭主键，必须等于当前 JWT tenant_id。</param>
    /// <param name="request">转让请求体。</param>
    /// <returns>新 owner 成员视图统一响应。</returns>
    [RequireHomeOwner]
    [Authorize(Policy = PermissionNames.TenantMemberManage)]
    [HttpPost("owner-transfer")]
    public async Task<ActionResult<ApiResponse<object>>> TransferOwner(long homeId, TenantOwnerTransferRequest request) =>
        ToResponse(await WithUserAsync((user, token) => _members.TransferOwnerAsync(user.TenantId, user.UserId, request, token)));

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
