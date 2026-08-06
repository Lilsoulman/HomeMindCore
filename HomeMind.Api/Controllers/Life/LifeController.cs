using System;
using System.Threading;
using System.Threading.Tasks;
using HomeMind.Api.Controllers.Base;
using HomeMind.Api.Services;
using HomeMind.Business.IServices.Life;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.Life;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HomeMind.Api.Controllers.Life;

/// <summary>
/// 个人生活 API 控制器，负责个人偏好收藏的 CRUD 与对话导入入口。
/// 家庭归属一律由 JWT 推导，客户端不得指定或覆盖 homeId。
/// </summary>
/// <remarks>
/// 权限策略（B14 已预注册）：
/// - 只读接口（List/Get）使用 <c>life.favorite.read</c>。
/// - 写入接口（Create/Update/Delete/Import）使用 <c>life.favorite.write</c>。
/// private 收藏仅归属成员本人可见；跨家庭与越权访问一律返回 404。
/// </remarks>
[Authorize]
[Route("api/v1/life")]
public sealed class LifeController : ApiControllerBase
{
    private readonly IFavoriteService _favorites;

    /// <summary>构造个人生活控制器。</summary>
    /// <param name="favorites">个人偏好收藏服务。</param>
    public LifeController(IFavoriteService favorites) => _favorites = favorites;

    /// <summary>按分类与可见性列出当前成员可见的收藏。</summary>
    /// <remarks>权限：<c>life.favorite.read</c>。private 项仅归属成员本人可见；过滤参数非法返回 422。</remarks>
    /// <param name="category">可选分类过滤：restaurant / travel / material。</param>
    /// <param name="visibility">可选可见性过滤：private / family。</param>
    /// <returns>收藏列表统一响应。</returns>
    [Authorize(Policy = PermissionNames.LifeFavoriteRead)]
    [HttpGet("favorites")]
    public async Task<ActionResult<ApiResponse<object>>> ListFavorites(string? category = null, string? visibility = null) =>
        ToResponse(await WithUserAsync((user, token) => _favorites.ListAsync(user.TenantId, user.UserId, category, visibility, token)));

    /// <summary>获取单条收藏详情。</summary>
    /// <remarks>权限：<c>life.favorite.read</c>。不可见或不存在统一返回 404，避免泄露归属信息。</remarks>
    /// <param name="favoriteId">目标收藏主键。</param>
    /// <returns>收藏详情统一响应；不存在或不可见返回 404。</returns>
    [Authorize(Policy = PermissionNames.LifeFavoriteRead)]
    [HttpGet("favorites/{favoriteId:long}")]
    public async Task<ActionResult<ApiResponse<object>>> GetFavorite(long favoriteId) =>
        ToResponse(await WithUserAsync((user, token) => _favorites.GetAsync(user.TenantId, user.UserId, favoriteId, token)));

    /// <summary>创建一条收藏；归属成员默认解析为当前成员，可显式指定同家庭成员。</summary>
    /// <remarks>权限：<c>life.favorite.write</c>。写操作记录 favorite_create 审计。</remarks>
    /// <param name="request">创建请求体。</param>
    /// <returns>创建成功返回 201；校验失败返回 422。</returns>
    [Authorize(Policy = PermissionNames.LifeFavoriteWrite)]
    [HttpPost("favorites")]
    public async Task<ActionResult<ApiResponse<object>>> CreateFavorite(FavoriteCreateRequest request) =>
        ToResponse(await WithUserAsync((user, token) => _favorites.CreateAsync(user.TenantId, user.UserId, request, token)));

    /// <summary>更新一条收藏；仅归属成员本人或家庭管理员可操作。</summary>
    /// <remarks>权限：<c>life.favorite.write</c>。写操作记录 favorite_update 审计。</remarks>
    /// <param name="favoriteId">目标收藏主键。</param>
    /// <param name="request">更新请求体。</param>
    /// <returns>更新成功返回 200；不存在返回 404；无权限返回 403。</returns>
    [Authorize(Policy = PermissionNames.LifeFavoriteWrite)]
    [HttpPut("favorites/{favoriteId:long}")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateFavorite(long favoriteId, FavoriteUpdateRequest request) =>
        ToResponse(await WithUserAsync((user, token) => _favorites.UpdateAsync(user.TenantId, user.UserId, favoriteId, request, token)));

    /// <summary>软删除一条收藏并写审计。</summary>
    /// <remarks>权限：<c>life.favorite.write</c>。仅归属成员本人或家庭管理员可操作。</remarks>
    /// <param name="favoriteId">目标收藏主键。</param>
    /// <returns>删除成功返回 200；不存在返回 404；无权限返回 403。</returns>
    [Authorize(Policy = PermissionNames.LifeFavoriteWrite)]
    [HttpDelete("favorites/{favoriteId:long}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteFavorite(long favoriteId) =>
        ToResponse(await WithUserAsync((user, token) => _favorites.DeleteAsync(user.TenantId, user.UserId, favoriteId, token)));

    /// <summary>从对话导入收藏；记录来源并写审计。</summary>
    /// <remarks>权限：<c>life.favorite.write</c>。AI 对话提取部分依赖 AI 运行时，按部署环境验证。</remarks>
    /// <param name="request">导入请求体。</param>
    /// <returns>导入成功返回 201；校验失败返回 422。</returns>
    [Authorize(Policy = PermissionNames.LifeFavoriteWrite)]
    [HttpPost("favorites/import")]
    public async Task<ActionResult<ApiResponse<object>>> ImportFavorite(FavoriteImportRequest request) =>
        ToResponse(await WithUserAsync((user, token) => _favorites.ImportAsync(user.TenantId, user.UserId, request, token)));

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
