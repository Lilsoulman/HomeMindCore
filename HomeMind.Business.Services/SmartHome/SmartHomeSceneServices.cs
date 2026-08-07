using HomeMind.Business.IServices.SmartHome;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.SmartHome;

namespace HomeMind.Business.Services.SmartHome;

/// <summary>
/// 内置场景兼容代理：场景键解析为管家意图后，懒启用对应场景模板实例并转调场景工作流运行链路。
/// 前端已接入契约与自动化规则动作引用保持不变；场景完成事件仍发布给自动化规则。
/// </summary>
public sealed class SmartHomeSceneServices : ISmartHomeSceneServices
{
    private readonly IScenarioWorkflowServices _scenarios;
    private readonly IAutomationRuleServices _automation;

    /// <summary>构造内置场景兼容代理服务。</summary>
    /// <param name="scenarios">场景工作流服务。</param>
    /// <param name="automation">自动化规则服务，用于发布场景完成事件。</param>
    public SmartHomeSceneServices(IScenarioWorkflowServices scenarios, IAutomationRuleServices automation)
    {
        _scenarios = scenarios;
        _automation = automation;
    }

    /// <inheritdoc />
    public async Task<ServiceResult> RunAsync(long userId, long tenantId, string sceneKey, SceneRunRequest request, CancellationToken cancellationToken = default)
    {
        if (!SmartHomeSceneDefinitions.TryGetIntent(sceneKey, out _))
        {
            return new ServiceResult(404, "请求的场景不存在。");
        }

        var enabled = await _scenarios.EnableAsync(userId, tenantId, sceneKey, cancellationToken);
        if (!enabled.Succeeded) return enabled;

        var instanceId = ReadInstanceId(enabled.Data);
        if (instanceId is null) return new ServiceResult(404, "场景实例解析失败。");

        var result = await _scenarios.RunAsync(userId, tenantId, instanceId.Value, new ScenarioRunRequest(request?.IdempotencyKey), cancellationToken);
        if (result.Succeeded) await _automation.HandleSceneCompletedAsync(tenantId, sceneKey, DateTime.UtcNow, cancellationToken);
        return result;
    }

    /// <summary>从启用结果解析实例主键；结果缺失或非法时返回 null。</summary>
    private static long? ReadInstanceId(object? data)
    {
        try
        {
            if (data is not ScenarioInstanceView view) return null;
            return view.Id;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
