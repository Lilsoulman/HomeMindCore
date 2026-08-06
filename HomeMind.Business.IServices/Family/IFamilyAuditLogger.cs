namespace HomeMind.Business.IServices.Family;

/// <summary>家庭域审计日志写入抽象；与管家动态、运行事件分离，承载成员更正/知识冲突/决策记录的审计动作。</summary>
public interface IFamilyAuditLogger
{
    /// <summary>写入一条家庭审计日志。</summary>
    /// <param name="homeId">归属家庭主键。</param>
    /// <param name="actorUserId">操作用户标识；系统行为可为空。</param>
    /// <param name="action">审计动作，取值必须为 <see cref="Common.Model.Entities.Family.FamilyAuditActions"/> 之一。</param>
    /// <param name="targetType">目标类型，取值必须为 <see cref="Common.Model.Entities.Family.FamilyAuditTargetTypes"/> 之一。</param>
    /// <param name="targetId">目标实体主键；新增记录在回填后可传。</param>
    /// <param name="before">操作前状态对象；序列化为 JSON 前值。</param>
    /// <param name="after">操作后状态对象；序列化为 JSON 后值。</param>
    /// <param name="reason">操作原因或额外上下文。</param>
    /// <param name="relatedRunId">可选的关联管家运行主键，与运行/确认链路同源。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>写入是否成功（写库失败仅记日志不抛）。</returns>
    /// <exception cref="ArgumentException">当 action 或 targetType 不在白名单时抛出。</exception>
    Task<bool> LogAsync(
        long homeId,
        long? actorUserId,
        string action,
        string targetType,
        long? targetId,
        object? before,
        object? after,
        string? reason,
        long? relatedRunId,
        CancellationToken cancellationToken = default);
}
