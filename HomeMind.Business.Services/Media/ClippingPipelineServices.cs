using System.Diagnostics;
using System.Text.Json;
using HomeMind.Business.IServices.Expert;
using HomeMind.Business.IServices.Family;
using HomeMind.Common.Model.Entities.Family;
using HomeMind.Business.IServices.Media;
using HomeMind.Common.Model.Entities;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HomeMind.Business.Services.Media;

/// <summary>剪辑任务四引擎后台调度实现；所有公开事件仅记录展示安全的阶段状态。</summary>
public sealed class ClippingPipelineServices : IClippingPipelineServices
{
    private static readonly string[] Stages = ["video_use", "seedance", "hyperframes", "remotion", "draft"];
    private readonly HomeMindDbContext _db;
    private readonly IReadOnlyDictionary<string, IClippingEngine> _engines;
    private readonly IConfiguration _configuration;
    private readonly IClippingRenderService _render;
    private readonly IExpertFileServices? _files;
    private readonly IFamilyAuditLogger? _audit;

    /// <summary>构造剪辑流水线调度服务。</summary>
    public ClippingPipelineServices(HomeMindDbContext db, IEnumerable<IClippingEngine> engines, IConfiguration configuration, IClippingRenderService render, IExpertFileServices? files = null, IFamilyAuditLogger? audit = null)
    {
        _db = db;
        _engines = engines.ToDictionary(x => x.Stage, StringComparer.Ordinal);
        _configuration = configuration;
        _render = render;
        _files = files;
        _audit = audit;
    }

