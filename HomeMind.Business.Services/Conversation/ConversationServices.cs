using System.Text;
using System.Text.Json;
using HomeMind.Business.IServices.Conversation;
using HomeMind.Business.IServices.Family;
using HomeMind.Common.Model.Entities;
using HomeMind.Common.Model.Entities.Family;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.AI;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ConversationEntity = HomeMind.Common.Model.Entities.Conversation;

namespace HomeMind.Business.Services.Conversation;

/// <summary>
/// 专家会话服务实现：会话 CRUD、消息历史与发送准备。
/// 会话为个人资源，全部读写按 <c>tenant_id + owner_user_id</c> 隔离，跨用户/跨租户一律 404；
/// 写操作（创建/重命名/软删除）写入家庭审计日志（home_id 取租户标识）。
/// </summary>
public sealed class ConversationServices : IConversationServices
{
    private const int MaxLimit = 50;
    private const int HistoryCount = 20;
    private const int ContextBudget = 12000;
    private const int AssistantContentMax = 2000;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HomeMindDbContext _db;
    private readonly IFamilyAuditLogger _audit;
    private readonly ILogger<ConversationServices> _logger;

    /// <summary>构造会话服务。</summary>
    /// <param name="db">数据库上下文。</param>
    /// <param name="audit">家庭审计写入器。</param>
    /// <param name="logger">日志器。</param>
    public ConversationServices(HomeMindDbContext db, IFamilyAuditLogger audit, ILogger<ConversationServices> logger)
    {
        _db = db;
        _audit = audit;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ServiceResult> ListAsync(long userId, long tenantId, int limit, string? cursor, CancellationToken cancellationToken = default)
    {
        if (limit <= 0) limit = 20;
        else if (limit > MaxLimit) limit = MaxLimit;

        var query = _db.Conversations.Where(x => x.TenantId == tenantId && x.OwnerUserId == userId && x.DeletedAt == null);
        if (TryDecodeCursor(cursor, out var updatedAt, out var id))
            query = query.Where(x => x.UpdatedAt < updatedAt || (x.UpdatedAt == updatedAt && x.Id < id));

        var items = await query
            .OrderByDescending(x => x.UpdatedAt).ThenByDescending(x => x.Id)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);

        var hasMore = items.Count > limit;
        if (hasMore) items = items.Take(limit).ToList();
        var nextCursor = hasMore && items.Count > 0 ? EncodeCursor(items[^1].UpdatedAt, items[^1].Id) : null;

        return new ServiceResult(200, "查询成功。", new ConversationListView(items.Select(ToView).ToArray(), nextCursor));
    }

