using System;
using System.Threading;
using System.Threading.Tasks;
using HomeMind.Api.Controllers.Base;
using HomeMind.Api.Services;
using HomeMind.Business.IServices.Expert;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.AI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HomeMind.Api.Controllers.AI;

/// <summary>Expert File 上传、对象提交、列表、附件与读取令牌。Controller 仅处理 HTTP 和鉴权；不持有对象存储凭据。</summary>
/// <remarks>所有响应均不包含对象存储内部路径、凭据或厂商标识。文件 ID 跨租户访问将返回 404。</remarks>
[Authorize]
[Route("api/v1")]
public sealed class ExpertFilesController : ApiControllerBase
{
    private readonly IExpertFileServices _files;

    /// <summary>构造专家文件控制器。</summary>
    /// <param name="files">专家文件业务服务。</param>
    public ExpertFilesController(IExpertFileServices files) => _files = files;

    /// <summary>创建 Expert File 上传会话，返回短期 <c>uploadToken</c> 与 <c>uploadUrl</c>。</summary>
    /// <remarks>权限：<c>expert_file.write</c>。仅声明元数据，二进制通过 <c>uploadUrl</c> 单独上传。成功时文件状态为 <c>pending_upload</c>。</remarks>
    /// <param name="request">上传会话请求体，包含文件名、MIME、字节数、SHA-256 与可选幂等键。</param>
    /// <returns>包含 fileId、status、uploadToken、uploadUrl、expiresAtUnixTime 的统一响应。</returns>
    [Authorize(Policy = PermissionNames.ExpertFileWrite)]
    [HttpPost("expert-files")]
    public async Task<ActionResult<ApiResponse<object>>> CreateUpload(ExpertFileUploadRequest request) =>
        ToResponse(await WithUserAsync((user, token) => _files.CreateUploadAsync(user.UserId, user.TenantId, request, token)));

    /// <summary>提交已上传的对象分片元数据，触发扫描并将状态置为 <c>ready</c> 或 <c>rejected</c>。</summary>
    /// <remarks>权限：<c>expert_file.write</c>。仅 <c>pending_upload</c> 或 <c>scanning</c> 状态的文件可被提交。</remarks>
    /// <param name="fileId">文件主键。</param>
    /// <param name="request">对象分片元数据请求体。</param>
    /// <returns>提交结果的统一响应。</returns>
    [Authorize(Policy = PermissionNames.ExpertFileWrite)]
    [HttpPost("expert-files/{fileId:long}/objects")]
    public async Task<ActionResult<ApiResponse<object>>> CommitObject(long fileId, ExpertFileObjectRequest request) =>
        ToResponse(await WithUserAsync((user, token) => _files.CommitObjectAsync(user.UserId, user.TenantId, fileId, request, token)));

    /// <summary>列出当前租户内的 Expert File 摘要，不返回内部对象路径或扫描提供方密钥。</summary>
    /// <remarks>权限：<c>expert_file.read</c>。跨租户文件 ID 返回 404。</remarks>
    /// <returns>文件摘要列表的统一响应。</returns>
    [Authorize(Policy = PermissionNames.ExpertFileRead)]
    [HttpGet("expert-files")]
    public async Task<ActionResult<ApiResponse<object>>> List() =>
        ToResponse(await WithUserAsync((user, token) => _files.ListAsync(user.UserId, user.TenantId, token)));

    /// <summary>软删除文件，移除附件并尽力清理存储。</summary>
    /// <remarks>权限：<c>expert_file.write</c>。会写入 <c>file_delete</c> 审计条目。</remarks>
    /// <param name="fileId">文件主键。</param>
    /// <returns>删除结果的统一响应。</returns>
    [Authorize(Policy = PermissionNames.ExpertFileWrite)]
    [HttpDelete("expert-files/{fileId:long}")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long fileId) =>
        ToResponse(await WithUserAsync((user, token) => _files.DeleteAsync(user.UserId, user.TenantId, fileId, token)));

    /// <summary>将 ready 状态文件附加到指定 Expert。</summary>
    /// <remarks>权限：<c>expert_file.write</c>。仅接受同租户、<c>ready</c> 状态的文件；会写入 <c>file_attach</c> 审计条目。</remarks>
    /// <param name="expertId">目标专家主键。</param>
    /// <param name="request">附件请求体，包含 fileId 与可选幂等键。</param>
    /// <returns>附件结果的统一响应。</returns>
    [Authorize(Policy = PermissionNames.ExpertFileWrite)]
    [HttpPost("experts/{expertId:long}/files")]
    public async Task<ActionResult<ApiResponse<object>>> AttachToExpert(long expertId, ExpertFileAttachmentRequest request) =>
        ToResponse(await WithUserAsync((user, token) => _files.AttachToExpertAsync(user.UserId, user.TenantId, expertId, request, token)));

