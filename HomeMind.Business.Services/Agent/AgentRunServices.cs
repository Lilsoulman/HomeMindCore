using System.Text.Json;
using HomeMind.Business.IServices.Agent;
using HomeMind.Common.Model.Agent;
using HomeMind.Common.Model.Entities;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.AI;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;

namespace HomeMind.Business.Services.Agent;

/// <summary>
/// Coordinates AgentRun lifecycle. It resolves an Expert policy and queues work,
/// but never executes external effects itself.
/// </summary>
public sealed class AgentRunServices : IAgentRunServices
{
    private static readonly HashSet<string> AllowedActionTypes = new(StringComparer.Ordinal)
    {
        "plan", "todos", "calendar_events", "smart_home_device"
    };

    private readonly HomeMindDbContext _db;

    public AgentRunServices(HomeMindDbContext db) => _db = db;

    public async Task<ServiceResult> CreateAsync(long userId, long tenantId, AgentRunCreateRequest request, CancellationToken cancellationToken = default)
    {
        if (request.SourceType is not ("expert" or "group") || !IsJson(request.InputJson))
            return new ServiceResult(422, "请提供有效的专家来源和输入内容。");

        if (request.ConversationId is long conversationId
            && !await _db.Conversations.AnyAsync(x => x.Id == conversationId && x.TenantId == tenantId && x.OwnerUserId == userId && x.DeletedAt == null, cancellationToken))
            return new ServiceResult(404, "请求的会话不存在。");

        var source = await ResolveSourceAsync(tenantId, request.SourceType, request.SourceId, cancellationToken);
        if (source is null) return new ServiceResult(404, "请求的专家或专家团不存在。");

        var key = Guid.TryParse(request.IdempotencyKey, out var parsed) ? parsed.ToString() : Guid.NewGuid().ToString();
        var existing = await _db.AgentRuns.SingleOrDefaultAsync(
            x => x.TenantId == tenantId && x.UserId == userId && x.RequestIdempotencyKey == key,
            cancellationToken);
        if (existing is not null)
        {
            if (existing.SourceType != request.SourceType || existing.ExpertVersionId != source.ExpertVersionId || existing.GroupVersionId != source.GroupVersionId)
                return new ServiceResult(409, "该幂等键已用于其他 Agent 运行。");
            if (existing.ConversationId != request.ConversationId)
                return new ServiceResult(409, "该幂等键已用于其他会话的消息。");
            return new ServiceResult(200, "Agent 运行已存在。", ToView(existing));
        }

        var now = DateTime.UtcNow;
        var run = new AgentRun
        {
            TenantId = tenantId,
            UserId = userId,
            SourceType = request.SourceType,
            ExpertVersionId = source.ExpertVersionId,
            GroupVersionId = source.GroupVersionId,
            RequestIdempotencyKey = key,
            Input = request.InputJson,
            Status = AgentRunStatus.Queued,
            EstimatedCredits = source.EstimatedCredits,
            ConversationId = request.ConversationId,
            CreatedAt = now
        };
        _db.AgentRuns.Add(run);
        await _db.SaveChangesAsync(cancellationToken);

        _db.ExpertJobs.Add(new ExpertJob
        {
            TenantId = tenantId,
            RunId = run.Id,
            JobType = "plan",
            Status = AgentRunStatus.Queued,
            IdempotencyKey = $"run-{run.Id}-plan"
        });
        AddEvent(run, 1, AgentRunStatus.Queued, "Agent 任务已进入队列。", now);
        await _db.SaveChangesAsync(cancellationToken);
        return new ServiceResult(201, "Agent 任务已创建。", ToView(run));
    }

    public async Task<ServiceResult> GetAsync(long userId, long tenantId, long runId, CancellationToken cancellationToken = default)
    {
        var run = await FindAsync(userId, tenantId, runId, cancellationToken);
        return run is null ? new ServiceResult(404, "请求的 Agent 运行不存在。") : new ServiceResult(200, "查询成功。", ToView(run));
    }

