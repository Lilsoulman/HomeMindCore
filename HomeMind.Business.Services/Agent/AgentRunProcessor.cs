using System.Text.Json;
using System.Text.Json.Nodes;
using HomeMind.Business.IServices.Agent;
using HomeMind.Business.IServices.AI;
using HomeMind.Business.IServices.Expert;
using HomeMind.Common.Infrastructure;
using HomeMind.Common.Model.Agent;
using HomeMind.Common.Model.Entities;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;

namespace HomeMind.Business.Services.Agent;

/// <summary>
/// AgentRun 作业处理器：单作业串行消费 queued ExpertJob，组装专家提示词，调用 LLM，
/// 提取并校验 JSON 输出，回写运行状态、结果与事件。所有回写合并为一次 SaveChanges。
/// </summary>
public sealed class AgentRunProcessor : IAgentRunProcessor
{
    private const string SkillPrefix = "skill:";
    private const int MaxSummaryLength = 200;
    private const string PptExpertCode = "ppt-expert";
    private const string PptxMimeType = "application/vnd.openxmlformats-officedocument.presentationml.presentation";

    private readonly HomeMindDbContext _db;
    private readonly ILLMClient _llm;
    private readonly SecretProtector _secretProtector;
    private readonly IPptxBuilder _pptx;
    private readonly IExpertFileServices _files;

    public AgentRunProcessor(HomeMindDbContext db, ILLMClient llm, SecretProtector secretProtector, IPptxBuilder pptx, IExpertFileServices files)
    {
        _db = db;
        _llm = llm;
        _secretProtector = secretProtector;
        _pptx = pptx;
        _files = files;
    }

