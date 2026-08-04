using HomeMind.Business.IServices.AI;
using HomeMind.Business.IServices.SmartHome;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.AI;
using HomeMind.Common.Model.ViewModel.Data.SmartHome;

namespace HomeMind.Business.Services.SmartHome;

/// <summary>Maps a built-in scene to the existing audited, confirmation-required Run workflow.</summary>
public sealed class SmartHomeSceneServices : ISmartHomeSceneServices
{
    private readonly IHousekeeperRunServices _housekeeperRuns;
    private readonly IAutomationRuleServices _automation;

    public SmartHomeSceneServices(IHousekeeperRunServices housekeeperRuns, IAutomationRuleServices automation)
    {
        _housekeeperRuns = housekeeperRuns;
        _automation = automation;
    }

    public Task<ServiceResult> RunAsync(long userId, long tenantId, string sceneKey, SceneRunRequest request, CancellationToken cancellationToken = default)
    {
        if (!SmartHomeSceneDefinitions.TryGetIntent(sceneKey, out var intent))
        {
            return Task.FromResult(new ServiceResult(404, "请求的场景不存在。"));
        }

        return RunAndPublishAsync(userId, tenantId, sceneKey, intent, request, cancellationToken);
    }

    private async Task<ServiceResult> RunAndPublishAsync(long userId, long tenantId, string sceneKey, string intent, SceneRunRequest request, CancellationToken cancellationToken)
    {
        var result = await _housekeeperRuns.CreateAsync(userId, tenantId, new HousekeeperRunRequest(intent, null, request?.IdempotencyKey), cancellationToken);
        if (result.Succeeded) await _automation.HandleSceneCompletedAsync(tenantId, sceneKey, DateTime.UtcNow, cancellationToken);
        return result;
    }
}