    /// <inheritdoc />
    public async Task<ServiceResult> QueueAsync(long taskId, long tenantId, string startStage, bool allowSeedance, bool costConfirmed, CancellationToken cancellationToken = default)
    {
        if (!Stages.Contains(startStage, StringComparer.Ordinal)) return new ServiceResult(422, "重做范围无效。");
        var task = await _db.ClippingTasks.SingleOrDefaultAsync(x => x.Id == taskId && x.TenantId == tenantId && x.DeletedAt == null, cancellationToken);
        if (task is null) return new ServiceResult(404, "请求的剪辑任务不存在。");
        task.Status = ClippingTaskStatus.Generating;
        task.EngineStage = startStage;
        task.CurrentPlan = WithEngineAuthorization(task.CurrentPlan, allowSeedance, costConfirmed);
        task.UpdatedAt = DateTime.UtcNow;
        await AddEventAsync(task, startStage, "queued", "剪辑引擎任务已排队。", cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return new ServiceResult(202, "剪辑引擎任务已排队。");
    }

    /// <inheritdoc />
    public async Task<int> ProcessNextAsync(CancellationToken cancellationToken = default)
    {
        var task = await _db.ClippingTasks.OrderBy(x => x.UpdatedAt).FirstOrDefaultAsync(x => (x.Status == ClippingTaskStatus.Generating || x.Status == ClippingTaskStatus.Rendering) && x.DeletedAt == null, cancellationToken);
        if (task is null || task.RunId is null) return 0;
        if (task.Status == ClippingTaskStatus.Rendering) return await RenderAsync(task, cancellationToken);
        var startIndex = Array.IndexOf(Stages, task.EngineStage ?? "video_use");
        if (startIndex < 0) startIndex = 0;
        for (var index = startIndex; index < Stages.Length; index++)
        {
            var stage = Stages[index];
            task.EngineStage = stage;
            if (stage == "draft")
            {
                await AddEventAsync(task, stage, "queued", "草稿生成等待用户确认。", cancellationToken);
                task.Status = ClippingTaskStatus.Reviewing;
                task.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(cancellationToken);
                return 1;
            }
            if (stage == "seedance" && !CanUseSeedance(task))
            {
                await AddEventAsync(task, stage, "skipped", "生成式补充未获授权或未启用，已跳过。", cancellationToken);
                continue;
            }
            if (!_engines.TryGetValue(stage, out var engine)) return await FailAsync(task, stage, "剪辑引擎未配置。", cancellationToken);
            var health = await engine.CheckHealthAsync(cancellationToken);
            if (!health.Succeeded) return await FailAsync(task, stage, health.Message, cancellationToken);
            await AddEventAsync(task, stage, "running", "剪辑引擎正在处理。", cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            var result = await engine.ExecuteAsync(cancellationToken);
            if (!result.Succeeded) return await FailAsync(task, stage, result.Message, cancellationToken);
            await AddEventAsync(task, stage, "succeeded", "剪辑引擎处理完成。", cancellationToken);
        }
        return 0;
    }

    /// <summary>执行已确认方案的粗剪渲染，登记 mp4 后才将动作、运行和任务统一标为完成。</summary>
    private async Task<int> RenderAsync(ClippingTask task, CancellationToken cancellationToken)
    {
        task.EngineStage = "render";
        await AddEventAsync(task, "render", "running", "粗剪视频正在渲染。", cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        var render = await _render.RenderAsync(task.CurrentPlan ?? string.Empty, cancellationToken);
        long? fileId = null;
        long sizeBytes = 0;
        if (render.Succeeded && render.Content is { Length: > 0 } && !string.IsNullOrWhiteSpace(render.FileName) && _files is not null)
        {
            var registered = await _files.RegisterGeneratedFileAsync(task.CreatedByUserId, task.TenantId, render.FileName, "video/mp4", render.Content, task.RunId, cancellationToken);
            if (registered.Succeeded)
            {
                using var document = JsonDocument.Parse(JsonSerializer.Serialize(registered.Data));
                if (document.RootElement.TryGetProperty("fileId", out var id) && id.TryGetInt64(out var parsedId)) fileId = parsedId;
                if (document.RootElement.TryGetProperty("SizeBytes", out var size) && size.TryGetInt64(out var parsedSize)) sizeBytes = parsedSize;
            }
            else render = new ClippingRenderResult(false, "粗剪视频登记失败，请稍后重试。");
        }

        var action = await _db.ExpertRunActions.SingleOrDefaultAsync(x => x.RunId == task.RunId && x.TenantId == task.TenantId && x.ActionType == "draft_generate", cancellationToken);
        var audit = action is null ? null : await _db.ActionExecutionAudits.OrderByDescending(x => x.Id).FirstOrDefaultAsync(x => x.RunActionId == action.Id && x.Status == "executing", cancellationToken);
        var run = await _db.AgentRuns.SingleAsync(x => x.Id == task.RunId && x.TenantId == task.TenantId, cancellationToken);
        var now = DateTime.UtcNow;
        var succeeded = fileId is not null;
        task.Status = succeeded ? ClippingTaskStatus.Done : ClippingTaskStatus.Failed;
        task.UpdatedAt = now;
        if (action is not null)
        {
            action.Status = succeeded ? "executed" : "failed";
            action.Result = JsonSerializer.Serialize(succeeded
                ? (object)new { status = action.Status, mp4_file_id = fileId, size_bytes = sizeBytes }
                : new { status = action.Status, error_code = "render_failed" });
            action.UpdatedAt = now;
        }
        if (audit is not null) { audit.Status = succeeded ? "executed" : "failed"; audit.Result = action?.Result; audit.UpdatedAt = now; }
        run.Status = succeeded ? "completed" : "failed";
        run.FinishedAt = now;
        run.ResultSummary = succeeded ? "粗剪视频已生成，可预览或下载。" : render.Message;
        run.Result = JsonSerializer.Serialize(succeeded
            ? (object)new { skill_run = "quick_edit", status = run.Status, mp4_file_id = fileId, size_bytes = sizeBytes }
            : new { skill_run = "quick_edit", status = run.Status, error_code = "render_failed" });
        await AddEventAsync(task, "render", succeeded ? "succeeded" : "failed", run.ResultSummary, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        if (succeeded && _audit is not null)
            await _audit.LogAsync(task.TenantId, task.CreatedByUserId, FamilyAuditActions.SkillDraftRegistered, FamilyAuditTargetTypes.SkillDraft,
                fileId, null, new { file_id = fileId, file_name = render.FileName, size_bytes = sizeBytes }, null, run.Id, cancellationToken);
        return 1;
    }

    /// <summary>判断 Seedance 的全局开关、用户授权、成本确认与安全密钥四重门禁。</summary>
    private bool CanUseSeedance(ClippingTask task)
    {
        var options = _configuration.GetSection("Clipping:Engines:Seedance").Get<ClippingEngineOptions>() ?? new ClippingEngineOptions();
        if (string.IsNullOrWhiteSpace(task.CurrentPlan)) return false;
        try
        {
            using var document = JsonDocument.Parse(task.CurrentPlan);
            return options.Enabled && !string.IsNullOrWhiteSpace(options.ApiKey)
                && document.RootElement.TryGetProperty("allow_seedance", out var allowed) && allowed.GetBoolean()
                && document.RootElement.TryGetProperty("cost_confirmed", out var confirmed) && confirmed.GetBoolean();
        }
        catch (JsonException) { return false; }
    }

    /// <summary>写入失败事件并将任务置为失败，绝不追加成功事件。</summary>
    private async Task<int> FailAsync(ClippingTask task, string stage, string message, CancellationToken cancellationToken)
    {
        task.Status = ClippingTaskStatus.Failed;
        task.EngineStage = stage;
        task.UpdatedAt = DateTime.UtcNow;
        await AddEventAsync(task, stage, "failed", message, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return 1;
    }

    /// <summary>将阶段状态序列化到既有 RunEvents，负载不含内部命令、路径或凭据。</summary>
    private async Task AddEventAsync(ClippingTask task, string stage, string status, string message, CancellationToken cancellationToken)
    {
        var sequence = (await _db.RunEvents.Where(x => x.RunId == task.RunId).MaxAsync(x => (int?)x.Sequence, cancellationToken) ?? 0) + 1;
        _db.RunEvents.Add(new RunEvent { TenantId = task.TenantId, RunId = task.RunId!.Value, Sequence = sequence, EventType = "engine_stage", Payload = JsonSerializer.Serialize(new { stage, status, message, occurredAt = DateTime.UtcNow }), CreatedAt = DateTime.UtcNow });
    }

    /// <summary>在展示安全方案中保存逐任务的 Seedance 授权和成本确认，不保存密钥。</summary>
    private static string WithEngineAuthorization(string? currentPlan, bool allowSeedance, bool costConfirmed)
    {
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(currentPlan) ? "{}" : currentPlan);
        var plan = document.RootElement.EnumerateObject().ToDictionary(x => x.Name, x => x.Value.Clone());
        plan["allow_seedance"] = JsonSerializer.SerializeToElement(allowSeedance);
        plan["cost_confirmed"] = JsonSerializer.SerializeToElement(costConfirmed);
        return JsonSerializer.Serialize(plan);
    }
}

/// <summary>通过白名单配置启动本地进程的剪辑引擎；不提供 Mock 成功回退。</summary>
public sealed class ConfiguredClippingEngine : IClippingEngine
{
    private readonly ClippingEngineOptions _options;
    /// <summary>公开阶段标识。</summary>
    public string Stage { get; }
    /// <summary>构造一个指定阶段的受控引擎。</summary>
    public ConfiguredClippingEngine(string stage, ClippingEngineOptions options) { Stage = stage; _options = options; }
    /// <inheritdoc />
    public async Task<ClippingEngineResult> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.CommandFileName)) return new(false, "剪辑引擎未配置或未启用。");
        if (string.IsNullOrWhiteSpace(_options.HealthCheckArguments)) return new(true, "健康检查通过。");
        return await RunAsync(_options.HealthCheckArguments, cancellationToken);
    }
    /// <inheritdoc />
    public Task<ClippingEngineResult> ExecuteAsync(CancellationToken cancellationToken = default) => RunAsync(_options.Arguments, cancellationToken);
    /// <summary>执行无 shell 的受控进程并仅返回脱敏状态。</summary>
    private async Task<ClippingEngineResult> RunAsync(string arguments, CancellationToken cancellationToken)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo { FileName = _options.CommandFileName, Arguments = arguments, WorkingDirectory = _options.WorkingDirectory ?? "", UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true, CreateNoWindow = true });
            if (process is null) return new(false, "剪辑引擎无法启动。");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _options.TimeoutSeconds)));
            await Task.WhenAll(process.StandardOutput.ReadToEndAsync(timeout.Token), process.StandardError.ReadToEndAsync(timeout.Token), process.WaitForExitAsync(timeout.Token));
            return process.ExitCode == 0 ? new(true, "剪辑引擎处理完成。") : new(false, "剪辑引擎处理失败。");
        }
        catch (OperationCanceledException) { return new(false, "剪辑引擎处理超时或被取消。"); }
        catch { return new(false, "剪辑引擎无法启动或健康检查失败。"); }
    }
}
