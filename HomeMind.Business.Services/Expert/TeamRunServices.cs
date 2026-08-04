using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HomeMind.Business.IServices.Expert;
using HomeMind.Business.Services.SmartHome;
using HomeMind.Common.Model.Entities;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.AI;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HomeMind.Business.Services.Expert;

/// <summary>版本化的多专家团队编排。客户端请求被冻结到模板版本；成员执行、取消、重试、合成均由父 Run 统一驱动。</summary>
public sealed class TeamRunServices : ITeamRunServices
{
    private const int CurrentTeamVersion = 1;

    private static readonly HashSet<string> AllowedModes = new(StringComparer.OrdinalIgnoreCase)
    {
        TeamRunMode.Sequential, TeamRunMode.Parallel, TeamRunMode.Synthesis
    };

    private static readonly HashSet<string> AllowedApprovals = new(StringComparer.OrdinalIgnoreCase)
    {
        "manual_confirmation", "auto_execute"
    };

    private readonly HomeMindDbContext _db;
    private readonly ILogger<TeamRunServices> _logger;

    public TeamRunServices(HomeMindDbContext db, ILogger<TeamRunServices> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<ServiceResult> CreateAsync(long userId, long tenantId, TeamRunCreateRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.TeamVersion) || !int.TryParse(request.TeamVersion, out var teamVersion) || teamVersion != CurrentTeamVersion)
        {
            return new ServiceResult(422, $"仅支持 teamVersion={CurrentTeamVersion} 的团队契约。");
        }
        if (string.IsNullOrWhiteSpace(request.Mode) || !AllowedModes.Contains(request.Mode))
        {
            return new ServiceResult(422, "团队模式必须为 sequential、parallel 或 synthesis。");
        }
        if (request.Members is null || request.Members.Count == 0)
        {
            return new ServiceResult(422, "团队至少需要一个成员 ExpertVersion 引用。");
        }
        if (request.Members.Count > 8) return new ServiceResult(422, "团队成员数量不能超过 8。");

        var parentRun = await _db.AgentRuns.SingleOrDefaultAsync(x => x.Id == request.ParentAgentRunId && x.TenantId == tenantId, cancellationToken);
        if (parentRun is null) return new ServiceResult(404, "父 AgentRun 不存在或不属于当前租户。");

        var versionIds = request.Members.Select(m => m.ExpertVersionId).Distinct().ToArray();
        var versions = await _db.ExpertVersions
            .Where(v => versionIds.Contains(v.Id) && v.TenantId == tenantId && v.Status == "published")
            .ToListAsync(cancellationToken);
        if (versions.Count != versionIds.Length) return new ServiceResult(404, "部分成员 ExpertVersion 不存在或未发布。");

        if (request.FileIds is { Count: > 0 })
        {
            var files = await _db.ExpertFiles
                .Where(f => request.FileIds.Contains(f.Id) && f.TenantId == tenantId && f.Status == ExpertFileStatus.Ready)
                .ToListAsync(cancellationToken);
            if (files.Count != request.FileIds.Count) return new ServiceResult(422, "文件引用必须为当前租户下已就绪的文件。");
        }

        var now = DateTime.UtcNow;
        var template = new TeamRunTemplate
        {
            TenantId = tenantId,
            OwnerUserId = userId,
            Name = $"team-{now:yyyyMMddHHmmss}",
            TeamVersion = teamVersion,
            Mode = request.Mode.ToLowerInvariant(),
            GraphJson = JsonSerializer.Serialize(BuildGraph(request.Mode, request.Members.Count)),
            ApprovalPolicy = "manual_confirmation",
            CreatedAt = now,
            UpdatedAt = now,
            RowVersion = 1,
            SyncVersion = 1
        };
        _db.TeamRunTemplates.Add(template);
        await _db.SaveChangesAsync(cancellationToken);

        var membersJson = JsonSerializer.Serialize(request.Members.Select(m => new
        {
            expertVersionId = m.ExpertVersionId,
            displayName = m.DisplayName,
            stageOrder = m.StageOrder
        }));
        var intersections = ComputePermissionIntersections(versions);
        var templateVersion = new TeamRunTemplateVersion
        {
            TeamRunTemplateId = template.Id,
            TenantId = tenantId,
            Version = 1,
            MembersJson = membersJson,
            FileRefsJson = JsonSerializer.Serialize(request.FileIds ?? Array.Empty<long>()),
            PermissionIntersectionsJson = JsonSerializer.Serialize(intersections),
            GraphJson = template.GraphJson,
            CreatedAt = now
        };
        _db.TeamRunTemplateVersions.Add(templateVersion);
        await _db.SaveChangesAsync(cancellationToken);

        var teamRun = new TeamRun
        {
            TenantId = tenantId,
            ParentAgentRunId = parentRun.Id,
            TeamRunTemplateId = template.Id,
            TeamRunTemplateVersionId = templateVersion.Id,
            TeamVersion = teamVersion,
            Mode = template.Mode,
            Status = TeamRunStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now,
            RowVersion = 1,
            SyncVersion = 1
        };
        _db.TeamRuns.Add(teamRun);
        await _db.SaveChangesAsync(cancellationToken);

