using FluentAssertions;
using TomasAI.IFM.Domain.Trade.Shared;

namespace TomasAI.IFM.Domain.Trade.UnitTests;

public sealed class StrategyPositionIdTests
{
    [Theory]
    [InlineData(TradeStrategyKind.IronCondor)]
    [InlineData(TradeStrategyKind.VerticalSpread)]
    [InlineData(TradeStrategyKind.FuturesOutright)]
    public void Create_returns_a_stable_valid_identity(TradeStrategyKind strategyKind)
    {
        var trade = new TradeEntityId(11, 17, 101, 7);

        var first = StrategyPositionId.Create(trade, strategyKind);
        var second = StrategyPositionId.Create(trade, strategyKind);

        first.Should().Be(second);
        first.IsValid.Should().BeTrue();
        first.Trade.Should().Be(trade);
        first.Format().Should().StartWith(trade.Format() + ".");
    }

    [Fact]
    public void Create_uses_distinct_purposes_for_option_and_futures_positions()
    {
        var trade = new TradeEntityId(11, 17, 101, 7);

        var option = StrategyPositionId.Create(trade, TradeStrategyKind.IronCondor);
        var futures = StrategyPositionId.Create(trade, TradeStrategyKind.FuturesOutright);

        option.PositionId.Should().NotBe(futures.PositionId);
    }

    [Theory]
    [InlineData(TradeStrategyKind.Unknown)]
    public void Create_rejects_an_unsupported_strategy(TradeStrategyKind strategyKind)
    {
        var action = () => StrategyPositionId.Create(new TradeEntityId(11, 17, 101, 7), strategyKind);

        action.Should().Throw<ArgumentException>().WithParameterName("strategyKind");
    }

    [Fact]
    public void Create_rejects_an_invalid_trade_identity()
    {
        var action = () => StrategyPositionId.Create(default, TradeStrategyKind.IronCondor);

        action.Should().Throw<ArgumentException>().WithParameterName("trade");
    }
}
