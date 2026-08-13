using System.Text.Json;
using HomeMind.Business.IServices.AI;
using HomeMind.Business.IServices.Connector;
using HomeMind.Business.IServices.Family;
using HomeMind.Common.Model.Entities;
using HomeMind.Common.Model.Entities.Family;
using HomeMind.Common.Model.Entities.SmartHome;
using HomeMind.Common.Model.Entities.Steward;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.Connectors;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;

namespace HomeMind.Business.Services.Connectors;

/// <summary>
/// 小红书（xhs）笔记发布服务：创建 L2 发布动作（SourceType=xhs 的 AgentRun + 单个
/// <c>xhs_publish</c> ExpertRunAction，经确认中心逐项确认）与确认执行（UUID 幂等键、
/// ActionExecutionAudits 重放、权限快照复验，确认后经本地 MCP 发布并写 <c>xhs_note_published</c>
/// 审计）。复用既有 AgentRun/确认/幂等/审计边界，不新建运行时；响应不含凭据与 MCP 内部路径。
/// </summary>
public sealed class XhsPublishServices : IXhsPublishServices
{
    private const string XhsProviderCode = "xhs";
    private const string ImageType = "image";
    private const string VideoType = "video";
    private const int MaxTitleLength = 20;
    private const int MaxContentLength = 1000;
    private const int MaxImageCount = 18;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HomeMindDbContext _db;
    private readonly IFamilyAuditLogger _audit;
    private readonly IXhsMcpClient _xhs;

    /// <summary>构造小红书发布服务。</summary>
    /// <param name="db">数据库上下文。</param>
    /// <param name="audit">家庭域审计日志写入器，发布完成审计使用。</param>
    /// <param name="xhs">小红书 MCP 客户端，确认后执行发布。</param>
    public XhsPublishServices(HomeMindDbContext db, IFamilyAuditLogger audit, IXhsMcpClient xhs)
    {
        _db = db;
        _audit = audit;
        _xhs = xhs;
    }

    /// <inheritdoc />
    public async Task<ServiceResult> CreateAsync(long userId, long tenantId, XhsPublishRequest request, CancellationToken cancellationToken = default)
    {
        var validation = Validate(request);
        if (validation is not null) return validation;
        if (!await IsAuthorizedAsync(userId, tenantId, cancellationToken))
            return new ServiceResult(404, "小红书连接器未授权或不可用。");

        var idempotencyKey = Guid.TryParse(request.IdempotencyKey, out var parsedKey) ? parsedKey.ToString() : Guid.NewGuid().ToString();
        var existing = await _db.AgentRuns.SingleOrDefaultAsync(
            x => x.TenantId == tenantId && x.UserId == userId && x.RequestIdempotencyKey == idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            if (existing.SourceType != "xhs") return new ServiceResult(409, "该幂等键已用于其他运行类型。");
            return new ServiceResult(200, "发布动作已存在。", await GetActionViewAsync(existing.Id, cancellationToken));
        }

        var now = DateTime.UtcNow;
        var run = new AgentRun
        {
            TenantId = tenantId,
            UserId = userId,
            SourceType = "xhs",
            ExpertVersionId = null,
            RequestIdempotencyKey = idempotencyKey,
            Input = JsonSerializer.Serialize(request, JsonOptions),
            Status = "planning",
            Mode = HousekeeperRunPolicies.Steward,
            AutoConfirmPolicy = HousekeeperRunPolicies.L3Only,
            PermissionSnapshot = JsonSerializer.Serialize(new { bindingScope = "personal", ownerUserId = userId, connectorGrants = Array.Empty<object>() }),
            EstimatedCredits = 0,
            StartedAt = now,
            CreatedAt = now
        };
        _db.AgentRuns.Add(run);
        await _db.SaveChangesAsync(cancellationToken);

        _db.ExpertRunActions.Add(new ExpertRunAction
        {
            RunId = run.Id,
            TenantId = tenantId,
            UserId = userId,
            ActionType = "xhs_publish",
            RequestIdempotencyKey = Guid.NewGuid().ToString(),
            RequestJson = JsonSerializer.Serialize(ToRequestJson(request), JsonOptions),
            Status = "pending",
            CreatedAt = now,
            UpdatedAt = now
        });

        run.Status = "pending_actions";
        run.ResultSummary = $"小红书发布待确认：{Describe(request)}。";
        run.Result = JsonSerializer.Serialize(new { connector = XhsProviderCode, action = "xhs_publish", type = request.Type }, JsonOptions);
        AddEvent(run, 1, "running", "正在准备小红书发布内容。", now);
        AddEvent(run, 2, "pending_actions", run.ResultSummary, now);
        await _db.SaveChangesAsync(cancellationToken);

        return new ServiceResult(201, run.ResultSummary, await GetActionViewAsync(run.Id, cancellationToken));
    }

