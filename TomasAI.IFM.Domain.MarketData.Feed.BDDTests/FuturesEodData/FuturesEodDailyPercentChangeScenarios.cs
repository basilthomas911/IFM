using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Model;
using TomasAI.IFM.Domain.MarketData.Shared;

namespace TomasAI.IFM.Domain.MarketData.Feed.BDDTests.FuturesEodData;

/// <summary>Executable behavior specifications for live session-relative price changes.</summary>
public sealed class FuturesEodDailyPercentChangeScenarios
{
    [Theory]
    [InlineData(5400, 5425, 0.0046, PriceDirectionType.Rising)]
    [InlineData(5400, 5375, -0.0046, PriceDirectionType.Falling)]
    [InlineData(5400, 5400, 0, PriceDirectionType.Flat)]
    public void GivenAValidSessionOpen_WhenAnIndependentTradeArrives_ThenTheCurrentCloseDeterminesTheChange(
        decimal sessionOpen,
        decimal currentClose,
        double expectedChange,
        PriceDirectionType expectedDirection)
    {
        FuturesSessionPriceCalculator.CalculateDailyPercentChange(currentClose, sessionOpen)
            .Should().Be(expectedChange);
        FuturesSessionPriceCalculator.CalculatePriceDirection(currentClose, sessionOpen)
            .Should().Be(expectedDirection);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void GivenNoValidSessionOpen_WhenATradeArrives_ThenNoFalseMovementIsCreated(
        decimal sessionOpen)
    {
        FuturesSessionPriceCalculator.CalculateDailyPercentChange(5425m, sessionOpen)
            .Should().Be(0);
        FuturesSessionPriceCalculator.CalculatePriceDirection(5425m, sessionOpen)
            .Should().Be(PriceDirectionType.Flat);
    }
}
