using System;
using System.Threading.Tasks;
using Dapper;
using HomeMind.Api.Controllers.Base;
using HomeMind.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HomeMind.Api.Controllers.AI;

/// <summary>
/// AI 专家模块，提供专家目录、专家团详情和 AI 任务运行管理能力。
/// </summary>
[Authorize]
[Route("api/v1")]
public sealed class ExpertsController : ApiControllerBase
{
    private readonly MySqlConnectionFactory _connections;
    public ExpertsController(MySqlConnectionFactory connections) => _connections = connections;

    /// <summary>
    /// 按名称或分类查询当前租户可用的 AI 专家和专家团目录。
    /// </summary>
    [Authorize(Policy = PermissionNames.AiRead)]
    [HttpGet("experts")]
    public async Task<ActionResult<ApiResponse<object>>> ListExperts(string? query, string? category, string? type)
    {
        if (!TryGetUser(out var user)) return UnauthorizedResult<object>();
        await using var db = _connections.Open();
        var items = await db.QueryAsync("SELECT 'expert' catalogType,e.id,e.code,e.name,e.category,e.description,v.estimated_credits estimatedCredits FROM experts e JOIN expert_versions v ON v.expert_id=e.id AND v.status='published' WHERE e.status='active' AND (e.tenant_id=1 OR e.tenant_id=@TenantId) AND (@Category IS NULL OR e.category=@Category) AND (@Query IS NULL OR e.name LIKE CONCAT('%',@Query,'%') OR e.code LIKE CONCAT('%',@Query,'%')) UNION ALL SELECT 'group' catalogType,g.id,g.code,g.name,g.category,g.description,v.estimated_credits estimatedCredits FROM expert_groups g JOIN expert_group_versions v ON v.group_id=g.id AND v.status='published' WHERE g.status='active' AND (g.tenant_id=1 OR g.tenant_id=@TenantId) AND (@Category IS NULL OR g.category=@Category) AND (@Query IS NULL OR g.name LIKE CONCAT('%',@Query,'%') OR g.code LIKE CONCAT('%',@Query,'%'))", new { user.TenantId, Category = category, Query = query });
        return Ok(ApiResponse<object>.Ok(items));
    }

    /// <summary>
    /// 获取指定 AI 专家或专家团的已发布版本及调用配置。
    /// </summary>
    [Authorize(Policy = PermissionNames.AiRead)]
    [HttpGet("experts/{id:long}")]
    public async Task<ActionResult<ApiResponse<object>>> GetExpert(long id, string type = "expert")
    {
        if (!TryGetUser(out var user)) return UnauthorizedResult<object>();
        await using var db = _connections.Open();
        object? item = type == "group"
            ? await db.QuerySingleOrDefaultAsync("SELECT g.id,g.code,g.name,g.category,g.description,v.id versionId,v.version,v.orchestration_policy_json orchestrationPolicy,v.output_schema_json outputSchema,v.estimated_credits estimatedCredits FROM expert_groups g JOIN expert_group_versions v ON v.group_id=g.id AND v.status='published' WHERE g.id=@Id AND g.status='active' AND (g.tenant_id=1 OR g.tenant_id=@TenantId) ORDER BY v.version DESC LIMIT 1", new { Id = id, user.TenantId })
            : await db.QuerySingleOrDefaultAsync("SELECT e.id,e.code,e.name,e.category,e.description,e.privacy_scope_json privacyScope,v.id versionId,v.version,v.persona,v.methodology,v.tool_policy_json toolPolicy,v.output_schema_json outputSchema,v.estimated_credits estimatedCredits FROM experts e JOIN expert_versions v ON v.expert_id=e.id AND v.status='published' WHERE e.id=@Id AND e.status='active' AND (e.tenant_id=1 OR e.tenant_id=@TenantId) ORDER BY v.version DESC LIMIT 1", new { Id = id, user.TenantId });
        return item is null ? NotFoundResult<object>() : Ok(ApiResponse<object>.Ok(item));
    }

