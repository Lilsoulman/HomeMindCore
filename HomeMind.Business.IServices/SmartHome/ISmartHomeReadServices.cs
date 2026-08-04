using HomeMind.Common.Model.ViewModel.Common;

namespace HomeMind.Business.IServices.SmartHome;

/// <summary>面向 Home+ 的标准化只读能力，不泄露连接器或厂商实体细节。</summary>
public interface ISmartHomeReadServices
{
    Task<ServiceResult> ListSpacesAsync(long tenantId, CancellationToken cancellationToken = default);
    Task<ServiceResult> ListDevicesAsync(long tenantId, long? spaceId, CancellationToken cancellationToken = default);
    Task<ServiceResult> ListScenesAsync(long tenantId, CancellationToken cancellationToken = default);
}