    public async Task<ServiceResult> ListEventsAsync(long userId, long tenantId, long runId, CancellationToken cancellationToken = default)
    {
        if (await FindAsync(userId, tenantId, runId, cancellationToken) is null)
            return new ServiceResult(404, "请求的 Agent 运行不存在。");
        var events = await _db.RunEvents.Where(x => x.RunId == runId && x.TenantId == tenantId)
            .OrderBy(x => x.Sequence).ToListAsync(cancellationToken);
        return new ServiceResult(200, "查询成功。", events.Select(x => new { x.Id, x.Sequence, x.EventType, x.Payload, x.CreatedAt }));
    }

    public async Task<ServiceResult> CancelAsync(long userId, long tenantId, long runId, CancellationToken cancellationToken = default)
    {
        var run = await FindAsync(userId, tenantId, runId, cancellationToken);
        if (run is null) return new ServiceResult(404, "请求的 Agent 运行不存在。");
        if (AgentRunStatus.IsTerminal(run.Status)) return new ServiceResult(409, "该 Agent 运行已结束，不能取消。");

        var now = DateTime.UtcNow;
        run.CancelRequestedAt = now;
        if (run.Status is AgentRunStatus.Draft or AgentRunStatus.Queued or AgentRunStatus.Planning)
        {
            run.Status = AgentRunStatus.Cancelled;
            run.FinishedAt = now;
            AddEvent(run, await NextSequenceAsync(runId, cancellationToken), AgentRunStatus.Cancelled, "Agent 任务已取消。", now);
        }
        await _db.SaveChangesAsync(cancellationToken);
        return new ServiceResult(200, "已请求取消 Agent 任务。", new { id = runId, cancelRequested = true, status = run.Status });
    }

    public async Task<ServiceResult> RetryAsync(long userId, long tenantId, long runId, CancellationToken cancellationToken = default)
    {
        var run = await FindAsync(userId, tenantId, runId, cancellationToken);
        if (run is null) return new ServiceResult(404, "请求的 Agent 运行不存在。");
        if (run.Status is not (AgentRunStatus.Failed or AgentRunStatus.Cancelled))
            return new ServiceResult(422, "只有失败或已取消的 Agent 任务可以重试。");

        var now = DateTime.UtcNow;
        run.Status = AgentRunStatus.Queued;
        run.CancelRequestedAt = null;
        run.StartedAt = null;
        run.FinishedAt = null;
        _db.ExpertJobs.Add(new ExpertJob { TenantId = tenantId, RunId = run.Id, JobType = "retry", Status = AgentRunStatus.Queued, IdempotencyKey = Guid.NewGuid().ToString() });
        AddEvent(run, await NextSequenceAsync(runId, cancellationToken), AgentRunStatus.Queued, "Agent 任务已重新进入队列。", now);
        await _db.SaveChangesAsync(cancellationToken);
        return new ServiceResult(200, "Agent 任务已重新进入队列。", ToView(run));
    }

    public async Task<ServiceResult> ListAsync(long userId, long tenantId, string? sourceType, long? expertId, int limit, CancellationToken cancellationToken = default)
    {
        if (limit <= 0 || limit > 50) limit = 10;
        var query = _db.AgentRuns.Where(x => x.TenantId == tenantId && x.UserId == userId);
        if (sourceType is not null) query = query.Where(x => x.SourceType == sourceType);
        if (expertId is not null)
        {
            var versionIds = await _db.ExpertVersions.Where(v => v.ExpertId == expertId).Select(v => v.Id).ToListAsync(cancellationToken);
            query = query.Where(x => x.ExpertVersionId != null && versionIds.Contains(x.ExpertVersionId.Value));
        }
        var items = await query.OrderByDescending(x => x.Id).Take(limit).ToListAsync(cancellationToken);
        return new ServiceResult(200, "查询成功。", items.Select(ToView).ToArray());
    }

