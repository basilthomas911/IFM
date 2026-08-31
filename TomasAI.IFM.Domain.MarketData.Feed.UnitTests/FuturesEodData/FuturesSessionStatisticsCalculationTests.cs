using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Command.Model;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Model;
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
    [InlineData(5425, 5400, 0.0046)]
    [InlineData(5375, 5400, -0.0046)]
    [InlineData(5000, 0, 0)]
    [InlineData(5000, -1, 0)]
    public void Daily_percent_change_uses_current_close_and_session_open(
        decimal close,
        decimal open,
        double expected)
        => Assert.Equal(
            expected,
            FuturesSessionPriceCalculator.CalculateDailyPercentChange(close, open));

    [Theory]
    [InlineData(100.005, 100, 0)]
    [InlineData(100.015, 100, 0.0002)]
    [InlineData(99.995, 100, 0)]
    [InlineData(99.985, 100, -0.0002)]
    public void Daily_percent_change_uses_decimal_bankers_rounding_at_four_places(
        decimal close,
        decimal open,
        double expected)
        => Assert.Equal(
            expected,
            FuturesSessionPriceCalculator.CalculateDailyPercentChange(close, open));

    [Theory]
    [InlineData(5050, 5000, PriceDirectionType.Rising)]
    [InlineData(4950, 5000, PriceDirectionType.Falling)]
    [InlineData(5000, 5000, PriceDirectionType.Flat)]
    [InlineData(5000, 0, PriceDirectionType.Flat)]
    [InlineData(5000, -1, PriceDirectionType.Flat)]
    public void Price_direction_uses_current_close_and_session_open(
        decimal close,
        decimal open,
        PriceDirectionType expected)
        => Assert.Equal(
            expected,
            FuturesSessionPriceCalculator.CalculatePriceDirection(close, open));

    [Fact]
    public void Each_live_tick_recomputes_session_change_and_preserves_analytics_fields()
    {
        var valueDate = new DateOnly(2026, 8, 21);
        var contract = Contract();
        var current = new FuturesEodDataV2ReadModel(
            contract.ContractId,
            valueDate,
            contract.Symbol,
            5400m,
            5410m,
            5390m,
            5405m,
            12_345,
            0.0009,
            0.01,
            54.25,
            5500,
            5425,
            5350,
            MarketDirectionType.NeutralUp,
            MarketVolatilityType.Normal,
            PriceDirectionType.Rising,
            PriceVolatilityType.Falling,
            42.5,
            20,
            5375m,
            5250m);

        var rising = Update(current, 5425m, 11);
        var falling = Update(rising, 5375m, 12);
        var unchanged = Update(falling, 5400m, 13);

        rising.DailyPercentChange.Should().Be(0.0046);
        rising.PriceDirection.Should().Be(PriceDirectionType.Rising);
        falling.DailyPercentChange.Should().Be(-0.0046);
        falling.PriceDirection.Should().Be(PriceDirectionType.Falling);
        unchanged.DailyPercentChange.Should().Be(0);
        unchanged.PriceDirection.Should().Be(PriceDirectionType.Flat);
        unchanged.HighPrice.Should().Be(5425m);
        unchanged.LowPrice.Should().Be(5375m);
        unchanged.DailyStdDev.Should().Be(current.DailyStdDev);
        unchanged.DailyStdDevAmount.Should().Be(current.DailyStdDevAmount);
        unchanged.UpperBand.Should().Be(current.UpperBand);
        unchanged.Mean.Should().Be(current.Mean);
        unchanged.LowerBand.Should().Be(current.LowerBand);
        unchanged.MarketDirection.Should().Be(current.MarketDirection);
        unchanged.MarketVolatility.Should().Be(current.MarketVolatility);
        unchanged.PriceVolatility.Should().Be(current.PriceVolatility);
        unchanged.MarketDirectionIndicator.Should().Be(current.MarketDirectionIndicator);
        unchanged.WindowSize.Should().Be(current.WindowSize);
        unchanged.FiftyDMA.Should().Be(current.FiftyDMA);
        unchanged.TwoHundredDMA.Should().Be(current.TwoHundredDMA);

        FuturesEodDataV2ReadModel Update(
            FuturesEodDataV2ReadModel eod,
            decimal close,
            long tickId) => FuturesEodDataModel.CreateFuturesEodData(
                valueDate,
                new FuturesTickDataV2ReadModel(
                    contract.ContractId,
                    valueDate,
                    tickId,
                    new TimeOnly(10, 30),
                    close,
                    10),
                contract,
                eod,
                [],
                new NormalCurveTableReadModel([]),
                20,
                []);
    }

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

    static FuturesContractV2ReadModel Contract() => new(
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
}
