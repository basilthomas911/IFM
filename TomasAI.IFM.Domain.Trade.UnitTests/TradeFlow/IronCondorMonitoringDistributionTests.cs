using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.OptionPricer.Shared;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Model;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Trade.UnitTests.TradeFlow;

public sealed class IronCondorMonitoringDistributionTests
{
    [Theory]
    [InlineData(0, 0.1, 12, 30)]
    [InlineData(1, 0.4, 10, 40)]
    [InlineData(1, 0.9, 3, 8)]
    public void Forward_calculation_matches_the_recovered_provider_formula(int factor, double delta, int remaining, int total)
    {
        var result = new OptionSpreadResult(0, remaining, 5000, .05, .05, 4900, .2, 4800, .2);
        result.ShortValues.Add([25]); result.LongValues.Add([10]);
        var legacy = new ProbabilityValueCollection([result]).SetForwardPrice(OptionType.Put, remaining, total, factor, delta, 15m);
        IronCondorMonitoringDistributionCompute.CalculateForwardPrice(15, 15, remaining, total, factor, delta)
            .Should().BeApproximately(legacy.ToViewModel(1, TradeType.PutCreditSpread, TradeStatus.IntraDay, new(2026,9,8), new(0,0,0)).ForwardPrice, 1e-12);
    }

    [Theory]
    [InlineData(1, TradeType.ShortIronCondor, TradeType.PutCreditSpread, TradeType.CallCreditSpread)]
    [InlineData(-1, TradeType.LongIronCondor, TradeType.PutDebitSpread, TradeType.CallDebitSpread)]
    public void Current_calculator_observations_create_a_matching_pair_without_legacy_trade_rows(int direction,
        TradeType type, TradeType putType, TradeType callType)
    {
        var (legs, risk) = IronCondorOptionCalculatorTests.Evidence();
        legs = legs.Select(leg => leg with { SignedQuantity = leg.SignedQuantity*direction }).ToArray();
        var now = risk[0].EvaluatedAtUtc.UtcDateTime;
        var trade = new EstablishedTradeDefinition { Id = new(1,2,3,4), StrategyKind = TradeStrategyKind.IronCondor,
            EstablishedAtUtc = now.AddDays(-7), Legs = legs };
        var position = new StrategyPositionSnapshot { Id = StrategyPositionId.Create(trade.Id, trade.StrategyKind),
            Legs = legs.Select(leg => new StrategyPositionLeg { TradeLegId = leg.TradeLegId, ContractId = leg.ContractId,
                SignedQuantity = leg.SignedQuantity, PutCall = leg.PutCall, CurrentPrice = 10, OpeningPrice = 10 }).ToArray() };
        var prices = IronCondorOptionCalculator.Calculate(legs, risk, now);
        var pair = IronCondorMonitoringDistributionCompute.Calculate(trade, position, prices, risk,
            new TradeLimitReadModel { TradeId = 4, TradeType = type, MaxLoss = -2000 }, new(2026,9,8));
        pair.Put.TradeType.Should().Be(putType); pair.Call.TradeType.Should().Be(callType);
        pair.Put.TradeStatus.Should().Be(TradeStatus.IntraDay);
        pair.Put.CreatedOn.Should().Be(now); pair.Call.CreatedOn.Should().Be(now);
        pair.Put.Id.Should().BePositive(); pair.Call.Id.Should().Be(pair.Put.Id);
        double.IsFinite(pair.Put.ForwardPrice).Should().BeTrue(); double.IsFinite(pair.Call.ForwardPrice).Should().BeTrue();
        pair.Put.LossProbability.Should().Be(0); pair.Call.LossProbability.Should().Be(0);
    }

    [Fact]
    public void Invalid_denominators_are_rejected_instead_of_becoming_forward_risk_defaults()
    {
        FluentActions.Invoking(() => IronCondorMonitoringDistributionCompute.CalculateForwardPrice(15, 15, 0, 20, 1, .2))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => IronCondorMonitoringDistributionCompute.CalculateForwardPrice(15, 15, 10, 0, 1, .2))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => IronCondorMonitoringDistributionCompute.CalculateForwardPrice(15, 15, 10, 20, 1, double.NaN))
            .Should().Throw<ArgumentException>();
    }
}