    /// <inheritdoc />
    public async Task<ServiceResult> ConfirmActionAsync(long userId, long tenantId, long actionId, XhsPublishConfirmRequest request, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(request.IdempotencyKey, out _))
            return new ServiceResult(422, "确认小红书发布动作时必须提供有效的幂等键。");

        var action = await _db.ExpertRunActions.SingleOrDefaultAsync(x =>
            x.Id == actionId && x.TenantId == tenantId && x.UserId == userId && x.ActionType == "xhs_publish", cancellationToken);
        if (action is null) return new ServiceResult(404, "请求的发布动作不存在。");

        var run = await _db.AgentRuns.SingleOrDefaultAsync(x => x.Id == action.RunId && x.TenantId == tenantId, cancellationToken);
        if (run is null) return new ServiceResult(404, "请求的运行不存在。");
        if (!IsSnapshotAuthorized(run, userId))
            return new ServiceResult(403, "当前成员无权执行该运行的动作。");

        var idempotencyKey = request.IdempotencyKey;
        var previous = await _db.ActionExecutionAudits.SingleOrDefaultAsync(
            x => x.RunActionId == action.Id && x.IdempotencyKey == idempotencyKey, cancellationToken);
        if (previous is not null) return ReplayActionResult(action, previous);
        if (action.Status != "pending") return new ServiceResult(409, "该发布动作已经确认或处理完成，不能再次执行。");

        var now = DateTime.UtcNow;
        action.Status = "executing";
        action.UpdatedAt = now;
        var audit = new ActionExecutionAudit
        {
            TenantId = tenantId,
            RunActionId = action.Id,
            OperatorUserId = userId,
            IdempotencyKey = idempotencyKey,
            Status = "executing",
            Command = JsonSerializer.Serialize(new { action_type = "xhs_publish" }),
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.ActionExecutionAudits.Add(audit);
        AddEvent(run, await NextSequenceAsync(run.Id, cancellationToken), "action_confirmed", "已确认小红书发布动作。", now);
        await _db.SaveChangesAsync(cancellationToken);

        string? failureMessage = null;
        string? noteId = null;
        try
        {
            var input = ReadPublishInput(action.RequestJson);
            if (input is null)
            {
                failureMessage = "发布参数缺失。";
            }
            else
            {
                var connector = await GetAuthorizedConnectorAsync(userId, tenantId, cancellationToken);
                if (connector is null)
                {
                    failureMessage = "小红书连接器未授权或不可用。";
                }
                else
                {
                    var result = await _xhs.PublishAsync(input, connector.CredentialRef!, cancellationToken);
                    if (result.Succeeded)
                    {
                        noteId = result.NoteId;
                    }
                    else
                    {
                        failureMessage = result.Message;
                    }
                }
            }
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            failureMessage = "小红书发布执行失败。";
        }

        var succeeded = noteId is not null;
        now = DateTime.UtcNow;
        action.Status = succeeded ? "executed" : "failed";
        action.Result = JsonSerializer.Serialize(succeeded
            ? (object)new { status = action.Status, note_id = noteId }
            : new { status = action.Status, error_code = "xhs_publish_failed" });
        action.UpdatedAt = now;
        audit.Status = action.Status;
        audit.Result = action.Result;
        audit.UpdatedAt = now;

        var summary = succeeded ? "小红书笔记发布成功。" : $"小红书发布失败：{failureMessage ?? "发布服务不可用"}。";
        run.Status = succeeded ? "completed" : "failed";
        run.FinishedAt = now;
        run.ResultSummary = summary;
        run.Result = JsonSerializer.Serialize(succeeded
            ? (object)new { connector = XhsProviderCode, status = run.Status, note_id = noteId }
            : new { connector = XhsProviderCode, status = run.Status, error_code = "xhs_publish_failed" }, JsonOptions);
        AddEvent(run, await NextSequenceAsync(run.Id, cancellationToken), succeeded ? "action_executed" : "action_failed", summary, now);
        await _db.SaveChangesAsync(cancellationToken);

        if (succeeded)
        {
            await _audit.LogAsync(tenantId, userId, FamilyAuditActions.XhsNotePublished, FamilyAuditTargetTypes.XhsNote,
                null, null, new { note_id = noteId, title = ReadTitle(action.RequestJson) }, "小红书笔记发布完成。", run.Id, cancellationToken);
            return new ServiceResult(200, summary, new { actionId = action.Id, status = action.Status, message = summary, noteId });
        }
        return new ServiceResult(502, summary);
    }

