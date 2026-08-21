using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.Family;

namespace HomeMind.Business.IServices.Family;

/// <summary>家庭多成员日历聚合、冲突检测、空档建议和到期提醒服务。</summary>
public interface IFamilyScheduleServices
{
    /// <summary>聚合家庭内活跃成员的日历事件。</summary>
    Task<ServiceResult> ListEventsAsync(long homeId, DateTime? from, DateTime? to, CancellationToken cancellationToken = default);
    /// <summary>检测家庭成员日历中的时间交叉。</summary>
    Task<ServiceResult> ListConflictsAsync(long homeId, DateTime? from, DateTime? to, CancellationToken cancellationToken = default);
    /// <summary>给出全部活跃成员共同空闲的候选时段。</summary>
    Task<ServiceResult> ListAvailabilityAsync(long homeId, DateTime? from, DateTime? to, int durationMinutes, CancellationToken cancellationToken = default);
    /// <summary>创建不含证件原件与号码的到期提醒记录。</summary>
    Task<ServiceResult> CreateDocumentDeadlineAsync(long homeId, long actorUserId, FamilyDocumentDeadlineCreateRequest request, CancellationToken cancellationToken = default);
    /// <summary>列出家庭证件到期提醒记录。</summary>
    Task<ServiceResult> ListDocumentDeadlinesAsync(long homeId, CancellationToken cancellationToken = default);
    /// <summary>列出缴费和证件到期提醒，并幂等生成 L1 确认卡。</summary>
    Task<ServiceResult> ListRemindersAsync(long homeId, DateTime? asOf = null, CancellationToken cancellationToken = default);
    /// <summary>汇总明日的家庭日程、冲突和到期提醒。</summary>
    Task<ServiceResult> GetTomorrowPreviewAsync(long homeId, DateTime? asOf = null, CancellationToken cancellationToken = default);
}