    /// <summary>
    /// 创建 AI 专家或专家团运行任务，并返回排队中的运行记录。
    /// </summary>
    [Authorize(Policy = PermissionNames.AiRun)]
    [HttpPost("expert-runs")]
    public async Task<ActionResult<ApiResponse<object>>> CreateRun(CreateRunRequest request)
    {
        if (!TryGetUser(out var user)) return UnauthorizedResult<object>();
        if (request.SourceType is not ("expert" or "group") || string.IsNullOrWhiteSpace(request.InputJson)) return BadRequest(ApiResponse<object>.Fail(422, "sourceType and inputJson are required."));
        await using var db = _connections.Open();
        await db.OpenAsync();
        await using var tx = await db.BeginTransactionAsync();
        long? expertVersion = null;
        long? groupVersion = null;
        if (request.SourceType == "expert") expertVersion = await db.QuerySingleOrDefaultAsync<long?>("SELECT v.id FROM expert_versions v JOIN experts e ON e.id=v.expert_id WHERE e.id=@Id AND v.status='published' AND e.status='active' AND (e.tenant_id=1 OR e.tenant_id=@TenantId) ORDER BY v.version DESC LIMIT 1", new { Id = request.SourceId, user.TenantId }, tx);
        else groupVersion = await db.QuerySingleOrDefaultAsync<long?>("SELECT v.id FROM expert_group_versions v JOIN expert_groups g ON g.id=v.group_id WHERE g.id=@Id AND v.status='published' AND g.status='active' AND (g.tenant_id=1 OR g.tenant_id=@TenantId) ORDER BY v.version DESC LIMIT 1", new { Id = request.SourceId, user.TenantId }, tx);
        if (expertVersion is null && groupVersion is null) { await tx.RollbackAsync(); return NotFoundResult<object>(); }
        var key = Guid.TryParse(request.IdempotencyKey, out var parsed) ? parsed.ToString() : Guid.NewGuid().ToString();
        var runId = await db.QuerySingleAsync<long>("INSERT INTO expert_runs(tenant_id,user_id,source_type,expert_version_id,group_version_id,request_idempotency_key,input_json,status,estimated_credits) VALUES (@TenantId,@UserId,@SourceType,@ExpertVersion,@GroupVersion,@Key,CAST(@InputJson AS JSON),'queued',@Credits) ON DUPLICATE KEY UPDATE id=LAST_INSERT_ID(id); SELECT LAST_INSERT_ID();", new { user.TenantId, user.UserId, request.SourceType, ExpertVersion = expertVersion, GroupVersion = groupVersion, Key = key, request.InputJson, Credits = request.SourceType == "group" ? 2.5m : 1m }, tx);
        await db.ExecuteAsync("INSERT IGNORE INTO expert_jobs(tenant_id,run_id,job_type,status,idempotency_key) VALUES (@TenantId,@RunId,'plan','queued',@Key)", new { user.TenantId, RunId = runId, Key = $"run-{runId}-plan" }, tx);
        await db.ExecuteAsync("INSERT IGNORE INTO run_events(tenant_id,run_id,sequence,event_type,display_payload_json) VALUES (@TenantId,@RunId,1,'queued',JSON_OBJECT('message','Run queued'))", new { user.TenantId, RunId = runId }, tx);
        await tx.CommitAsync();
        return Created($"/api/v1/expert-runs/{runId}", ApiResponse<object>.Ok(await GetRun(db, user, runId)));
    }

    /// <summary>
    /// 查询指定 AI 任务的状态、输入、结果和消耗信息。
    /// </summary>
    [Authorize(Policy = PermissionNames.AiRun)]
    [HttpGet("expert-runs/{id:long}")]
    public async Task<ActionResult<ApiResponse<object>>> GetRunById(long id)
    {
        if (!TryGetUser(out var user)) return UnauthorizedResult<object>();
        await using var db = _connections.Open();
        var item = await GetRun(db, user, id);
        return item is null ? NotFoundResult<object>() : Ok(ApiResponse<object>.Ok(item));
    }

    /// <summary>
    /// 获取指定 AI 任务按执行顺序记录的运行事件。
    /// </summary>
    [Authorize(Policy = PermissionNames.AiRun)]
    [HttpGet("expert-runs/{id:long}/events")]
    public async Task<ActionResult<ApiResponse<object>>> Events(long id)
    {
        if (!TryGetUser(out var user)) return UnauthorizedResult<object>();
        await using var db = _connections.Open();
        var items = await db.QueryAsync("SELECT e.id,e.sequence,e.event_type eventType,e.display_payload_json payload,e.created_at createdAt FROM run_events e JOIN expert_runs r ON r.id=e.run_id WHERE e.run_id=@Id AND e.tenant_id=@TenantId AND r.user_id=@UserId ORDER BY e.sequence", new { Id = id, user.TenantId, user.UserId });
        return Ok(ApiResponse<object>.Ok(items));
    }