    public async Task<ServiceResult> CreateActionAsync(long userId, long tenantId, long runId, AgentRunActionRequest request, CancellationToken cancellationToken = default)
    {
        if (!AllowedActionTypes.Contains(request.ActionType) || !IsJson(request.RequestJson ?? "{}"))
            return new ServiceResult(422, "操作类型或请求内容格式无效。");
        if (await FindAsync(userId, tenantId, runId, cancellationToken) is null)
            return new ServiceResult(404, "请求的 Agent 运行不存在。");

        var key = Guid.TryParse(request.IdempotencyKey, out var parsed) ? parsed.ToString() : Guid.NewGuid().ToString();
        var action = await _db.ExpertRunActions.SingleOrDefaultAsync(x => x.RunId == runId && x.RequestIdempotencyKey == key, cancellationToken);
        if (action is null)
        {
            var now = DateTime.UtcNow;
            action = new ExpertRunAction
            {
                RunId = runId,
                TenantId = tenantId,
                UserId = userId,
                ActionType = request.ActionType,
                RequestIdempotencyKey = key,
                RequestJson = request.RequestJson ?? "{}",
                Status = "queued",
                CreatedAt = now,
                UpdatedAt = now
            };
            _db.ExpertRunActions.Add(action);
            await _db.SaveChangesAsync(cancellationToken);
        }
        return new ServiceResult(200, "Agent 行动已创建。", new { id = action.Id, runId, status = action.Status });
    }

    private async Task<AgentRun?> FindAsync(long userId, long tenantId, long runId, CancellationToken cancellationToken) =>
        await _db.AgentRuns.SingleOrDefaultAsync(x => x.Id == runId && x.TenantId == tenantId && x.UserId == userId, cancellationToken);

    private async Task<ResolvedSource?> ResolveSourceAsync(long tenantId, string sourceType, long sourceId, CancellationToken cancellationToken)
    {
        if (sourceType == "expert")
        {
            return await (from version in _db.ExpertVersions
                          join expert in _db.Experts on version.ExpertId equals expert.Id
                          where expert.Id == sourceId && expert.Status == "active" && version.Status == "published" && (expert.TenantId == 1 || expert.TenantId == tenantId)
                          orderby version.Version descending
                          select new ResolvedSource(version.Id, null, version.EstimatedCredits)).FirstOrDefaultAsync(cancellationToken);
        }
        return await (from version in _db.ExpertGroupVersions
                      join expertGroup in _db.ExpertGroups on version.GroupId equals expertGroup.Id
                      where expertGroup.Id == sourceId && expertGroup.Status == "active" && version.Status == "published" && (expertGroup.TenantId == 1 || expertGroup.TenantId == tenantId)
                      orderby version.Version descending
                      select new ResolvedSource(null, version.Id, version.EstimatedCredits)).FirstOrDefaultAsync(cancellationToken);
    }

    private void AddEvent(AgentRun run, int sequence, string eventType, string message, DateTime createdAt) =>
        _db.RunEvents.Add(new RunEvent { TenantId = run.TenantId, RunId = run.Id, Sequence = sequence, EventType = eventType, Payload = JsonSerializer.Serialize(new { message }), CreatedAt = createdAt });

    private async Task<int> NextSequenceAsync(long runId, CancellationToken cancellationToken) =>
        (await _db.RunEvents.Where(x => x.RunId == runId).MaxAsync(x => (int?)x.Sequence, cancellationToken) ?? 0) + 1;

    private static bool IsJson(string value) { try { JsonDocument.Parse(value); return true; } catch (JsonException) { return false; } }
    private static AgentRunView ToView(AgentRun run) => new(
        run.Id, run.SourceType, run.Status, run.Input, run.Result, run.ResultSummary,
        run.EstimatedCredits, run.ActualCredits, run.CreatedAt, run.StartedAt, run.FinishedAt, run.ConversationId);
    private sealed record ResolvedSource(long? ExpertVersionId, long? GroupVersionId, decimal EstimatedCredits);
}