    public async Task<int> ProcessNextAsync(CancellationToken cancellationToken = default)
    {
        var job = await _db.ExpertJobs
            .Where(j => j.Status == AgentRunStatus.Queued)
            .OrderBy(j => j.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (job is null) return 0;

        var run = await _db.AgentRuns.SingleOrDefaultAsync(r => r.Id == job.RunId && r.TenantId == job.TenantId, cancellationToken);
        if (run is null || AgentRunStatus.IsTerminal(run.Status))
        {
            job.Status = AgentRunStatus.Completed;
            await _db.SaveChangesAsync(cancellationToken);
            return 1;
        }
        if (run.CancelRequestedAt is not null)
        {
            run.Status = AgentRunStatus.Cancelled;
            run.FinishedAt = DateTime.UtcNow;
            job.Status = AgentRunStatus.Completed;
            AddEvent(run, AgentRunStatus.Cancelled, "Agent 任务已取消。", DateTime.UtcNow);
            await _db.SaveChangesAsync(cancellationToken);
            return 1;
        }

        // 中间提交一次，让 planning 状态对外可见，再执行可能耗时较长的模型调用。
        job.Status = AgentRunStatus.Running;
        run.Status = AgentRunStatus.Planning;
        run.StartedAt = DateTime.UtcNow;
        AddEvent(run, AgentRunStatus.Planning, "正在分析你的需求…", DateTime.UtcNow);
        await _db.SaveChangesAsync(cancellationToken);

        try
        {
            if (run.ExpertVersionId is null)
                return await FailAsync(run.Id, job.Id, LlmErrorCodes.HttpError, "专家组运行暂不支持。", cancellationToken);

            var source = await (from version in _db.ExpertVersions
                                join expert in _db.Experts on version.ExpertId equals expert.Id
                                where version.Id == run.ExpertVersionId
                                select new { version, expert }).FirstOrDefaultAsync(cancellationToken);
            if (source is null)
                return await FailAsync(run.Id, job.Id, LlmErrorCodes.HttpError, "专家版本不存在。", cancellationToken);

            var systemPrompt = await BuildSystemPromptAsync(source.version, cancellationToken);
            var config = await _db.AiConfigs.FindAsync(new object?[] { run.UserId }, cancellationToken);
            if (config is null || config.ApiKeyEncrypted.Length == 0)
                return await FailAsync(run.Id, job.Id, LlmErrorCodes.AiConfigMissing, "尚未配置 AI 服务，请在设置中填写 API 地址与密钥。", cancellationToken);

            var completion = await _llm.CompleteAsync(new LlmRequest(
                config.Endpoint, config.Model, _secretProtector.Decrypt(config.ApiKeyEncrypted), config.Temperature,
                systemPrompt, run.Input), cancellationToken);
            if (!completion.Success)
                return await FailAsync(run.Id, job.Id, completion.ErrorCode ?? LlmErrorCodes.HttpError, completion.ErrorMessage ?? "模型调用失败。", cancellationToken);

            var json = TryExtractJson(completion.Content);
            if (json is null)
                return await FailAsync(run.Id, job.Id, LlmErrorCodes.EmptyResponse, "模型输出格式无效，请重试。", cancellationToken);

            var finalJson = await MaybeGeneratePptxAsync(source.expert, json, run, cancellationToken);
            return await SucceedAsync(run.Id, job.Id, finalJson, completion.Content, cancellationToken);
        }
        catch (Exception error)
        {
            return await FailAsync(run.Id, job.Id, LlmErrorCodes.HttpError, $"生成过程发生异常：{error.Message}", cancellationToken);
        }
    }

    /// <summary>PPT 专家专用后处理：按 LLM 输出的逐页 JSON 生成 .pptx 并登记为 Expert File；失败不阻塞运行终态。</summary>
    private async Task<string> MaybeGeneratePptxAsync(HomeMind.Common.Model.Entities.Expert expert, string json, AgentRun run, CancellationToken cancellationToken)
    {
        if (expert.Code != PptExpertCode) return json;
        JsonNode? node;
        try { node = JsonNode.Parse(json); } catch (JsonException) { return json; }
        if (node is null || node["slides"] is not JsonArray slides || slides.Count == 0) return json;

        var title = node["title"]?.GetValue<string>() ?? "演示文稿";
        var subtitle = node["subtitle"]?.GetValue<string>() ?? "";
        try
        {
            var slideList = slides
                .OfType<JsonObject>()
                .Select(s => new PptSlide(
                    s["title"]?.GetValue<string>() ?? "",
                    s["bullets"] is JsonArray bullets ? bullets.Select(b => b?.GetValue<string>() ?? "").ToList() : new List<string>()))
                .ToList();
            var result = await _files.RegisterGeneratedFileAsync(run.UserId, run.TenantId, $"{title}.pptx", PptxMimeType, _pptx.Build(title, subtitle, slideList), run.Id, cancellationToken);
            if (result.Succeeded && JsonSerializer.SerializeToNode(result.Data) is { } data && data["fileId"] is not null)
                node["generatedFileId"] = data["fileId"];
            else
                node["generatedFileError"] = result.Message;
        }
        catch (Exception error)
        {
            node["generatedFileError"] = $"PPT 生成失败：{error.Message}";
        }
        return node.ToJsonString();
    }

    /// <summary>回写失败终态；若运行已被并发取消，则仅结束作业。</summary>
    private async Task<int> FailAsync(long runId, long jobId, string errorCode, string message, CancellationToken cancellationToken)
    {
        _db.ChangeTracker.Clear();
        var run = await _db.AgentRuns.FindAsync(new object?[] { runId }, cancellationToken);
        var job = await _db.ExpertJobs.FindAsync(new object?[] { jobId }, cancellationToken);
        if (run is null || job is null) return 1;
        job.Status = AgentRunStatus.Completed;
        if (!AgentRunStatus.IsTerminal(run.Status))
        {
            run.Status = AgentRunStatus.Failed;
            run.ResultSummary = message;
            run.FinishedAt = DateTime.UtcNow;
            AddEvent(run, AgentRunStatus.Failed, message, DateTime.UtcNow);
        }
        await _db.SaveChangesAsync(cancellationToken);
        return 1;
    }

    /// <summary>回写成功终态；若运行已被并发取消，则仅结束作业。</summary>
    private async Task<int> SucceedAsync(long runId, long jobId, string json, string rawContent, CancellationToken cancellationToken)
    {
        _db.ChangeTracker.Clear();
        var run = await _db.AgentRuns.FindAsync(new object?[] { runId }, cancellationToken);
        var job = await _db.ExpertJobs.FindAsync(new object?[] { jobId }, cancellationToken);
        if (run is null || job is null) return 1;
        job.Status = AgentRunStatus.Completed;
        if (AgentRunStatus.IsTerminal(run.Status)) { await _db.SaveChangesAsync(cancellationToken); return 1; }
        run.Status = AgentRunStatus.Completed;
        run.Result = json;
        run.ResultSummary = ExtractSummary(json, rawContent);
        run.ActualCredits = run.EstimatedCredits;
        run.FinishedAt = DateTime.UtcNow;
        AddEvent(run, AgentRunStatus.Completed, "建议已生成。", DateTime.UtcNow);
        await _db.SaveChangesAsync(cancellationToken);
        return 1;
    }

    private async Task<string> BuildSystemPromptAsync(ExpertVersion version, CancellationToken cancellationToken)
    {
        var prompt = $"{version.Persona}\n{version.Methodology}\n{version.PromptTemplate}";
        var skillIds = ExtractSkillIds(version.ToolPolicy);
        if (skillIds.Count > 0)
        {
            var skills = await _db.AiSkills
                .Where(s => skillIds.Contains(s.Id) && s.IsActive && s.DeletedAt == null)
                .ToListAsync(cancellationToken);
            foreach (var skill in skills) prompt += $"\n\n参考技能 [{skill.Name}]：\n{skill.Prompt}";
        }
        return prompt;
    }

    private static List<long> ExtractSkillIds(string? toolPolicy)
    {
        var result = new List<long>();
        if (string.IsNullOrWhiteSpace(toolPolicy)) return result;
        try
        {
            using var document = JsonDocument.Parse(toolPolicy);
            var root = document.RootElement;
            var skills = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("skills", out var nested) && nested.ValueKind == JsonValueKind.Array
                ? nested
                : root.ValueKind == JsonValueKind.Array ? root : default;
            if (skills.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in skills.EnumerateArray())
                {
                    var value = item.ValueKind == JsonValueKind.String ? item.GetString() : null;
                    if (value is not null && value.StartsWith(SkillPrefix, StringComparison.OrdinalIgnoreCase)
                        && long.TryParse(value[SkillPrefix.Length..], out var id)) result.Add(id);
                }
            }
        }
        catch (JsonException) { }
        return result;
    }

