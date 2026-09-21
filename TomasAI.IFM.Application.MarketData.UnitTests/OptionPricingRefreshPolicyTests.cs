using TomasAI.IFM.Application.MarketData.Pricing;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class OptionPricingRefreshPolicyTests
{
    [Fact]
    public void Approved_test_defaults_are_not_production_approval()
    {
        var policy = new OptionPricingRefreshPolicy();
        policy.Validate();
        Assert.Equal(250, policy.PriceDeltaMilliseconds);
        Assert.Equal(5000, policy.ImpliedVolatilityMilliseconds);
        Assert.Equal(1000, policy.FullRiskMilliseconds);
        Assert.False(policy.IsProductionQualified);
        Assert.Throws<InvalidOperationException>(policy.RequireProductionApproval);
        Assert.False((policy with { ProductionApproved = true, ReviewEvidenceId = "test" }).IsProductionQualified);
        Assert.False((policy with { ProductionApproved = true, Version = "reviewed/v1" }).IsProductionQualified);
        (policy with { ProductionApproved = true, Version = "reviewed/v1", ReviewEvidenceId = "review-123" })
            .RequireProductionApproval();
    }

    [Theory]
    [InlineData(0, 5000, 1000)]
    [InlineData(250, 100, 1000)]
    [InlineData(250, 5000, 0)]
    [InlineData(250, 60001, 1000)]
    public void Invalid_cadences_are_rejected(int selection, int iv, int risk)
    {
        var policy = new OptionPricingRefreshPolicy
        { PriceDeltaMilliseconds = selection, ImpliedVolatilityMilliseconds = iv, FullRiskMilliseconds = risk };
        Assert.Throws<ArgumentException>(policy.Validate);
    }
}
