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
[Authorize]
[Route("api/v1/automation-rules")]
public sealed class AutomationRulesController : ApiControllerBase
{
    private readonly IAutomationRuleServices _rules;
    public AutomationRulesController(IAutomationRuleServices rules) => _rules = rules;

    [Authorize(Policy = PermissionNames.AutomationRead)]
    [HttpGet]
    public async Task<ActionResult<ApiResponse<object>>> List() => ToResponse(TryGetUser(out var user)
        ? await _rules.ListAsync(user.TenantId, HttpContext.RequestAborted)
        : new ServiceResult(401, "未提供访问令牌，或访问令牌已过期。"));

    [Authorize(Policy = PermissionNames.AutomationWrite)]
    [HttpPost]
    public async Task<ActionResult<ApiResponse<object>>> Create(AutomationRuleRequest request) => ToResponse(TryGetUser(out var user)
        ? await _rules.CreateAsync(user.UserId, user.TenantId, request, HttpContext.RequestAborted)
        : new ServiceResult(401, "未提供访问令牌，或访问令牌已过期。"));

    [Authorize(Policy = PermissionNames.AutomationWrite)]
    [HttpPatch("{id:long}")]
    public async Task<ActionResult<ApiResponse<object>>> Update(long id, UpdateAutomationRuleRequest request) => ToResponse(TryGetUser(out var user)
        ? await _rules.UpdateAsync(user.UserId, user.TenantId, id, request, HttpContext.RequestAborted)
        : new ServiceResult(401, "未提供访问令牌，或访问令牌已过期。"));

    private static ActionResult<ApiResponse<object>> ToResponse(ServiceResult result) => result.Succeeded
        ? new ObjectResult(new ApiResponse<object>(0, result.Message, result.Data)) { StatusCode = result.StatusCode }
        : new ObjectResult(ApiResponse<object>.Fail(result.StatusCode, result.Message)) { StatusCode = result.StatusCode };
}
