using HomeMind.Api.Controllers.Base;
using HomeMind.Api.Services;
using HomeMind.Business.IServices.SmartHome;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.SmartHome;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace HomeMind.Api.Controllers.SmartHome;

/// <summary>管理经授权的长期自动化规则。设备侧效果继续使用现有的 Run、Action 和审计链路。</summary>
/// <remarks>规则动作被限制为内置场景键；自动执行策略在规则所有者的有效授权下才会运行，绝不绕过命令边界。</remarks>
[Authorize]
[Route("api/v1/automation-rules")]
public sealed class AutomationRulesController : ApiControllerBase
{
    private readonly IAutomationRuleServices _rules;

    /// <summary>构造自动化规则控制器。</summary>
    /// <param name="rules">自动化规则业务服务。</param>
    public AutomationRulesController(IAutomationRuleServices rules) => _rules = rules;

    /// <summary>列出当前租户内的所有自动化规则。</summary>
    /// <remarks>权限：<c>automation.read</c>。租户从 JWT 派生，跨租户不可见。</remarks>
    /// <returns>规则列表统一响应。</returns>
    [Authorize(Policy = PermissionNames.AutomationRead)]
    [HttpGet]
    public async Task<ActionResult<ApiResponse<object>>> List() => ToResponse(TryGetUser(out var user)
        ? await _rules.ListAsync(user.TenantId, HttpContext.RequestAborted)
        : new ServiceResult(401, "未提供访问令牌，或访问令牌已过期。"));

    /// <summary>创建一个新的自动化规则。仅所有者和管理员可创建。</summary>
    /// <remarks>权限：<c>automation.write</c>。规则名称、触发类型与动作列表必填。</remarks>
    /// <param name="request">规则创建请求体。</param>
    /// <returns>新建规则统一响应。</returns>
    [Authorize(Policy = PermissionNames.AutomationWrite)]
    [HttpPost]
    public async Task<ActionResult<ApiResponse<object>>> Create(AutomationRuleRequest request) => ToResponse(TryGetUser(out var user)
        ? await _rules.CreateAsync(user.UserId, user.TenantId, request, HttpContext.RequestAborted)
        : new ServiceResult(401, "未提供访问令牌，或访问令牌已过期。"));

    /// <summary>按主键部分更新自动化规则；需要返回的 <c>rowVersion</c> 以保证乐观锁。</summary>
    /// <remarks>权限：<c>automation.write</c>。版本号不匹配将返回 409。</remarks>
    /// <param name="id">规则主键。</param>
    /// <param name="request">规则更新请求体，必须携带当前 <c>rowVersion</c>。</param>
    /// <returns>更新结果统一响应。</returns>
    [Authorize(Policy = PermissionNames.AutomationWrite)]
    [HttpPatch("{id:long}")]
    public async Task<ActionResult<ApiResponse<object>>> Update(long id, UpdateAutomationRuleRequest request) => ToResponse(TryGetUser(out var user)
        ? await _rules.UpdateAsync(user.UserId, user.TenantId, id, request, HttpContext.RequestAborted)
        : new ServiceResult(401, "未提供访问令牌，或访问令牌已过期。"));

    /// <summary>将 <see cref="ServiceResult"/> 转换为统一 HTTP 响应。</summary>
    /// <param name="result">业务执行结果。</param>
    /// <returns>统一响应体与对应状态码。</returns>
    private static ActionResult<ApiResponse<object>> ToResponse(ServiceResult result) => result.Succeeded
        ? new ObjectResult(new ApiResponse<object>(0, result.Message, result.Data)) { StatusCode = result.StatusCode }
        : new ObjectResult(ApiResponse<object>.Fail(result.Code, result.Message)) { StatusCode = result.StatusCode };
}
