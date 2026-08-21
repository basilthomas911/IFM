using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

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

    [Fact]
    public void CompleteSessionStatistics_BootstrapTheFirstLiveEodRow()
    {
        var valueDate = new DateOnly(2026, 8, 21);
        var contract = new FuturesContractV2ReadModel(
            "ES20260918",
            "E-mini S&P 500",
            "ES",
            "ESU6",
            "FUT",
            "USD",
            "XCME",
            "50",
            new DateOnly(2026, 9, 18),
            true);
        var tick = new FuturesTickDataV2ReadModel(
            contract.ContractId,
            valueDate,
            10,
            new TimeOnly(1, 30),
            5010m,
            25);
        var statistics = new FuturesSessionStatisticsSnapshot(
            contract.ContractId,
            valueDate,
            5000m,
            5020m,
            4990m,
            10,
            20,
            12_345,
            FuturesSessionVolumeQuality.ObservedComplete);

        var baseline = FuturesTickTradeDataInserted.CreateSessionBaseline(
            contract,
            tick,
            true,
            statistics);

        Assert.NotNull(baseline);
        Assert.Equal(valueDate, baseline.ValueDate);
        Assert.Equal(5000m, baseline.OpenPrice);
        Assert.Equal(5020m, baseline.HighPrice);
        Assert.Equal(4990m, baseline.LowPrice);
        Assert.Equal(5010m, baseline.ClosePrice);
        Assert.Equal(12_345, baseline.Volume);
        Assert.Equal(0.002d, baseline.DailyPercentChange);
        Assert.Equal(PriceDirectionType.Rising, baseline.PriceDirection);
    }
}
