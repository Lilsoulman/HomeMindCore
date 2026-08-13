using System;
using System.Threading;
using System.Threading.Tasks;
using HomeMind.Api.Controllers.Base;
using HomeMind.Api.Services;
using HomeMind.Business.IServices.AI;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.AI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HomeMind.Api.Controllers.AI;

/// <summary>AI 技能模块，控制器不直接访问数据库。</summary>
/// <remarks>技能是执行边界；内置技能不可删除或编辑。所有操作均作用于当前用户的租户内。</remarks>
[Authorize]
[Route("api/v1/skills")]
public sealed class SkillsController : ApiControllerBase
{
    private readonly IAiSkillServices _skillServices;

    /// <summary>构造技能控制器。</summary>
    /// <param name="skillServices">AI 技能业务服务。</param>
    public SkillsController(IAiSkillServices skillServices) => _skillServices = skillServices;

    /// <summary>列出当前租户内的用户 Skill，或供开发端查看的平台/聚合目录。</summary>
    /// <remarks>权限：<c>ai.skills.read</c>。scope=mine（默认）仅返回本人用户 Skill；platform/all 仅 owner/admin 可查询。</remarks>
    /// <param name="scope">视图范围：mine、platform 或 all；默认 mine。</param>
    /// <returns>技能列表的统一响应。</returns>
    [Authorize(Policy = PermissionNames.AiSkillsRead)]
    [HttpGet]
    public async Task<ActionResult<ApiResponse<object>>> List(string? scope = null) =>
        ToResponse(await WithUserAsync((user, token) => (scope ?? "mine").ToLowerInvariant() switch
        {
            "mine" => _skillServices.ListAsync(user.UserId, user.TenantId, token),
            "platform" => _skillServices.ListPlatformAsync(user.TenantId, user.Role, token),
            "all" => _skillServices.ListAllAsync(user.TenantId, user.Role, token),
            _ => Task.FromResult(new ServiceResult(422, "scope 仅支持 mine、platform 或 all。"))
        }));

    /// <summary>创建一个新技能，归属当前用户与租户。</summary>
    /// <remarks>权限：<c>ai.skills.write</c>。</remarks>
    /// <param name="request">技能创建请求体。</param>
    /// <returns>新建技能的统一响应。</returns>
    [Authorize(Policy = PermissionNames.AiSkillsWrite)]
    [HttpPost]
    public async Task<ActionResult<ApiResponse<object>>> Create(SkillRequest request) => ToResponse(await WithUserAsync((user, token) => _skillServices.CreateAsync(user.UserId, user.TenantId, request, token)));

    /// <summary>按主键更新技能；可空字段表示不修改。</summary>
    /// <remarks>权限：<c>ai.skills.write</c>。内置技能不可更新。</remarks>
    /// <param name="id">技能主键。</param>
    /// <param name="request">技能更新请求体。</param>
    /// <returns>更新结果的统一响应。</returns>
    [Authorize(Policy = PermissionNames.AiSkillsWrite)]
    [HttpPut("{id:long}")]
    public async Task<ActionResult<ApiResponse<object>>> Update(long id, SkillRequest request) => ToResponse(await WithUserAsync((user, token) => _skillServices.UpdateAsync(user.UserId, user.TenantId, id, request, token)));

    /// <summary>按主键软删除技能。</summary>
    /// <remarks>权限：<c>ai.skills.write</c>。内置技能不可删除。</remarks>
    /// <param name="id">技能主键。</param>
    /// <returns>删除结果的统一响应。</returns>
    [Authorize(Policy = PermissionNames.AiSkillsWrite)]
    [HttpDelete("{id:long}")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id) => ToResponse(await WithUserAsync((user, token) => _skillServices.DeleteAsync(user.UserId, user.TenantId, id, token)));

    /// <summary>在用户上下文就绪时执行给定的业务回调，否则返回 401。</summary>
    /// <param name="action">执行业务逻辑的回调。</param>
    /// <returns>业务执行结果 <see cref="ServiceResult"/>。</returns>
    private async Task<ServiceResult> WithUserAsync(Func<UserContext, CancellationToken, Task<ServiceResult>> action) => TryGetUser(out var user) ? await action(user, HttpContext.RequestAborted) : new ServiceResult(401, "未提供访问令牌，或访问令牌已过期。");

    /// <summary>将 <see cref="ServiceResult"/> 转换为统一 HTTP 响应。</summary>
    /// <param name="result">业务执行结果。</param>
    /// <returns>统一响应体与对应状态码。</returns>
    private ActionResult<ApiResponse<object>> ToResponse(ServiceResult result) => StatusCode(result.StatusCode, result.Succeeded ? new ApiResponse<object>(0, result.Message, result.Data) : ApiResponse<object>.Fail(result.Code, result.Message));
}