    /// <inheritdoc />
    public async Task<ServiceResult> CreateAsync(long userId, long tenantId, ConversationCreateRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return new ServiceResult(422, "会话标题为必填项。");

        long? expertId = null, versionId = null;
        if (request.ExpertId is long expert)
        {
            var resolved = await ResolveExpertAsync(userId, tenantId, expert, cancellationToken);
            if (resolved is null) return new ServiceResult(404, "请求的专家不存在或不可见。");
            expertId = expert;
            versionId = resolved.Value.VersionId;
        }
        if (request.WorkspaceConnectorId is long connectorId
            && !await _db.WorkspaceConnectors.AnyAsync(x => x.Id == connectorId && x.TenantId == tenantId, cancellationToken))
            return new ServiceResult(404, "请求的连接器实例不存在。");

        var now = DateTime.UtcNow;
        var conversation = new ConversationEntity
        {
            TenantId = tenantId,
            OwnerUserId = userId,
            Title = request.Title.Trim(),
            ExpertId = expertId,
            ExpertVersionId = versionId,
            WorkspaceConnectorId = request.WorkspaceConnectorId,
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.Conversations.Add(conversation);
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync(tenantId, userId, FamilyAuditActions.ConversationCreate,
            FamilyAuditTargetTypes.Conversation, conversation.Id,
            null, ToAuditSnapshot(conversation), null, null, cancellationToken);

        return new ServiceResult(201, "会话已创建。", ToView(conversation));
    }

    /// <inheritdoc />
    public async Task<ServiceResult> GetAsync(long userId, long tenantId, long conversationId, CancellationToken cancellationToken = default)
    {
        var conversation = await FindOwnedAsync(userId, tenantId, conversationId, cancellationToken);
        return conversation is null
            ? new ServiceResult(404, "请求的会话不存在。")
            : new ServiceResult(200, "查询成功。", ToView(conversation));
    }

    /// <inheritdoc />
    public async Task<ServiceResult> UpdateAsync(long userId, long tenantId, long conversationId, ConversationUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var conversation = await FindOwnedAsync(userId, tenantId, conversationId, cancellationToken);
        if (conversation is null) return new ServiceResult(404, "请求的会话不存在。");
        if (conversation.RowVersion != request.RowVersion)
            return new ServiceResult(409, "会话已被其他操作修改，请刷新后重试。", null, ApiErrorCodes.OptimisticLockConflict);

        long? expertId = request.ExpertId, versionId = null;
        if (request.ExpertId is long expert)
        {
            var resolved = await ResolveExpertAsync(userId, tenantId, expert, cancellationToken);
            if (resolved is null) return new ServiceResult(404, "请求的专家不存在或不可见。");
            expertId = expert;
            versionId = resolved.Value.VersionId;
        }
        if (request.WorkspaceConnectorId is long connectorId
            && !await _db.WorkspaceConnectors.AnyAsync(x => x.Id == connectorId && x.TenantId == tenantId, cancellationToken))
            return new ServiceResult(404, "请求的连接器实例不存在。");

        var before = ToAuditSnapshot(conversation);
        conversation.Title = request.Title.Trim();
        conversation.ExpertId = expertId;
        conversation.ExpertVersionId = versionId;
        conversation.WorkspaceConnectorId = request.WorkspaceConnectorId;
        conversation.RowVersion += 1;
        conversation.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync(tenantId, userId, FamilyAuditActions.ConversationRename,
            FamilyAuditTargetTypes.Conversation, conversation.Id,
            before, ToAuditSnapshot(conversation), null, null, cancellationToken);

        return new ServiceResult(200, "会话已更新。", ToView(conversation));
    }

    /// <inheritdoc />
    public async Task<ServiceResult> DeleteAsync(long userId, long tenantId, long conversationId, CancellationToken cancellationToken = default)
    {
        var conversation = await FindOwnedAsync(userId, tenantId, conversationId, cancellationToken);
        if (conversation is null) return new ServiceResult(404, "请求的会话不存在。");

        conversation.DeletedAt = DateTime.UtcNow;
        conversation.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync(tenantId, userId, FamilyAuditActions.ConversationDelete,
            FamilyAuditTargetTypes.Conversation, conversation.Id,
            ToAuditSnapshot(conversation), null, "会话软删除，消息历史保留留档。", null, cancellationToken);

        return new ServiceResult(200, "会话已删除。");
    }

    /// <inheritdoc />
    public async Task<ServiceResult> ListMessagesAsync(long userId, long tenantId, long conversationId, int limit, string? cursor, CancellationToken cancellationToken = default)
    {
        if (await FindOwnedAsync(userId, tenantId, conversationId, cancellationToken) is null)
            return new ServiceResult(404, "请求的会话不存在。");
        if (limit <= 0) limit = 20;
        else if (limit > MaxLimit) limit = MaxLimit;

        var query = _db.ConversationMessages.Where(x => x.ConversationId == conversationId);
        if (TryDecodeIdCursor(cursor, out var id))
            query = query.Where(x => x.Id < id);

        var items = await query
            .OrderByDescending(x => x.Id)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);

        var hasMore = items.Count > limit;
        if (hasMore) items = items.Take(limit).ToList();
        var nextCursor = hasMore && items.Count > 0 ? EncodeIdCursor(items[^1].Id) : null;

        return new ServiceResult(200, "查询成功。", new ConversationMessageListView(items.Select(ToMessageView).ToArray(), nextCursor));
    }

    /// <inheritdoc />
    public async Task<ServiceResult> PrepareMessageAsync(long userId, long tenantId, long conversationId, string content, CancellationToken cancellationToken = default)
    {
        var conversation = await FindOwnedAsync(userId, tenantId, conversationId, cancellationToken);
        if (conversation is null) return new ServiceResult(404, "请求的会话不存在。");
        if (conversation.ExpertId is null || conversation.ExpertVersionId is null)
            return new ServiceResult(422, "该会话尚未绑定专家，请先在会话设置中选择专家。", null, ApiErrorCodes.PreconditionFailed);

        var resolved = await ResolveExpertAsync(userId, tenantId, conversation.ExpertId.Value, cancellationToken);
        if (resolved is null) return new ServiceResult(404, "会话绑定的专家不可用。");

        var history = await _db.ConversationMessages
            .Where(x => x.ConversationId == conversationId)
            .OrderByDescending(x => x.Id)
            .Take(HistoryCount)
            .ToListAsync(cancellationToken);
        history.Reverse();

        var messages = new List<(string Role, string Content)>();
        var budget = ContextBudget;
        foreach (var message in history)
        {
            if (message.Content.Length > budget) break;
            messages.Add((message.Role, message.Content));
            budget -= message.Content.Length;
        }
        messages.Add(("user", content));

        var inputJson = JsonSerializer.Serialize(new { messages = messages.Select(m => new { m.Role, m.Content }) }, JsonOptions);
        return new ServiceResult(200, "已准备消息上下文。", new PreparedMessageContext(conversation.ExpertId.Value, inputJson));
    }