    /// <summary>校验发布参数：类型、标题与正文长度、媒体数量（图文≤18，视频恰 1）。</summary>
    private static ServiceResult? Validate(XhsPublishRequest request)
    {
        if (request.Type is not (ImageType or VideoType)) return new ServiceResult(422, "发布类型仅支持 image 或 video。");
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length > MaxTitleLength)
            return new ServiceResult(422, $"笔记标题必填且不超过 {MaxTitleLength} 个字符。");
        if (string.IsNullOrWhiteSpace(request.Content) || request.Content.Trim().Length > MaxContentLength)
            return new ServiceResult(422, $"笔记正文必填且不超过 {MaxContentLength} 个字符。");
        if (request.MediaPaths is null || request.MediaPaths.Count == 0)
            return new ServiceResult(422, "媒体路径列表不能为空。");
        if (request.Type == ImageType && request.MediaPaths.Count > MaxImageCount)
            return new ServiceResult(422, $"图文笔记最多 {MaxImageCount} 张图片。");
        if (request.Type == VideoType && request.MediaPaths.Count != 1)
            return new ServiceResult(422, "视频笔记必须恰好 1 个视频文件。");
        return null;
    }

    /// <summary>校验本人小红书连接器已授权：当前租户 + personal 作用域 + 本人 owner + connected 状态。</summary>
    private async Task<bool> IsAuthorizedAsync(long userId, long tenantId, CancellationToken cancellationToken)
    {
        return await GetAuthorizedConnectorAsync(userId, tenantId, cancellationToken) is not null;
    }

    private async Task<WorkspaceConnector?> GetAuthorizedConnectorAsync(long userId, long tenantId, CancellationToken cancellationToken)
    {
        return await (from item in _db.WorkspaceConnectors
                               join provider in _db.ConnectorProviders on item.ConnectorProviderId equals provider.Id
                               where item.TenantId == tenantId
                                     && item.BindingScope == "personal"
                                     && item.OwnerUserId == userId
                                     && item.DeletedAt == null
                                     && item.Status == "connected"
                                     && item.AuthStatus == WorkspaceConnectorAuthStatus.Connected
                                     && provider.Code == XhsProviderCode
                               select item).FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>复验运行权限快照：快照缺失视为存量运行放行；xhs 发布为 personal 快照，仅校验归属。</summary>
    private static bool IsSnapshotAuthorized(AgentRun run, long userId)
    {
        if (string.IsNullOrWhiteSpace(run.PermissionSnapshot)) return true;
        try
        {
            using var document = JsonDocument.Parse(run.PermissionSnapshot);
            if (document.RootElement.TryGetProperty("bindingScope", out var scope) && scope.GetString() == "personal")
            {
                return document.RootElement.TryGetProperty("ownerUserId", out var owner) && owner.TryGetInt64(out var ownerId) && ownerId == userId;
            }
            return true;
        }
        catch (JsonException)
        {
            return true;
        }
    }

    /// <summary>重放既有确认结果：同一幂等键仅返回首次执行结果，不重复发布。</summary>
    private static ServiceResult ReplayActionResult(ExpertRunAction action, ActionExecutionAudit audit)
    {
        var succeeded = audit.Status == "executed";
        return new ServiceResult(succeeded ? 200 : audit.Status == "executing" ? 202 : 502,
            succeeded ? "小红书笔记发布成功。" : "小红书发布动作正在处理或已执行失败。",
            succeeded ? new { actionId = action.Id, status = action.Status, message = "小红书笔记发布成功。" } : null);
    }

    /// <summary>将发布请求序列化为蛇形键 JSON（与动作 RequestJson 同惯例）。</summary>
    private static object ToRequestJson(XhsPublishRequest request) => new
    {
        type = request.Type,
        title = request.Title,
        content = request.Content,
        media_paths = request.MediaPaths,
        tags = request.Tags
    };

    /// <summary>从动作 RequestJson 解析发布参数；参数缺失或非法返回 null。</summary>
    private static XhsPublishInput? ReadPublishInput(string requestJson)
    {
        try
        {
            using var document = JsonDocument.Parse(requestJson);
            var root = document.RootElement;
            var type = ReadString(root, "type");
            var title = ReadString(root, "title");
            var content = ReadString(root, "content");
            var media = ReadStringArray(root, "media_paths");
            var tags = ReadStringArray(root, "tags");
            if (type is null || title is null || content is null || media.Count == 0) return null;
            return new XhsPublishInput(type, title, content, media, tags.Count > 0 ? tags : null);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>从动作 RequestJson 读取标题，用于审计摘要。</summary>
    private static string ReadTitle(string requestJson)
    {
        try
        {
            using var document = JsonDocument.Parse(requestJson);
            return ReadString(document.RootElement, "title") ?? "";
        }
        catch (JsonException)
        {
            return "";
        }
    }

    /// <summary>生成面向用户的内容描述：类型 + 标题 + 媒体数量。</summary>
    private static string Describe(XhsPublishRequest request) =>
        $"{(request.Type == ImageType ? "图文" : "视频")}《{request.Title}》（{request.MediaPaths.Count} 个媒体文件）";

    /// <summary>按蛇形键读取 JSON 属性字符串值；兼容 System.Text.Json 驼峰序列化形态。</summary>
    private static string? ReadString(JsonElement element, string snakeName)
    {
        if (element.TryGetProperty(snakeName, out var value) && value.ValueKind == JsonValueKind.String) return value.GetString();
        var parts = snakeName.Split('_');
        var camelName = parts.Length == 1 ? parts[0] : parts[0] + string.Concat(parts.Skip(1).Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
        return element.TryGetProperty(camelName, out value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    /// <summary>按蛇形键读取 JSON 字符串数组；缺失或非数组返回空列表。</summary>
    private static IReadOnlyList<string> ReadStringArray(JsonElement element, string snakeName)
    {
        if (!element.TryGetProperty(snakeName, out var value) && !TryGetCamelProperty(element, snakeName, out value))
            return [];
        if (value.ValueKind != JsonValueKind.Array) return [];
        var result = new List<string>();
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString())) result.Add(item.GetString()!);
        }
        return result;
    }

    private static bool TryGetCamelProperty(JsonElement element, string snakeName, out JsonElement value)
    {
        var parts = snakeName.Split('_');
        var camelName = parts.Length == 1 ? parts[0] : parts[0] + string.Concat(parts.Skip(1).Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
        return element.TryGetProperty(camelName, out value);
    }

    /// <summary>按运行读取发布动作视图（确认中心展示用）；运行创建后动作必然存在。</summary>
    private async Task<XhsPublishActionView> GetActionViewAsync(long runId, CancellationToken cancellationToken)
    {
        var action = await _db.ExpertRunActions.SingleAsync(x =>
            x.RunId == runId && x.ActionType == "xhs_publish", cancellationToken);
        return ToActionView(action);
    }

    /// <summary>从动作 RequestJson 生成动作视图：标题、描述（内容摘要与媒体数量）、风险等级 L2。</summary>
    private static XhsPublishActionView ToActionView(ExpertRunAction action)
    {
        var title = ReadTitle(action.RequestJson);
        var input = ReadPublishInput(action.RequestJson);
        var description = input is null
            ? "发布小红书笔记。"
            : $"{(input.Type == ImageType ? "图文" : "视频")}《{input.Title}》，{input.MediaPaths.Count} 个媒体文件，风险等级 {ConfirmationRiskLevel.L2}。";
        return new XhsPublishActionView
        {
            ActionId = action.Id,
            ActionType = action.ActionType,
            Status = action.Status,
            Title = string.IsNullOrWhiteSpace(title) ? "小红书笔记发布" : title,
            Description = description,
            RiskLevel = ConfirmationRiskLevel.L2
        };
    }

    private void AddEvent(AgentRun run, int sequence, string type, string message, DateTime createdAt) =>
        _db.RunEvents.Add(new RunEvent
        {
            TenantId = run.TenantId,
            RunId = run.Id,
            Sequence = sequence,
            EventType = type,
            Payload = JsonSerializer.Serialize(new { message }),
            CreatedAt = createdAt
        });

    private async Task<int> NextSequenceAsync(long runId, CancellationToken cancellationToken) =>
        (await _db.RunEvents.Where(x => x.RunId == runId).MaxAsync(x => (int?)x.Sequence, cancellationToken) ?? 0) + 1;
}
