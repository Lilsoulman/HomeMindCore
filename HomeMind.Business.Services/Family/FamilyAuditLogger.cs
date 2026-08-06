using System.Text.Json;
using HomeMind.Business.IServices.Family;
using HomeMind.Common.Model.Entities.Family;
using HomeMind.Common.Repository;
using Microsoft.Extensions.Logging;

namespace HomeMind.Business.Services.Family;

/// <summary>
/// 家庭域审计日志写入器；与管家动态（<c>steward_activities</c>）严格分离，专注 Family 域合规排障审计。
/// 写库失败仅记警告，不向调用方抛异常，与 Run Action 审计一致。
/// </summary>
public sealed class FamilyAuditLogger : IFamilyAuditLogger
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> AllowedActions = new(StringComparer.Ordinal)
    {
        FamilyAuditActions.MemberCorrection,
        FamilyAuditActions.MemberTerminalRestore,
        FamilyAuditActions.KnowledgeWrite,
        FamilyAuditActions.KnowledgeConflictResolved,
        FamilyAuditActions.DecisionRecord,
        FamilyAuditActions.ConfirmationConfirm,
        FamilyAuditActions.ConfirmationDeny,
        FamilyAuditActions.ConfirmationBatch,
        FamilyAuditActions.ActivityUndo,
        FamilyAuditActions.FavoriteCreate,
        FamilyAuditActions.FavoriteUpdate,
        FamilyAuditActions.FavoriteDelete,
        FamilyAuditActions.FavoriteImport
    };
    private static readonly HashSet<string> AllowedTargetTypes = new(StringComparer.Ordinal)
    {
        FamilyAuditTargetTypes.FamilyMember,
        FamilyAuditTargetTypes.FamilyKnowledge,
        FamilyAuditTargetTypes.DecisionHistory,
        FamilyAuditTargetTypes.ConfirmationItem,
        FamilyAuditTargetTypes.StewardActivity,
        FamilyAuditTargetTypes.PersonalFavorite
    };

    private readonly HomeMindDbContext _db;
    private readonly ILogger<FamilyAuditLogger> _logger;

    /// <summary>构造审计日志写入器。</summary>
    /// <param name="db">数据库上下文。</param>
    /// <param name="logger">日志记录器。</param>
    public FamilyAuditLogger(HomeMindDbContext db, ILogger<FamilyAuditLogger> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException">当 action 或 targetType 不在白名单时抛出。</exception>
    public async Task<bool> LogAsync(
        long homeId,
        long? actorUserId,
        string action,
        string targetType,
        long? targetId,
        object? before,
        object? after,
        string? reason,
        long? relatedRunId,
        CancellationToken cancellationToken = default)
    {
        if (!AllowedActions.Contains(action))
            throw new ArgumentException($"不支持的审计动作：{action}。", nameof(action));
        if (!AllowedTargetTypes.Contains(targetType))
            throw new ArgumentException($"不支持的审计目标类型：{targetType}。", nameof(targetType));

        try
        {
            _db.FamilyAuditLogs.Add(new FamilyAuditLog
            {
                HomeId = homeId,
                ActorUserId = actorUserId,
                Action = action,
                TargetType = targetType,
                TargetId = targetId,
                BeforeJson = BuildPayload(before),
                AfterJson = BuildPayload(after),
                Reason = reason,
                RelatedRunId = relatedRunId,
                CreatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "家庭审计日志写入失败：Action={Action} TargetType={TargetType} TargetId={TargetId} HomeId={HomeId}",
                action, targetType, targetId, homeId);
            return false;
        }
    }

    /// <summary>将 before/after 对象序列化为 JSON 字符串；null 返回 null。</summary>
    /// <param name="payload">待序列化对象，可为 null。</param>
    /// <returns>JSON 字符串或 null。</returns>
    private static string? BuildPayload(object? payload) =>
        payload is null ? null : JsonSerializer.Serialize(payload, JsonOptions);
}
