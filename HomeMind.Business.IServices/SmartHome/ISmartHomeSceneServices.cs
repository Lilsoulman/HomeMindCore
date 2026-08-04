using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.SmartHome;

namespace HomeMind.Business.IServices.SmartHome;

/// <summary>Runs built-in scenes by creating confirmation-required housekeeper actions.</summary>
public interface ISmartHomeSceneServices
{
    Task<ServiceResult> RunAsync(long userId, long tenantId, string sceneKey, SceneRunRequest request, CancellationToken cancellationToken = default);
}
