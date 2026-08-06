using HomeMind.Business.IServices.AI;

namespace HomeMind.Business.Services.AI;

/// <summary>
/// 家庭管家托管运行的服务层策略。L3 永远不允许被自动确认（满足产品总设计约束）。
/// </summary>
public sealed class HousekeeperRunPolicy : IHousekeeperRunPolicy
{
    public bool CanAutoConfirm(string policy, string riskLevel) => (policy, riskLevel) switch
    {
        (HousekeeperRunPolicies.L3Only, "L1") => true,
        (HousekeeperRunPolicies.L2AndAbove, "L1") => true,
        (HousekeeperRunPolicies.L2AndAbove, "L2") => true,
        _ => false
    };
}
