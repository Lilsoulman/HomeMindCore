using System.Text.Json;
using HomeMind.Business.IServices.AI;
using HomeMind.Business.IServices.Family;
using HomeMind.Common.Model.Entities;
using HomeMind.Common.Model.Entities.Family;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.AI;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;

namespace HomeMind.Business.Services.AI;

/// <summary>思维导图 Skill 运行实现：服务端仅保存 markdown 与摘要，导图转换完全由浏览器负责。</summary>
public sealed class MindmapRunServices : IMindmapRunServices
{
    private const int MaxMarkdownLength = 100000;
    private readonly HomeMindDbContext _db;
    private readonly IFamilyAuditLogger _audit;

    /// <summary>构造思维导图 Skill 运行服务。</summary>
    /// <param name="db">数据库上下文。</param>
    /// <param name="audit">家庭域审计日志写入器。</param>
    public MindmapRunServices(HomeMindDbContext db, IFamilyAuditLogger audit)
    {
        _db = db;
        _audit = audit;
    }

    /// <inheritdoc />
    public async Task<ServiceResult> CreateAsync(long userId, long tenantId, MindmapRunCreateRequest request, CancellationToken cancellationToken = default)
    {
        var markdown = request.Markdown?.Trim();
        if (string.IsNullOrWhiteSpace(markdown) || markdown.Length > MaxMarkdownLength)
            return new ServiceResult(422, "markdown 为必填项且长度不得超过 100000 个字符。");

        var skill = await _db.SkillCatalogs.SingleOrDefaultAsync(x =>
            x.TenantId == 1 && x.Key == "mindmap" && x.Status == SkillCatalogStatus.Active && x.DeletedAt == null, cancellationToken);
        if (skill is null) return new ServiceResult(422, "未知或未启用的 Skill。");

        var idempotencyKey = Guid.TryParse(request.IdempotencyKey, out var parsed) ? parsed.ToString() : Guid.NewGuid().ToString();
        var existing = await _db.AgentRuns.SingleOrDefaultAsync(x =>
            x.TenantId == tenantId && x.UserId == userId && x.RequestIdempotencyKey == idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            if (!IsMindmapRun(existing)) return new ServiceResult(409, "该幂等键已用于其他运行类型。");
            return new ServiceResult(200, "思维导图运行已存在。", ToView(existing));
        }

        var now = DateTime.UtcNow;
        var title = ExtractFirstHeading(markdown);
        var summary = BuildSummary(markdown.Length, title);
        var run = new AgentRun
        {
            TenantId = tenantId,
            UserId = userId,
            SourceType = "skill",
            RequestIdempotencyKey = idempotencyKey,
            Input = JsonSerializer.Serialize(new { markdown }),
            Status = "completed",
            Mode = "single",
            AutoConfirmPolicy = "never",
            PermissionSnapshot = JsonSerializer.Serialize(new { skill = skill.Key }),
            Result = JsonSerializer.Serialize(new { skill = skill.Key, character_count = markdown.Length, first_heading = title }),
            ResultSummary = summary,
            EstimatedCredits = 0,
            ActualCredits = 0,
            CreatedAt = now,
            StartedAt = now,
            FinishedAt = now
        };
        _db.AgentRuns.Add(run);
        await _db.SaveChangesAsync(cancellationToken);

        _db.RunEvents.Add(new RunEvent
        {
            TenantId = tenantId,
            RunId = run.Id,
            Sequence = 1,
            EventType = "completed",
            Payload = JsonSerializer.Serialize(new { message = summary }),
            CreatedAt = now
        });
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync(tenantId, userId, FamilyAuditActions.SkillRunCreated, FamilyAuditTargetTypes.SkillRun,
            run.Id, null, new { skill = skill.Key, character_count = markdown.Length, first_heading = title }, null, run.Id, cancellationToken);
        return new ServiceResult(201, summary, ToView(run));
    }

    /// <inheritdoc />
    public async Task<ServiceResult> GetAsync(long userId, long tenantId, long runId, CancellationToken cancellationToken = default)
    {
        var run = await _db.AgentRuns.SingleOrDefaultAsync(x =>
            x.Id == runId && x.TenantId == tenantId && x.UserId == userId && x.SourceType == "skill", cancellationToken);
        if (run is null || !IsMindmapRun(run)) return new ServiceResult(404, "请求的思维导图运行不存在。");
        return new ServiceResult(200, "查询成功。", ToView(run));
    }

    private static bool IsMindmapRun(AgentRun run)
    {
        try
        {
            using var document = JsonDocument.Parse(run.Result ?? "{}");
            return document.RootElement.TryGetProperty("skill", out var skill) && skill.GetString() == "mindmap";
        }
        catch (JsonException) { return false; }
    }

    private static MindmapRunView ToView(AgentRun run)
    {
        using var document = JsonDocument.Parse(run.Result ?? "{}");
        var root = document.RootElement;
        var count = root.TryGetProperty("character_count", out var countElement) && countElement.TryGetInt32(out var parsedCount) ? parsedCount : 0;
        var heading = root.TryGetProperty("first_heading", out var headingElement) ? headingElement.GetString() : null;
        return new MindmapRunView(run.Id, run.Status, count, heading, run.ResultSummary ?? "", run.CreatedAt, run.FinishedAt);
    }

    private static string? ExtractFirstHeading(string markdown)
    {
        foreach (var line in markdown.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
        {
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("# ", StringComparison.Ordinal)) return trimmed[2..].Trim();
        }
        return null;
    }

    private static string BuildSummary(int characterCount, string? firstHeading) =>
        firstHeading is null ? $"已记录 {characterCount} 个字符的思维导图内容。" : $"已记录 {characterCount} 个字符的思维导图内容，一级标题：{firstHeading}。";
}