        var stage = 0;
        foreach (var member in request.Members.OrderBy(m => m.StageOrder))
        {
            var version = versions.Single(v => v.Id == member.ExpertVersionId);
            var intersection = intersections[version.Id];
            _db.TeamRunMembers.Add(new TeamRunMember
            {
                TenantId = tenantId,
                TeamRunId = teamRun.Id,
                ExpertVersionId = version.Id,
                ChildAgentRunId = null,
                DisplayName = string.IsNullOrWhiteSpace(member.DisplayName) ? version.Persona : member.DisplayName.Trim(),
                StageOrder = member.StageOrder,
                PermissionIntersectionJson = JsonSerializer.Serialize(intersection),
                Status = TeamRunMemberStatus.Pending,
                CreatedAt = now,
                UpdatedAt = now
            });
            stage++;
        }
        teamRun.Status = TeamRunStatus.Running;
        teamRun.UpdatedAt = DateTime.UtcNow;
        teamRun.RowVersion += 1;
        _db.TeamRunAudits.Add(new TeamRunAudit
        {
            TenantId = tenantId,
            ActorUserId = userId,
            TeamRunId = teamRun.Id,
            Action = "team_run_create",
            Result = "success",
            PayloadJson = JsonSerializer.Serialize(new { teamVersion, mode = template.Mode, memberCount = request.Members.Count }),
            CreatedAt = now
        });
        await _db.SaveChangesAsync(cancellationToken);

