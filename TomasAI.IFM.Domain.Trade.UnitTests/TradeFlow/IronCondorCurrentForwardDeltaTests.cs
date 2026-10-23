using FluentAssertions;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Model;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using Xunit;

namespace TomasAI.IFM.Domain.Trade.UnitTests.TradeFlow;

public sealed class IronCondorCurrentForwardDeltaTests
{
    static IronCondorTradePlanLegObservation[] Legs(int quantity) =>
    [
        new() { PutCall = 2, SignedQuantity = -quantity, Delta = -0.22, ForwardDelta = 0.9 },
        new() { PutCall = 2, SignedQuantity = quantity, Delta = -0.07, ForwardDelta = 0.9 },
        new() { PutCall = 1, SignedQuantity = -quantity, Delta = 0.20, ForwardDelta = 0.9 },
        new() { PutCall = 1, SignedQuantity = quantity, Delta = 0.09, ForwardDelta = 0.9 }
    ];

    [Theory]
    [InlineData(1, 0.04)]
    [InlineData(10, 0.04)]
    [InlineData(-1, -0.04)]
    public void Uses_current_deltas_and_signed_verticals_per_unit_independent_of_scenario(int quantity, double expected)
    {
        // Put vertical +0.15, call vertical -0.11 for the short condor.
        var legs = Legs(quantity);
        IronCondorTradePlanSnapshotCalculator.CalculateForwardDelta(legs).Should().BeApproximately(expected, 1e-12);
        IronCondorTradePlanSnapshotCalculator.CalculateForwardDelta(legs.Reverse().ToArray()).Should().BeApproximately(expected, 1e-12);
    }

    [Fact]
    public void Current_delta_is_written_to_snapshot_even_when_amplified_price_fails()
    {
        var observations = Legs(1);
        var position = new StrategyPositionSnapshot { Legs = observations.Select((leg, index) =>
            new StrategyPositionLeg { TradeLegId = Guid.NewGuid(), ContractId = $"option-{index}",
                SignedQuantity = leg.SignedQuantity!.Value, PutCall = leg.PutCall,
                CurrentPrice = 5, OpeningPrice = 5, Scenario = leg }).ToArray() };
        var plan = new StrategyTradePlanSnapshot { Position = position, CalculatedAtUtc = DateTime.UtcNow };
        var snapshot = new IronCondorTradePlanSnapshotCalculator().CreateFromForwardPrice(plan,
            new UpdateIronCondorTradePlanCommand { Position = position, ForwardPriceFailure = "test failure" });
        snapshot.ForwardDelta.Should().BeApproximately(0.04, 1e-12);
        snapshot.ForwardPrice.Should().Be(0);
        snapshot.ForwardPriceCalculationError.Should().Contain("test failure");
    }

    [Fact]
    public void Missing_or_invalid_current_delta_and_unbalanced_legs_remain_unavailable()
    {
        var legs = Legs(1);
        IronCondorTradePlanSnapshotCalculator.CalculateForwardDelta(legs[..3]).Should().BeNull();
        foreach (var delta in new double?[] { null, double.NaN, double.PositiveInfinity, 1.1 })
        {
            var invalid = Legs(1);
            invalid[0] = invalid[0] with { Delta = delta };
            IronCondorTradePlanSnapshotCalculator.CalculateForwardDelta(invalid).Should().BeNull();
        }
        legs[0] = legs[0] with { SignedQuantity = -2 };
        IronCondorTradePlanSnapshotCalculator.CalculateForwardDelta(legs).Should().BeNull();
        legs = Legs(1);
        legs[0] = legs[0] with { PutCall = 1 };
        IronCondorTradePlanSnapshotCalculator.CalculateForwardDelta(legs).Should().BeNull();
    }
}
