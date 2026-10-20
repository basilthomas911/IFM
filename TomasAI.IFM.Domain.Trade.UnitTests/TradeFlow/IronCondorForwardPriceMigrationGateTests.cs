using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.OptionPricer.Shared;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Model;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Trade.UnitTests.TradeFlow;

/// <summary>Numerical counterexamples that must be resolved before connecting the recovered pricing rule to exit recommendations.</summary>
/// <remarks>Deterministic calculator observations, not fabricated live-provider evidence or broker executions.</remarks>
public sealed class IronCondorForwardPriceMigrationGateTests
{
    [Fact]
    public void Combination_preserves_positive_and_negative_component_sums()
    {
        var put = LegacyForward(1, .4);
        var call = LegacyForward(0, .4);

        Assert.Equal(21d, put, 10);
        Assert.Equal(9d, call, 10);
        Assert.Equal(30m, IronCondorLegacyValueInitializers.CalculateForwardPrice((decimal)put, (decimal)call));
        Assert.Equal(-30m, IronCondorLegacyValueInitializers.CalculateForwardPrice(-21m, -9m));
        Assert.Equal(12m, IronCondorLegacyValueInitializers.CalculateForwardPrice(21m, -9m));
    }

    [Theory]
    [InlineData(.1)]
    [InlineData(.4)]
    [InlineData(.9)]
    public void Applying_short_position_signs_can_trigger_exit_with_zero_projected_value_change(double shortDelta)
    {
        // Each spread's current and opening theoretical price is 25 - 10 = 15 points.
        // Equal OTM values select put as the risk side in the legacy service.
        // With equal deltas the adverse and favorable adjustments exactly cancel.
        var put = LegacyForward(1, shortDelta);
        var call = LegacyForward(0, shortDelta);
        const decimal currentSignedPrice = -30m;
        const decimal multiplier = 50m;
        const int quantity = 1;
        const decimal maxLossLimit = 1000m;
        var forwardSignedPrice = -(decimal)Math.Round(put + call, 10);
        var forwardLossFromSignedValue = forwardSignedPrice * multiplier * quantity;
        var projectedValueChange = (forwardSignedPrice - currentSignedPrice) * multiplier * quantity;

        Assert.Equal(currentSignedPrice, forwardSignedPrice);
        Assert.Equal(0m, projectedValueChange);
        Assert.Equal(-1500m, forwardLossFromSignedValue);
        Assert.True(forwardLossFromSignedValue <= -maxLossLimit);
        Assert.Equal(1.5m, forwardLossFromSignedValue / -maxLossLimit);
    }

    /// <summary>Runs the actual recovered collection/forward implementation on one theoretical spread observation.</summary>
    static double LegacyForward(int lossFactor, double shortDelta)
    {
        var observation = new OptionSpreadResult(0, 10, 5000, .05, .05, 4900, .2, 4800, .2);
        observation.ShortValues.Add([25d]);
        observation.LongValues.Add([10d]);
        var forward = new ProbabilityValueCollection([observation])
            .SetForwardPrice(OptionType.Put, 10, 40, lossFactor, shortDelta, 15m);
        // Adapter is used solely to read the old result; loss probability is not calculated.
        return forward.ToViewModel(1, TradeType.PutCreditSpread, TradeStatus.IntraDay,
            new DateOnly(2026, 10, 20), new LossProbabilityDataModel(0, 0, 0)).ForwardPrice;
    }
}