        AutomationMetrics.TeamRunTriggered.Add(1);
        _logger.LogInformation("Team run {TeamRunId} created in tenant {TenantId} with {MemberCount} members", teamRun.Id, tenantId, request.Members.Count);
        return new ServiceResult(200, "团队运行已创建。", ToSummary(teamRun));
    }

    public async Task<ServiceResult> GetAsync(long userId, long tenantId, long teamRunId, CancellationToken cancellationToken = default)
    {
        var run = await _db.TeamRuns.SingleOrDefaultAsync(x => x.Id == teamRunId && x.TenantId == tenantId, cancellationToken);
        if (run is null) return new ServiceResult(404, "团队运行不存在。");
        return new ServiceResult(200, "操作成功", ToSummary(run));
    }

    public async Task<ServiceResult> ListEventsAsync(long userId, long tenantId, long teamRunId, CancellationToken cancellationToken = default)
    {
        var run = await _db.TeamRuns.SingleOrDefaultAsync(x => x.Id == teamRunId && x.TenantId == tenantId, cancellationToken);
        if (run is null) return new ServiceResult(404, "团队运行不存在。");
        var audits = await _db.TeamRunAudits
            .Where(a => a.TeamRunId == teamRunId && a.TenantId == tenantId)
            .OrderBy(a => a.CreatedAt)
            .Take(200)
            .ToListAsync(cancellationToken);
        var events = audits.Select(a => new TeamRunEvent(a.Id, a.Action, a.Result, a.CreatedAt)).ToArray();
        return new ServiceResult(200, "操作成功", events);
    }

    public async Task<ServiceResult> ListMembersAsync(long userId, long tenantId, long teamRunId, CancellationToken cancellationToken = default)
    {
        var run = await _db.TeamRuns.SingleOrDefaultAsync(x => x.Id == teamRunId && x.TenantId == tenantId, cancellationToken);
        if (run is null) return new ServiceResult(404, "团队运行不存在。");
        var members = await _db.TeamRunMembers
            .Where(m => m.TeamRunId == teamRunId && m.TenantId == tenantId)
            .OrderBy(m => m.StageOrder)
            .ToListAsync(cancellationToken);
        var views = members.Select(m => new TeamRunMemberSummary(
            m.Id, m.DisplayName, m.StageOrder, m.ExpertVersionId, m.ChildAgentRunId, m.Status, m.LastErrorCode,
            SummarizeIntersection(m.PermissionIntersectionJson))).ToArray();
        return new ServiceResult(200, "操作成功", views);
    }

    public async Task<ServiceResult> GetSynthesisAsync(long userId, long tenantId, long teamRunId, CancellationToken cancellationToken = default)
    {
        var run = await _db.TeamRuns.SingleOrDefaultAsync(x => x.Id == teamRunId && x.TenantId == tenantId, cancellationToken);
        if (run is null) return new ServiceResult(404, "团队运行不存在。");
        if (run.Status != TeamRunStatus.Completed) return new ServiceResult(409, "团队运行尚未完成。");
        if (string.IsNullOrEmpty(run.SynthesisResultJson)) return new ServiceResult(404, "团队运行尚未生成聚合结果。");
        var payload = JsonSerializer.Deserialize<TeamRunSynthesis>(run.SynthesisResultJson)
            ?? new TeamRunSynthesis(run.Id, run.Status, "无可用摘要。", Array.Empty<string>(), null);
        return new ServiceResult(200, "操作成功", payload);
    }

    public async Task<ServiceResult> CancelAsync(long userId, long tenantId, long teamRunId, CancellationToken cancellationToken = default)
    {
        var run = await _db.TeamRuns.SingleOrDefaultAsync(x => x.Id == teamRunId && x.TenantId == tenantId, cancellationToken);
        if (run is null) return new ServiceResult(404, "团队运行不存在。");
        if (run.Status is TeamRunStatus.Completed or TeamRunStatus.Cancelled or TeamRunStatus.Failed)
        {
            return new ServiceResult(409, "当前状态不可取消。");
        }
        run.Status = TeamRunStatus.Cancelled;
        run.UpdatedAt = DateTime.UtcNow;
        run.RowVersion += 1;
        var members = await _db.TeamRunMembers.Where(m => m.TeamRunId == teamRunId).ToListAsync(cancellationToken);
        foreach (var member in members)
        {
            if (member.Status is TeamRunMemberStatus.Pending or TeamRunMemberStatus.Running)
            {
                member.Status = TeamRunMemberStatus.Cancelled;
                member.UpdatedAt = run.UpdatedAt;
            }
        }
        _db.TeamRunAudits.Add(new TeamRunAudit
        {
            TenantId = tenantId,
            ActorUserId = userId,
            TeamRunId = run.Id,
            Action = "team_run_cancel",
            Result = "success",
            CreatedAt = run.UpdatedAt
        });
        await _db.SaveChangesAsync(cancellationToken);
        return new ServiceResult(200, "团队运行已取消。", ToSummary(run));
    }

    public async Task<ServiceResult> RetryAsync(long userId, long tenantId, long teamRunId, CancellationToken cancellationToken = default)
    {
        var run = await _db.TeamRuns.SingleOrDefaultAsync(x => x.Id == teamRunId && x.TenantId == tenantId, cancellationToken);
        if (run is null) return new ServiceResult(404, "团队运行不存在。");
        if (run.Status is TeamRunStatus.Running or TeamRunStatus.Pending)
        {
            return new ServiceResult(409, "当前状态不可重试。");
        }
        run.Status = TeamRunStatus.Pending;
        run.LastErrorCode = null;
        run.UpdatedAt = DateTime.UtcNow;
        run.RowVersion += 1;
        var members = await _db.TeamRunMembers.Where(m => m.TeamRunId == teamRunId).ToListAsync(cancellationToken);
        foreach (var member in members)
        {
            if (member.Status is TeamRunMemberStatus.Failed or TeamRunMemberStatus.Cancelled or TeamRunMemberStatus.Skipped)
            {
                member.Status = TeamRunMemberStatus.Pending;
                member.LastErrorCode = null;
                member.UpdatedAt = run.UpdatedAt;
            }
        }
        _db.TeamRunAudits.Add(new TeamRunAudit
        {
            TenantId = tenantId,
            ActorUserId = userId,
            TeamRunId = run.Id,
            Action = "team_run_retry",
            Result = "success",
            CreatedAt = run.UpdatedAt
        });
        await _db.SaveChangesAsync(cancellationToken);
        AutomationMetrics.TeamRunTriggered.Add(1);
        return new ServiceResult(200, "团队运行已重新入队。", ToSummary(run));
    }

    private static Dictionary<long, Dictionary<string, object>> ComputePermissionIntersections(IReadOnlyCollection<ExpertVersion> versions)
    {
        var result = new Dictionary<long, Dictionary<string, object>>();
        foreach (var version in versions)
        {
            var toolPolicy = string.IsNullOrWhiteSpace(version.ToolPolicy) ? "{}" : version.ToolPolicy;
            result[version.Id] = new Dictionary<string, object>
            {
                ["teamVersion"] = 1,
                ["scopes"] = new[] { "ai.read", "ai.run" },
                ["tools"] = JsonSerializer.Deserialize<JsonElement>(toolPolicy),
                ["source"] = "expert_version"
            };
        }
        return result;
    }

    private static object BuildGraph(string mode, int memberCount) => new
    {
        mode = mode.ToLowerInvariant(),
        stages = Enumerable.Range(0, memberCount).Select(i => new { order = i, kind = "member" }).ToArray()
    };

    private static string SummarizeIntersection(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("scopes", out var scopes) && scopes.ValueKind == JsonValueKind.Array)
            {
                return string.Join(",", scopes.EnumerateArray().Select(s => s.GetString()));
            }
        }
        catch (Exception) { }
        return "ai.read,ai.run";
    }

    private static TeamRunSummary ToSummary(TeamRun run) => new(
        run.Id, run.Status, run.Mode, run.TeamVersion.ToString(),
        run.ParentAgentRunId, run.CreatedAt, run.UpdatedAt, run.RowVersion);
}
