namespace HomeMind.Business.IServices.AI;

public interface IHousekeeperRunPolicy
{
    /// <summary>决定给定风险等级是否可被自动确认。L3 永远不允许被自动跳过。</summary>
    bool CanAutoConfirm(string policy, string riskLevel);
}

public static class HousekeeperRunPolicies
{
    public const string Steward = "steward";
    public const string Single = "single";

    public const string L3Only = "L3_only";
    public const string L2AndAbove = "L2_and_above";
    public const string Never = "never";
}
