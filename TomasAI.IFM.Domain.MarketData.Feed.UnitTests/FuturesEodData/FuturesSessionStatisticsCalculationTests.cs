using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime;
using TomasAI.IFM.Domain.MarketData.Shared;

namespace TomasAI.IFM.Domain.MarketData.Feed.UnitTests.FuturesEodData;

public sealed class FuturesSessionStatisticsCalculationTests
{
    [Theory]
    [InlineData(5050, 5000, 0.01)]
    [InlineData(4950, 5000, -0.01)]
    [InlineData(5000, 5000, 0)]
    public void Daily_percent_change_uses_statistics_open(
        decimal close,
        decimal open,
        double expected)
        => Assert.Equal(
            expected,
            FuturesSessionStatisticsUpdated.CalculateDailyPercentChange(close, open));

    [Theory]
    [InlineData(5050, 5000, PriceDirectionType.Rising)]
    [InlineData(4950, 5000, PriceDirectionType.Falling)]
    [InlineData(5000, 5000, PriceDirectionType.Rising)]
    public void Price_direction_uses_statistics_open(
        decimal close,
        decimal open,
        PriceDirectionType expected)
        => Assert.Equal(
            expected,
            FuturesSessionStatisticsUpdated.CalculatePriceDirection(close, open));
}
