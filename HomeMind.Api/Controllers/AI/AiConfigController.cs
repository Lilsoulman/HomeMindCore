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

/// <summary>AI 配置模块，配置按用户隔离，控制器不直接访问数据库。</summary>
/// <remarks>API 密钥提交后由服务端加密保存，响应中仅回传是否已配置，永不回传密钥本身。</remarks>
[Authorize]
[Route("api/v1/ai/config")]
public sealed class AiConfigController : ApiControllerBase
{
    private readonly IAiConfigServices _configServices;

    /// <summary>构造 AI 配置控制器。</summary>
    /// <param name="configServices">AI 配置业务服务。</param>
    public AiConfigController(IAiConfigServices configServices) => _configServices = configServices;

    /// <summary>读取当前用户的 AI 配置；未配置时返回默认值（含启用开关）。</summary>
    /// <remarks>权限：<c>ai.config.read</c>。返回字段固定为 <c>{ endpoint, model, temperature, hasApiKey, enabled }</c>。</remarks>
    /// <returns>AI 配置的统一响应。</returns>
    [Authorize(Policy = PermissionNames.AiConfigRead)]
    [HttpGet]
    public async Task<ActionResult<ApiResponse<object>>> Get() => ToResponse(await WithUserAsync((user, token) => _configServices.GetAsync(user.UserId, token)));

    /// <summary>保存当前用户的 AI 配置；apiKey 为空表示保留已保存的密钥，enabled 用于切换 AI 生成能力总开关。</summary>
    /// <remarks>权限：<c>ai.config.write</c>。请求体字段：<c>endpoint</c>、<c>model</c>、<c>temperature</c>、<c>enabled</c>、<c>apiKey</c>（可空）。切换开关时仅传 <c>enabled</c> 即可，密钥不会被清空。</remarks>
    /// <param name="request">AI 配置保存请求体。</param>
    /// <returns>保存后配置的统一响应。</returns>
    [Authorize(Policy = PermissionNames.AiConfigWrite)]
    [HttpPut]
    public async Task<ActionResult<ApiResponse<object>>> Save(AiConfigRequest request) => ToResponse(await WithUserAsync((user, token) => _configServices.SaveAsync(user.UserId, request, token)));

    /// <summary>在用户上下文就绪时执行给定的业务回调，否则返回 401。</summary>
    /// <param name="action">执行业务逻辑的回调。</param>
    /// <returns>业务执行结果 <see cref="ServiceResult"/>。</returns>
    private async Task<ServiceResult> WithUserAsync(Func<UserContext, CancellationToken, Task<ServiceResult>> action) => TryGetUser(out var user) ? await action(user, HttpContext.RequestAborted) : new ServiceResult(401, "未提供访问令牌，或访问令牌已过期。");

    /// <summary>将 <see cref="ServiceResult"/> 转换为统一 HTTP 响应。</summary>
    /// <param name="result">业务执行结果。</param>
    /// <returns>统一响应体与对应状态码。</returns>
    private ActionResult<ApiResponse<object>> ToResponse(ServiceResult result) => StatusCode(result.StatusCode, result.Succeeded ? new ApiResponse<object>(0, result.Message, result.Data) : ApiResponse<object>.Fail(result.Code, result.Message));
}
