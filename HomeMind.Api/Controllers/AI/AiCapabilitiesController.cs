using System;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using HomeMind.Api.Controllers.Base;
using HomeMind.Api.Services;
using HomeMind.Business.IServices.AI;
using HomeMind.Common.Model.ViewModel.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HomeMind.Api.Controllers.AI;

/// <summary>AI 生成能力占位入口（生成 / 对话 / 流式），调用前先校验 AI 配置启用开关。</summary>
/// <remarks>B18 占位路由：<c>POST /api/v1/ai/{generate,chat,stream}</c>。未启用时统一返回 HTTP 422 / <c>Code=42200</c>；
/// 启用时返回 HTTP 501 与提示"待后续切片接入"，等待真正模型调用实现。</remarks>
[Authorize]
[Route("api/v1/ai")]
[ApiExplorerSettings(GroupName = "个人/AI 生成能力")]
public sealed class AiCapabilitiesController : ApiControllerBase
{
    private readonly IAiConfigServices _configServices;

    /// <summary>构造 AI 生成能力占位控制器。</summary>
    /// <param name="configServices">AI 配置业务服务，用于闸门校验。</param>
    public AiCapabilitiesController(IAiConfigServices configServices) => _configServices = configServices;

    /// <summary>一次性生成：调用前先校验 AI 配置启用开关。</summary>
    /// <remarks>权限：<c>ai.run</c>。未启用 → HTTP 422；启用 → HTTP 501 占位响应。</remarks>
    /// <param name="request">单次生成请求体（占位）。</param>
    /// <returns>占位响应的统一响应。</returns>
    [Authorize(Policy = PermissionNames.AiRun)]
    [HttpPost("generate")]
    public Task<ActionResult<ApiResponse<object>>> Generate(AiCapabilityRequest request) => HandleAsync(request, HttpContext.RequestAborted);

    /// <summary>多轮对话：调用前先校验 AI 配置启用开关。</summary>
    /// <remarks>权限：<c>ai.run</c>。未启用 → HTTP 422；启用 → HTTP 501 占位响应。</remarks>
    /// <param name="request">多轮对话请求体（占位）。</param>
    /// <returns>占位响应的统一响应。</returns>
    [Authorize(Policy = PermissionNames.AiRun)]
    [HttpPost("chat")]
    public Task<ActionResult<ApiResponse<object>>> Chat(AiCapabilityRequest request) => HandleAsync(request, HttpContext.RequestAborted);

    /// <summary>流式生成：调用前先校验 AI 配置启用开关。</summary>
    /// <remarks>权限：<c>ai.run</c>。未启用 → HTTP 422；启用 → HTTP 501 占位响应。</remarks>
    /// <param name="request">流式生成请求体（占位）。</param>
    /// <returns>占位响应的统一响应。</returns>
    [Authorize(Policy = PermissionNames.AiRun)]
    [HttpPost("stream")]
    public Task<ActionResult<ApiResponse<object>>> Stream(AiCapabilityRequest request) => HandleAsync(request, HttpContext.RequestAborted);

    /// <summary>统一闸门：未配置 / 已禁用 → 422；启用 → 501 占位。</summary>
    /// <param name="request">占位请求体。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>统一响应。</returns>
    private async Task<ActionResult<ApiResponse<object>>> HandleAsync(AiCapabilityRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUser(out var user))
            return Unauthorized(ApiResponse<object>.Fail(ApiErrorCodes.AccessTokenInvalid, "未提供访问令牌，或访问令牌已过期。"));
        _ = request; // 占位入参，后续切片补真正字段
        if (!await _configServices.IsEnabledAsync(user.UserId, cancellationToken))
            return StatusCode(422, ApiResponse<object>.Fail(ApiErrorCodes.PreconditionFailed, "AI 生成能力已禁用，请在设置中开启。"));
        return StatusCode(501, ApiResponse<object>.Fail(ApiErrorCodes.NotImplemented, "AI 生成能力待后续切片接入。"));
    }
}

/// <summary>AI 生成能力占位请求体（B18）；后续切片补真正字段。</summary>
/// <param name="Prompt">用户输入提示词，当前切片仅占位。</param>
public sealed record AiCapabilityRequest(string Prompt)
{
    /// <summary>用户输入提示词（占位）。</summary>
    [Display(Name = "提示词")] public string Prompt { get; init; } = Prompt;
}
