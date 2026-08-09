using System;
using System.Threading;
using System.Threading.Tasks;
using HomeMind.Api.Controllers.Base;
using HomeMind.Api.Services;
using HomeMind.Business.IServices.Media;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.Media;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HomeMind.Api.Controllers.Media;

/// <summary>快速剪辑对话引导入口（B32）：无状态 context 回传推进 + 规则意图匹配 + 模板回复；只引导不执行。</summary>
/// <remarks>方案生成/确认/下载仍走既有 Skill Run 链路（POST /skills/quick-edit/runs、confirm、readToken）；响应不含 MCP 内部路径或 Prompt。</remarks>
[Authorize]
[Route("api/v1/clipping")]
public sealed class ClippingChatController : ApiControllerBase
{
    private readonly IClippingChatServices _chat;

    /// <summary>构造剪辑对话引导控制器。</summary>
    /// <param name="chat">剪辑对话引导服务。</param>
    public ClippingChatController(IClippingChatServices chat) => _chat = chat;

    /// <summary>处理一条剪辑对话消息：按回传上下文推进引导步骤，返回模板回复与 suggestions 快捷操作。</summary>
    /// <remarks>权限：<c>ai.run</c> + <c>media.read</c>。上下文步骤非法或消息为空返回 422。</remarks>
    /// <param name="request">对话请求，含消息与回传上下文。</param>
    /// <returns>引导响应统一响应。</returns>
    [Authorize(Policy = PermissionNames.AiRun)]
    [Authorize(Policy = PermissionNames.MediaRead)]
    [HttpPost("chat")]
    public async Task<ActionResult<ApiResponse<object>>> Chat(ClippingChatRequest request) =>
        ToResponse(await WithUserAsync((user, token) => _chat.ChatAsync(user.UserId, user.TenantId, request, token)));

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
