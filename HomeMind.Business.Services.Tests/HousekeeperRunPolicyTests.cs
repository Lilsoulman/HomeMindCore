using HomeMind.Business.IServices.AI;
using HomeMind.Business.Services.AI;
using Xunit;

namespace HomeMind.Business.Services.Tests;

public class HousekeeperRunPolicyTests
{
    private readonly IHousekeeperRunPolicy _policy = new HousekeeperRunPolicy();

    [Theory]
    [InlineData(HousekeeperRunPolicies.L3Only, "L1", true)]
    [InlineData(HousekeeperRunPolicies.L3Only, "L2", false)]
    [InlineData(HousekeeperRunPolicies.L3Only, "L3", false)]
    [InlineData(HousekeeperRunPolicies.L2AndAbove, "L1", true)]
    [InlineData(HousekeeperRunPolicies.L2AndAbove, "L2", true)]
    [InlineData(HousekeeperRunPolicies.L2AndAbove, "L3", false)]
    [InlineData(HousekeeperRunPolicies.Never, "L1", false)]
    [InlineData(HousekeeperRunPolicies.Never, "L2", false)]
    [InlineData(HousekeeperRunPolicies.Never, "L3", false)]
    public void CanAutoConfirm_Follows_Whitelist(string policy, string risk, bool expected)
    {
        Assert.Equal(expected, _policy.CanAutoConfirm(policy, risk));
    }

    [Fact]
    public void L3_Is_Always_Blocked_Regardless_Of_Policy()
    {
        Assert.False(_policy.CanAutoConfirm(HousekeeperRunPolicies.L2AndAbove, "L3"));
        Assert.False(_policy.CanAutoConfirm(HousekeeperRunPolicies.L3Only, "L3"));
        Assert.False(_policy.CanAutoConfirm(HousekeeperRunPolicies.Never, "L3"));
    }
}