    /// <inheritdoc />
    public async Task<ServiceResult> RecordUserMessageAsync(long userId, long tenantId, long conversationId, long runId, string content, CancellationToken cancellationToken = default)
    {
        if (await FindOwnedAsync(userId, tenantId, conversationId, cancellationToken) is null)
            return new ServiceResult(404, "请求的会话不存在。");

        var existing = await _db.ConversationMessages
            .SingleOrDefaultAsync(x => x.ConversationId == conversationId && x.RunId == runId, cancellationToken);
        if (existing is not null) return new ServiceResult(200, "消息已记录。", existing.Id);

        var message = new ConversationMessage
        {
            ConversationId = conversationId,
            Role = "user",
            Content = content,
            RunId = runId,
            CreatedAt = DateTime.UtcNow
        };
        _db.ConversationMessages.Add(message);
        await _db.SaveChangesAsync(cancellationToken);
        return new ServiceResult(201, "消息已发送。", message.Id);
    }

    /// <inheritdoc />
    public async Task<ServiceResult> AppendAssistantMessageAsync(long tenantId, long conversationId, long runId, string content, CancellationToken cancellationToken = default)
    {
        try
        {
            var conversationExists = await _db.Conversations.AnyAsync(
                x => x.Id == conversationId && x.TenantId == tenantId && x.DeletedAt == null, cancellationToken);
            if (!conversationExists) return new ServiceResult(200, "会话不存在，跳过消息追加。");

            var exists = await _db.ConversationMessages.AnyAsync(
                x => x.ConversationId == conversationId && x.RunId == runId, cancellationToken);
            if (exists) return new ServiceResult(200, "消息已存在。");

            var contentTruncated = content.Length > AssistantContentMax ? content[..AssistantContentMax] : content;
            _db.ConversationMessages.Add(new ConversationMessage
            {
                ConversationId = conversationId,
                Role = "assistant",
                Content = contentTruncated,
                RunId = runId,
                CreatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync(cancellationToken);
            return new ServiceResult(200, "消息已追加。");
        }
        catch (Exception error)
        {
            _logger.LogWarning(error, "追加 assistant 消息失败，conversationId={ConversationId} runId={RunId}", conversationId, runId);
            return new ServiceResult(200, "消息追加失败，已记录日志。");
        }
    }

    private Task<ConversationEntity?> FindOwnedAsync(long userId, long tenantId, long conversationId, CancellationToken cancellationToken)
        => _db.Conversations.SingleOrDefaultAsync(
            x => x.Id == conversationId && x.TenantId == tenantId && x.OwnerUserId == userId && x.DeletedAt == null, cancellationToken);

    /// <summary>解析对当前用户可见的专家及其最新已发布版本；平台基础专家（owner 空）或本人自建专家均可。</summary>
    private async Task<(long ExpertId, long VersionId)?> ResolveExpertAsync(long userId, long tenantId, long expertId, CancellationToken cancellationToken)
    {
        var expert = await _db.Experts.SingleOrDefaultAsync(
            x => x.Id == expertId
                 && x.Status == "active"
                 && x.DeletedAt == null
                 && (x.TenantId == 1 || x.TenantId == tenantId)
                 && (x.OwnerUserId == null || x.OwnerUserId == userId), cancellationToken);
        if (expert is null) return null;

        var version = await _db.ExpertVersions
            .Where(v => v.ExpertId == expert.Id && v.Status == "published")
            .OrderByDescending(v => v.Version)
            .FirstOrDefaultAsync(cancellationToken);
        return version is null ? null : (expert.Id, version.Id);
    }

    private static ConversationView ToView(ConversationEntity c) => new(
        c.Id, c.Title, c.ExpertId, c.ExpertVersionId, c.WorkspaceConnectorId, c.CreatedAt, c.UpdatedAt, c.RowVersion);

    private static ConversationMessageView ToMessageView(ConversationMessage m) => new(
        m.Id, m.Role, m.Content, m.RunId, m.CreatedAt);

    private static object ToAuditSnapshot(ConversationEntity c) => new
    {
        c.Id, c.Title, c.ExpertId, c.ExpertVersionId, c.WorkspaceConnectorId, c.DeletedAt
    };

    /// <summary>将 updated_at + id 编码为 base64 游标。</summary>
    private static string EncodeCursor(DateTime updatedAt, long id)
    {
        var key = $"{updatedAt:O}|{id}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(key));
    }

    /// <summary>解码会话列表游标；非法游标按第一页处理。</summary>
    private static bool TryDecodeCursor(string? cursor, out DateTime updatedAt, out long id)
    {
        updatedAt = default;
        id = 0;
        if (string.IsNullOrWhiteSpace(cursor)) return false;
        try
        {
            var key = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            var parts = key.Split('|', 2);
            updatedAt = DateTime.Parse(parts[0], null, System.Globalization.DateTimeStyles.RoundtripKind);
            id = long.Parse(parts[1]);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>将消息主键编码为 base64 游标。</summary>
    private static string EncodeIdCursor(long id) => Convert.ToBase64String(Encoding.UTF8.GetBytes(id.ToString()));

    /// <summary>解码消息列表游标；非法游标按第一页处理。</summary>
    private static bool TryDecodeIdCursor(string? cursor, out long id)
    {
        id = 0;
        if (string.IsNullOrWhiteSpace(cursor)) return false;
        try
        {
            id = long.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(cursor)));
            return true;
        }
        catch
        {
            return false;
        }
    }
}