    /// <summary>将 ready 状态文件附加到指定 AgentRun。</summary>
    /// <remarks>权限：<c>expert_file.write</c>。仅接受同租户、<c>ready</c> 状态的文件；会写入 <c>file_attach</c> 审计条目。</remarks>
    /// <param name="runId">目标运行主键。</param>
    /// <param name="request">附件请求体，包含 fileId 与可选幂等键。</param>
    /// <returns>附件结果的统一响应。</returns>
    [Authorize(Policy = PermissionNames.ExpertFileWrite)]
    [HttpPost("expert-runs/{runId:long}/files")]
    public async Task<ActionResult<ApiResponse<object>>> AttachToRun(long runId, ExpertFileAttachmentRequest request) =>
        ToResponse(await WithUserAsync((user, token) => _files.AttachToRunAsync(user.UserId, user.TenantId, runId, request, token)));

    /// <summary>颁发短期、按用途限制的文件读取令牌。</summary>
    /// <remarks>权限：<c>expert_file.read</c>。需要 <c>purpose</c> 查询参数；令牌在 10 分钟内过期；每次颁发都写入 <c>file_read</c> 审计条目。</remarks>
    /// <param name="fileId">文件主键。</param>
    /// <param name="purpose">用途描述，例如 <c>preview</c> 或 <c>download</c>。</param>
    /// <returns>读取令牌与下载 URL 的统一响应。</returns>
    [Authorize(Policy = PermissionNames.ExpertFileRead)]
    [HttpPost("expert-files/{fileId:long}/read-token")]
    public async Task<ActionResult<ApiResponse<object>>> GenerateReadToken(long fileId, [FromQuery] string purpose = "download") =>
        ToResponse(await WithUserAsync((user, token) => _files.GenerateReadTokenAsync(user.UserId, user.TenantId, fileId, purpose, token)));

    /// <summary>读取文件字节流并以下载形式返回。</summary>
    /// <remarks>权限：<c>expert_file.read</c>。Bearer 鉴权 + 租户归属校验；文件必须处于 <c>ready</c> 状态。</remarks>
    /// <param name="fileId">文件主键。</param>
    /// <returns>文件二进制流（attachment 下载）。</returns>
    [Authorize(Policy = PermissionNames.ExpertFileRead)]
    [HttpGet("expert-files/{fileId:long}/content")]
    public async Task<IActionResult> GetContent(long fileId)
    {
        var result = await WithUserAsync((user, token) => _files.GetContentAsync(user.UserId, user.TenantId, fileId, token));
        if (result.Succeeded && result.Data is GeneratedFileContent content)
            return File(content.Bytes, content.MimeType, content.Name);
        return result.Succeeded
            ? new ObjectResult(new ApiResponse<object>(0, result.Message, result.Data)) { StatusCode = result.StatusCode }
            : new ObjectResult(ApiResponse<object>.Fail(result.Code, result.Message)) { StatusCode = result.StatusCode };
    }

    /// <summary>在用户上下文就绪时执行给定的业务回调，否则返回 401。</summary>
    /// <param name="action">执行业务逻辑的回调，接收 <see cref="UserContext"/> 与取消令牌。</param>
    /// <returns>业务执行结果 <see cref="ServiceResult"/>。</returns>
    private async Task<ServiceResult> WithUserAsync(Func<UserContext, CancellationToken, Task<ServiceResult>> action) =>
        TryGetUser(out var user)
            ? await action(user, HttpContext.RequestAborted)
            : new ServiceResult(401, "未提供访问令牌，或访问令牌已过期。");

    /// <summary>将 <see cref="ServiceResult"/> 转换为统一 HTTP 响应。</summary>
    /// <param name="result">业务执行结果。</param>
    /// <returns>成功时返回 2xx，失败时按 <see cref="ServiceResult.StatusCode"/> 返回对应错误响应。</returns>
    private static ActionResult<ApiResponse<object>> ToResponse(ServiceResult result) => result.Succeeded
        ? new ObjectResult(new ApiResponse<object>(0, result.Message, result.Data)) { StatusCode = result.StatusCode }
        : new ObjectResult(ApiResponse<object>.Fail(result.Code, result.Message)) { StatusCode = result.StatusCode };
}
