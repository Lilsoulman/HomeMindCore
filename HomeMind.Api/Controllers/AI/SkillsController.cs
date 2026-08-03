using System.Threading.Tasks;
using Dapper;
using HomeMind.Api.Controllers.Base;
using HomeMind.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HomeMind.Api.Controllers.AI;

/// <summary>
/// AI 技能模块，管理当前用户可在 AI 任务中使用的提示词技能。
/// </summary>
[Authorize]
[Route("api/v1/skills")]
public sealed class SkillsController : ApiControllerBase
{
    private readonly MySqlConnectionFactory _connections;
    public SkillsController(MySqlConnectionFactory connections) => _connections = connections;

    /// <summary>
    /// 获取当前用户的内置与自定义 AI 技能列表。
    /// </summary>
    [Authorize(Policy = PermissionNames.AiSkillsRead)]
    [HttpGet]
    public async Task<ActionResult<ApiResponse<object>>> List()
    {
        if (!TryGetUser(out var user)) return UnauthorizedResult<object>();
        await using var db = _connections.Open();
        var items = await db.QueryAsync("SELECT id,name,prompt,scopes,is_builtin isBuiltin,is_active isActive,created_at createdAt,updated_at updatedAt FROM ai_skills WHERE tenant_id=@TenantId AND user_id=@UserId AND deleted_at IS NULL ORDER BY is_builtin DESC,name", new { user.TenantId, user.UserId });
        return Ok(ApiResponse<object>.Ok(items));
    }

    /// <summary>
    /// 创建一个可指定适用范围和启用状态的自定义 AI 技能。
    /// </summary>
    [Authorize(Policy = PermissionNames.AiSkillsWrite)]
    [HttpPost]
    public async Task<ActionResult<ApiResponse<object>>> Create(SkillRequest request)
    {
        if (!TryGetUser(out var user)) return UnauthorizedResult<object>();
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Prompt)) return BadRequest(ApiResponse<object>.Fail(422, "name and prompt are required."));
        await using var db = _connections.Open();
        var id = await db.QuerySingleAsync<long>("INSERT INTO ai_skills(tenant_id,user_id,name,prompt,scopes,is_builtin,is_active) VALUES (@TenantId,@UserId,@Name,@Prompt,CAST(@Scopes AS JSON),0,COALESCE(@Active,1)); SELECT LAST_INSERT_ID();", new { user.TenantId, user.UserId, Name = request.Name.Trim(), Prompt = request.Prompt, Scopes = string.IsNullOrWhiteSpace(request.Scopes) ? "[]" : request.Scopes, Active = request.IsActive });
        return Created($"/api/v1/skills/{id}", ApiResponse<object>.Ok(await db.QuerySingleAsync("SELECT id,name,prompt,scopes,is_active isActive FROM ai_skills WHERE id=@Id", new { Id = id })));
    }

    /// <summary>
    /// 更新指定自定义 AI 技能的名称、提示词、范围或启用状态。
    /// </summary>
    [Authorize(Policy = PermissionNames.AiSkillsWrite)]
    [HttpPut("{id:long}")]
    public async Task<ActionResult<ApiResponse<object>>> Update(long id, SkillRequest request)
    {
        if (!TryGetUser(out var user)) return UnauthorizedResult<object>();
        await using var db = _connections.Open();
        var changed = await db.ExecuteAsync("UPDATE ai_skills SET name=COALESCE(@Name,name),prompt=COALESCE(@Prompt,prompt),scopes=CASE WHEN @Scopes IS NULL THEN scopes ELSE CAST(@Scopes AS JSON) END,is_active=COALESCE(@Active,is_active) WHERE id=@Id AND tenant_id=@TenantId AND user_id=@UserId AND deleted_at IS NULL", new { Id = id, user.TenantId, user.UserId, request.Name, request.Prompt, request.Scopes, Active = request.IsActive });
        return changed == 0 ? NotFoundResult<object>() : Ok(ApiResponse<object>.Ok(new { id }));
    }

    /// <summary>
    /// 软删除指定的自定义 AI 技能。
    /// </summary>
    [Authorize(Policy = PermissionNames.AiSkillsWrite)]
    [HttpDelete("{id:long}")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id)
    {
        if (!TryGetUser(out var user)) return UnauthorizedResult<object>();
        await using var db = _connections.Open();
        var changed = await db.ExecuteAsync("UPDATE ai_skills SET deleted_at=UTC_TIMESTAMP(3) WHERE id=@Id AND tenant_id=@TenantId AND user_id=@UserId AND deleted_at IS NULL", new { Id = id, user.TenantId, user.UserId });
        return changed == 0 ? NotFoundResult<object>() : Ok(ApiResponse<object>.Ok(new { id }));
    }

    public sealed record SkillRequest(string? Name, string? Prompt, string? Scopes, bool? IsActive);
}
