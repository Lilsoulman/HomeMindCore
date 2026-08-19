using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.Finance;

namespace HomeMind.Business.IServices.Finance;

/// <summary>家庭缴费建档、提醒、缴后记录与年度趋势服务契约。</summary>
public interface IBillingServices
{
    /// <summary>创建一个家庭缴费账户并纳入到期日历。</summary>
    Task<ServiceResult> CreateAccountAsync(long homeId, long actorUserId, BillingAccountCreateRequest request, CancellationToken cancellationToken = default);
    /// <summary>列出家庭中仍启用的缴费账户。</summary>
    Task<ServiceResult> ListAccountsAsync(long homeId, CancellationToken cancellationToken = default);
    /// <summary>登记一次缴费，并同步写入既有财务账单事实源。</summary>
    Task<ServiceResult> RecordPaymentAsync(long homeId, long actorUserId, long accountId, BillingPaymentRecordRequest request, CancellationToken cancellationToken = default);
    /// <summary>获取提前三天和提前一天的到期提醒，并幂等投影为站内确认卡。</summary>
    Task<ServiceResult> ListRemindersAsync(long homeId, DateTime? asOf, CancellationToken cancellationToken = default);
    /// <summary>获取指定年度的缴费金额趋势。</summary>
    Task<ServiceResult> GetAnnualTrendAsync(long homeId, int? year, CancellationToken cancellationToken = default);
}