    /// <summary>
    /// 请求取消尚未结束的 AI 任务；可立即取消的任务会同步更新状态。
    /// </summary>
    [Authorize(Policy = PermissionNames.AiRun)]
    [HttpPost("expert-runs/{id:long}/cancel")]
    public async Task<ActionResult<ApiResponse<object>>> Cancel(long id)
    {
        if (!TryGetUser(out var user)) return UnauthorizedResult<object>();
        await using var db = _connections.Open();
        var changed = await db.ExecuteAsync("UPDATE expert_runs SET cancel_requested_at=UTC_TIMESTAMP(3),status=CASE WHEN status IN ('draft','queued','planning') THEN 'cancelled' ELSE status END WHERE id=@Id AND tenant_id=@TenantId AND user_id=@UserId AND status NOT IN ('completed','failed','cancelled')", new { Id = id, user.TenantId, user.UserId });
        return changed == 0 ? NotFoundResult<object>() : Ok(ApiResponse<object>.Ok(new { id, cancelRequested = true }));
    }

    /// <summary>
    /// 将失败、已取消或等待输入的 AI 任务重新加入执行队列。
    /// </summary>
    [Authorize(Policy = PermissionNames.AiRun)]
    [HttpPost("expert-runs/{id:long}/retry")]
    public async Task<ActionResult<ApiResponse<object>>> Retry(long id)
    {
        if (!TryGetUser(out var user)) return UnauthorizedResult<object>();
        await using var db = _connections.Open();
        var changed = await db.ExecuteAsync("UPDATE expert_runs SET status='queued',cancel_requested_at=NULL WHERE id=@Id AND tenant_id=@TenantId AND user_id=@UserId AND status IN ('failed','cancelled','needs_input')", new { Id = id, user.TenantId, user.UserId });
        return changed == 0 ? BadRequest(ApiResponse<object>.Fail(422, "Only failed, cancelled or needs-input runs can be retried.")) : Ok(ApiResponse<object>.Ok(new { id, status = "queued" }));
    }

    /// <summary>
    /// 为 AI 任务创建待办、日历或计划等后续执行动作。
    /// </summary>
    [Authorize(Policy = PermissionNames.AiRun)]
    [HttpPost("expert-runs/{id:long}/actions")]
    public async Task<ActionResult<ApiResponse<object>>> CreateAction(long id, RunActionRequest request)
    {
        if (!TryGetUser(out var user)) return UnauthorizedResult<object>();
        if (request.ActionType is not ("plan" or "todos" or "calendar_events")) return BadRequest(ApiResponse<object>.Fail(422, "actionType must be plan, todos or calendar_events."));
        await using var db = _connections.Open();
        var key = Guid.TryParse(request.IdempotencyKey, out var parsed) ? parsed.ToString() : Guid.NewGuid().ToString();
        var actionId = await db.QuerySingleOrDefaultAsync<long?>("INSERT INTO expert_run_actions(run_id,tenant_id,user_id,action_type,request_idempotency_key,request_json,status) SELECT r.id,r.tenant_id,r.user_id,@ActionType,@Key,CAST(@RequestJson AS JSON),'queued' FROM expert_runs r WHERE r.id=@RunId AND r.tenant_id=@TenantId AND r.user_id=@UserId ON DUPLICATE KEY UPDATE id=LAST_INSERT_ID(id); SELECT LAST_INSERT_ID();", new { RunId = id, user.TenantId, user.UserId, request.ActionType, Key = key, RequestJson = string.IsNullOrWhiteSpace(request.RequestJson) ? "{}" : request.RequestJson });
        return actionId is null or 0 ? NotFoundResult<object>() : Ok(ApiResponse<object>.Ok(new { id = actionId, runId = id, status = "queued" }));
    }

    private static Task<dynamic?> GetRun(System.Data.IDbConnection db, UserContext user, long id) => db.QuerySingleOrDefaultAsync("SELECT id,source_type sourceType,status,input_json input,result_json result,result_summary resultSummary,estimated_credits estimatedCredits,actual_credits actualCredits,created_at createdAt,started_at startedAt,finished_at finishedAt FROM expert_runs WHERE id=@Id AND tenant_id=@TenantId AND user_id=@UserId", new { Id = id, user.TenantId, user.UserId });
    public sealed record CreateRunRequest(string SourceType, long SourceId, string InputJson, string? IdempotencyKey);
    public sealed record RunActionRequest(string ActionType, string? RequestJson, string? IdempotencyKey);
}
