using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.Finance;

namespace HomeMind.Business.IServices.Finance;

/// <summary>家庭财务账单导入、查询与聚合服务契约。</summary>
public interface IFinanceServices
{
    /// <summary>在家庭范围内导入 CSV 账单并按内容哈希去重。</summary>
    Task<ServiceResult> ImportAsync(long homeId, long actorUserId, FinanceImportRequest request, CancellationToken cancellationToken = default);
    /// <summary>查询家庭账单条目。</summary>
    Task<ServiceResult> ListAsync(long homeId, DateTime? from, DateTime? to, string? category, CancellationToken cancellationToken = default);
    /// <summary>生成指定时间窗口的支出聚合和确定性建议。</summary>
    Task<ServiceResult> SummarizeAsync(long homeId, DateTime? from, DateTime? to, CancellationToken cancellationToken = default);
}
