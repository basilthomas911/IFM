using FluentAssertions;
using Xunit;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Model;
using TomasAI.IFM.Domain.Trade.Shared;

namespace TomasAI.IFM.Domain.Trade.UnitTests.TradeFlow;

public sealed class IronCondorTradeLimitInitializationTests
{
    [Theory]
    [InlineData(TradeType.ShortIronCondor, 20, 2.5)]
    [InlineData(TradeType.LongIronCondor, 2.5, 20)]
    public void Spread_limits_follow_legacy_strategy_formulas(TradeType type, double loss, double profit)
    {
        IronCondorTradeLimitInitialization.CalculateMaximumLossPriceLimit(type, 10m).Should().Be((decimal)loss);
        IronCondorTradeLimitInitialization.CalculateMinimumProfitPriceLimit(type, 10m).Should().Be((decimal)profit);
        IronCondorTradeLimitInitialization.CalculateMaximumProfitPriceLimit(type, 10m).Should().Be((decimal)profit);
    }

    [Fact]
    public void Initialization_populates_trade_and_both_spreads_with_current_profit_and_contract_multiplier()
    {
        var result = IronCondorTradeLimitInitialization.Initialize(1101, TradeType.ShortIronCondor,
            8m, 5m, 2, 50m, 100000m, 5000m, 5.2m, 10, DateTime.UnixEpoch, "test");
        result.TradeLimit.MaxProfit.Should().Be(1300m);
        result.TradeLimit.MaxLoss.Should().Be(-2000m);
        result.TradeLimit.MaxReturn.Should().Be(0.26m);
        result.TradeLimit.MaxLossLimit.Should().Be(26m);
        result.TradeLimit.MinProfitLimit.Should().Be(3.25m);
        result.TradeLimit.MinProfitTarget.Should().Be(660.4m);
        result.TradeLimit.DailyProfitTarget.Should().Be(140.4m);
        result.SpreadLimits.Select(x => x.TradeType).Should().Equal(TradeType.PutCreditSpread, TradeType.CallCreditSpread);
        IronCondorTradeLimitInitialization.CalculateSpreadValue(8m, 2, 5m).Should().Be(80m);
    }

    [Fact]
    public void Equal_actual_opening_put_prices_produce_zero_put_limits_without_rejecting_the_other_spread()
    {
        var result = IronCondorTradeLimitInitialization.Initialize(1101, TradeType.ShortIronCondor,
            0m, 13.05m, 1, 50m, 100000m, 5000m, 5.2m, 44, DateTime.UnixEpoch, "test");
        result.TradeLimit.MaxProfit.Should().Be(652.5m);
        result.TradeLimit.MaxLossLimit.Should().Be(26.10m);
        result.SpreadLimits[0].MaxLossLimit.Should().Be(0m);
        result.SpreadLimits[0].MinProfitLimit.Should().Be(0m);
    }

    [Fact]
    public void Midpoint_and_expiry_edges_are_explicit()
    {
        IronCondorTradeLimitInitialization.CalculateSpreadPrice(12m, 14m, 3m, 5m).Should().Be(9m);
        IronCondorTradeLimitInitialization.CalculateDailyProfitTarget(100m, 0, 2m).Should().Be(0m);
        IronCondorTradeLimitInitialization.CalculateMaximumReturn(100m, 0m).Should().Be(0m);
        var crossed = () => IronCondorTradeLimitInitialization.CalculateSpreadPrice(14m, 12m, 3m, 5m);
        crossed.Should().Throw<ArgumentOutOfRangeException>();
        var missingBalance = () => IronCondorTradeLimitInitialization.CalculateMaximumLoss(0m);
        missingBalance.Should().Throw<ArgumentOutOfRangeException>();
    }
}
