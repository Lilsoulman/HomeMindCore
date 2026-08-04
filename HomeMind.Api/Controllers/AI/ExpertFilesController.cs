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
[Authorize]
[Route("api/v1")]
public sealed class ExpertFilesController : ApiControllerBase
{
    private readonly IExpertFileServices _files;

    public ExpertFilesController(IExpertFileServices files) => _files = files;

    [Authorize(Policy = PermissionNames.ExpertFileWrite)]
    [HttpPost("expert-files")]
    public async Task<ActionResult<ApiResponse<object>>> CreateUpload(ExpertFileUploadRequest request) =>
        ToResponse(await WithUserAsync((user, token) => _files.CreateUploadAsync(user.UserId, user.TenantId, request, token)));

    [Authorize(Policy = PermissionNames.ExpertFileWrite)]
    [HttpPost("expert-files/{fileId:long}/objects")]
    public async Task<ActionResult<ApiResponse<object>>> CommitObject(long fileId, ExpertFileObjectRequest request) =>
        ToResponse(await WithUserAsync((user, token) => _files.CommitObjectAsync(user.UserId, user.TenantId, fileId, request, token)));

    [Authorize(Policy = PermissionNames.ExpertFileRead)]
    [HttpGet("expert-files")]
    public async Task<ActionResult<ApiResponse<object>>> List() =>
        ToResponse(await WithUserAsync((user, token) => _files.ListAsync(user.UserId, user.TenantId, token)));

    [Authorize(Policy = PermissionNames.ExpertFileWrite)]
    [HttpDelete("expert-files/{fileId:long}")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long fileId) =>
        ToResponse(await WithUserAsync((user, token) => _files.DeleteAsync(user.UserId, user.TenantId, fileId, token)));

    [Authorize(Policy = PermissionNames.ExpertFileWrite)]
    [HttpPost("experts/{expertId:long}/files")]
    public async Task<ActionResult<ApiResponse<object>>> AttachToExpert(long expertId, ExpertFileAttachmentRequest request) =>
        ToResponse(await WithUserAsync((user, token) => _files.AttachToExpertAsync(user.UserId, user.TenantId, expertId, request, token)));

    [Authorize(Policy = PermissionNames.ExpertFileWrite)]
    [HttpPost("expert-runs/{runId:long}/files")]
    public async Task<ActionResult<ApiResponse<object>>> AttachToRun(long runId, ExpertFileAttachmentRequest request) =>
        ToResponse(await WithUserAsync((user, token) => _files.AttachToRunAsync(user.UserId, user.TenantId, runId, request, token)));

    [Authorize(Policy = PermissionNames.ExpertFileRead)]
    [HttpPost("expert-files/{fileId:long}/read-token")]
    public async Task<ActionResult<ApiResponse<object>>> GenerateReadToken(long fileId, [FromQuery] string purpose = "download") =>
        ToResponse(await WithUserAsync((user, token) => _files.GenerateReadTokenAsync(user.UserId, user.TenantId, fileId, purpose, token)));

    private async Task<ServiceResult> WithUserAsync(Func<UserContext, CancellationToken, Task<ServiceResult>> action) =>
        TryGetUser(out var user)
            ? await action(user, HttpContext.RequestAborted)
            : new ServiceResult(401, "未提供访问令牌，或访问令牌已过期。");

    private static ActionResult<ApiResponse<object>> ToResponse(ServiceResult result) => result.Succeeded
        ? new ObjectResult(new ApiResponse<object>(0, result.Message, result.Data)) { StatusCode = result.StatusCode }
        : new ObjectResult(ApiResponse<object>.Fail(result.StatusCode, result.Message)) { StatusCode = result.StatusCode };
}
