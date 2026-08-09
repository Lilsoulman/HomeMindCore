using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using HomeMind.Api.Controllers.Base;
using HomeMind.Api.Services;
using HomeMind.Business.IServices.Media;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.Media;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace HomeMind.Api.Controllers.Media;

/// <summary>快速剪辑素材登记入口（B29）：浏览器上传或路径模式登记素材，ffprobe 提取元数据；素材仅本人可见可删。</summary>
/// <remarks>上传返回 <c>storagePath</c> 供 Web 端回填 Skill 输入 <c>media_location</c>（B24 契约零改动）；响应不包含目录遍历信息。</remarks>
[Authorize]
[Route("api/v1/clipping/materials")]
public sealed class ClippingMaterialsController : ApiControllerBase
{
    private readonly IClippingMaterialServices _materials;

    /// <summary>构造素材登记控制器。</summary>
    /// <param name="materials">素材登记服务。</param>
    public ClippingMaterialsController(IClippingMaterialServices materials) => _materials = materials;

    /// <summary>登记素材：multipart 上传（file 字段）或路径模式（filePath 字段）二选一；上传落盘服务端素材目录并 ffprobe 提取元数据。</summary>
    /// <remarks>权限：<c>media.write</c>。路径模式仅允许配置的素材根目录内，越界返回 403。</remarks>
    /// <param name="file">上传文件（multipart 字段）。</param>
    /// <param name="filePath">路径模式素材路径（multipart 字段），二选一。</param>
    /// <returns>素材视图统一响应；二选一不满足返回 422。</returns>
    [Authorize(Policy = PermissionNames.MediaWrite)]
    [HttpPost]
    public async Task<ActionResult<ApiResponse<object>>> Upload([FromForm] IFormFile file, [FromForm] string filePath) =>
        ToResponse(await WithUserAsync((user, token) =>
        {
            if (file is not null)
            {
                using var stream = file.OpenReadStream();
                var request = new ClippingMaterialUploadRequest(null, Path.GetFileName(file.FileName), file.ContentType, file.Length, stream);
                return _materials.UploadAsync(user.UserId, user.TenantId, request, token);
            }
            return _materials.UploadAsync(user.UserId, user.TenantId, new ClippingMaterialUploadRequest(filePath, null, null, 0, null), token);
        }));

    /// <summary>按登记时间倒序列出本人素材。</summary>
    /// <remarks>权限：<c>media.read</c>。仅返回本人素材。</remarks>
    /// <returns>素材列表统一响应。</returns>
    [Authorize(Policy = PermissionNames.MediaRead)]
    [HttpGet]
    public async Task<ActionResult<ApiResponse<object>>> List() =>
        ToResponse(await WithUserAsync((user, token) => _materials.ListAsync(user.UserId, user.TenantId, token)));

    /// <summary>软删除本人素材并写 media_file_deleted 审计。</summary>
    /// <remarks>权限：<c>media.write</c>。他人素材或不存在返回 404。</remarks>
    /// <param name="materialId">素材主键。</param>
    /// <returns>删除结果统一响应。</returns>
    [Authorize(Policy = PermissionNames.MediaWrite)]
    [HttpDelete("{materialId:long}")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long materialId) =>
        ToResponse(await WithUserAsync((user, token) => _materials.DeleteAsync(user.UserId, user.TenantId, materialId, token)));

    /// <summary>在用户上下文就绪时执行给定的业务回调，否则返回 401。</summary>
    /// <param name="action">执行业务逻辑的回调。</param>
    /// <returns>业务执行结果 <see cref="ServiceResult"/>。</returns>
    private async Task<ServiceResult> WithUserAsync(Func<UserContext, CancellationToken, Task<ServiceResult>> action) =>
        TryGetUser(out var user)
            ? await action(user, HttpContext.RequestAborted)
            : new ServiceResult(401, "未提供访问令牌，或访问令牌已过期。");

    /// <summary>将 <see cref="ServiceResult"/> 转换为统一 HTTP 响应。</summary>
    /// <param name="result">业务执行结果。</param>
    /// <returns>成功 2xx；失败按 <see cref="ServiceResult.StatusCode"/> 返回。</returns>
    private static ActionResult<ApiResponse<object>> ToResponse(ServiceResult result) => result.Succeeded
        ? new ObjectResult(new ApiResponse<object>(0, result.Message, result.Data)) { StatusCode = result.StatusCode }
        : new ObjectResult(ApiResponse<object>.Fail(result.Code, result.Message)) { StatusCode = result.StatusCode };
}