    /// <summary>从模型输出中提取首个 JSON 对象文本，容忍 markdown 代码块包裹。</summary>
    private static string? TryExtractJson(string content)
    {
        var start = content.IndexOf('{');
        var end = content.LastIndexOf('}');
        if (start < 0 || end <= start) return null;
        var candidate = content[start..(end + 1)];
        try { JsonDocument.Parse(candidate); return candidate; }
        catch (JsonException) { return null; }
    }

    private static string ExtractSummary(string json, string fallback)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.TryGetProperty("summary", out var summary)
                && summary.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(summary.GetString()))
                return summary.GetString()!;
        }
        catch (JsonException) { }
        var trimmed = fallback.Trim();
        return trimmed.Length <= MaxSummaryLength ? trimmed : trimmed[..MaxSummaryLength] + "…";
    }

    private void AddEvent(AgentRun run, string eventType, string message, DateTime createdAt)
    {
        var sequence = _db.RunEvents.Any(x => x.RunId == run.Id)
            ? _db.RunEvents.Where(x => x.RunId == run.Id).Max(x => x.Sequence) + 1
            : 1;
        _db.RunEvents.Add(new RunEvent
        {
            TenantId = run.TenantId,
            RunId = run.Id,
            Sequence = sequence,
            EventType = eventType,
            Payload = JsonSerializer.Serialize(new { message }),
            CreatedAt = createdAt
        });
    }
}
