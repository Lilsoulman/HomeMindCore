using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.Pet;

namespace HomeMind.Business.IServices.Pet;

/// <summary>家庭宠物档案、照护日历和用品消耗预测服务。</summary>
public interface IPetServices
{
    /// <summary>创建家庭宠物档案。</summary>
    Task<ServiceResult> CreateAsync(long homeId, long actorUserId, PetCreateRequest request, CancellationToken cancellationToken = default);
    /// <summary>列出家庭内有效宠物档案。</summary>
    Task<ServiceResult> ListAsync(long homeId, CancellationToken cancellationToken = default);
    /// <summary>创建宠物疫苗或驱虫日历记录。</summary>
    Task<ServiceResult> AddCareEventAsync(long homeId, long actorUserId, long petId, PetCareEventCreateRequest request, CancellationToken cancellationToken = default);
    /// <summary>列出宠物照护日历记录。</summary>
    Task<ServiceResult> ListCareEventsAsync(long homeId, long petId, CancellationToken cancellationToken = default);
    /// <summary>创建或更新宠物用品库存和日均消耗。</summary>
    Task<ServiceResult> UpsertSupplyAsync(long homeId, long actorUserId, long petId, PetSupplyUpsertRequest request, CancellationToken cancellationToken = default);
    /// <summary>列出宠物用品库存，并在七天内耗尽时生成确认卡。</summary>
    Task<ServiceResult> ListSuppliesAsync(long homeId, long petId, CancellationToken cancellationToken = default);
    /// <summary>列出家庭即将到期的照护和断粮提醒。</summary>
    Task<ServiceResult> ListAlertsAsync(long homeId, DateTime? asOf = null, CancellationToken cancellationToken = default);
}
