using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesBbSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesEmaSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.UI.Net.ViewModels.MarketData;

namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.ViewModels;

public sealed class FuturesTradeSignalUIViewModelTests
{
    [Fact]
    public void PartialSnapshot_DisplaysAvailableRsiAndMarksMissingSiblingsUnavailable()
    {
        var snapshot = new MarketOutlookReadModel
        {
            ContractId = "ESU26",
            ValueDate = new DateOnly(2026, 8, 21),
            UpdatedAtUtc = DateTime.UtcNow,
            FuturesRsiSignal = new FuturesRsiSignalReadModel
            {
                ContractId = "ESU26",
                ValueDate = new DateOnly(2026, 8, 21),
                TimePeriod = TimeFrameType.FifteenSeconds,
                RSI = 64.25
            },
            MissingInputs = "EOD, TDI, ITI direction, ITI extreme, ITI reversal, VX price"
        };

        var model = new FuturesTradeSignalUIViewModel(snapshot);

        model.RSI.Should().Be("64.25");
        model.MDITrend.Should().Be("N/A");
        model.Trend.Should().Be("N/A");
        model.FiftyDMA.Should().Be("N/A");
        model.TrendExtreme.Should().Be("N/A");
        model.TrendReversal.Should().Be("N/A");
    }

    [Theory]
    [InlineData(IntrinsicTimeModeType.Trending)]
    [InlineData(IntrinsicTimeModeType.TrendDirectionChanged)]
    [InlineData(IntrinsicTimeModeType.TrendExtremeChanged)]
    [InlineData(IntrinsicTimeModeType.TrendReversalChanged)]
    public void LatestItiSignal_RefreshesEveryLiveItiDisplayField(
        IntrinsicTimeModeType mode)
    {
        var valueDate = new DateOnly(2026, 8, 21);
        var snapshot = new MarketOutlookReadModel
        {
            ContractId = "ESU26",
            ValueDate = valueDate,
            UpdatedAtUtc = DateTime.UtcNow,
            LatestItiTrendSignal = new FuturesItiSignalV2ReadModel
            {
                ContractId = "ESU26",
                ValueDate = valueDate,
                TimePeriod = TimeFrameType.Daily,
                IntrinsicTimeMode = mode,
                IntrinsicTimeTrend = IntrinsicTimeTrendType.UpTrend,
                UpTrendTrigger = 6_530.25,
                DownTrendTrigger = 6_480.75,
                TrendExtreme = 6_525.5,
                TrendReversal = 6_490.25,
                TrendDelta = 35.25
            },
            TrendExtremeChange = new FuturesItiSignalV2ReadModel
            {
                ContractId = "ESU26",
                ValueDate = valueDate,
                IntrinsicTimeMode = IntrinsicTimeModeType.TrendExtremeChanged,
                TrendExtreme = 1
            },
            TrendReversalChange = new FuturesItiSignalV2ReadModel
            {
                ContractId = "ESU26",
                ValueDate = valueDate,
                IntrinsicTimeMode = IntrinsicTimeModeType.TrendReversalChanged,
                TrendReversal = 2
            }
        };

        var model = new FuturesTradeSignalUIViewModel(snapshot);

        model.UpTrendLimit.Should().Be("6530.25");
        model.DownLimitTrigger.Should().Be("6480.75");
        model.TrendExtreme.Should().Be("6525.50");
        model.TrendReversal.Should().Be("6490.25");
        model.TrendDelta.Should().Be("35.25");
        model.Trend.Should().Be("UpTrending");
    }

    [Fact]
    public void TypedDailyAnalyticsSupplyEmaAndBollingerDisplayValues()
    {
        var metadata = Metadata();
        var snapshot = new MarketOutlookReadModel
        {
            ContractId = metadata.ContractId,
            ValueDate = metadata.ValueDate,
            UpdatedAtUtc = DateTime.UtcNow,
            FuturesEmaSignal = new FuturesEmaSignalReadModel
            {
                Metadata = metadata,
                Ema50 = 5123.456m,
                Ema200 = 4987.654m,
                IsWarm = true
            },
            FuturesBbSignal = new FuturesBbSignalReadModel
            {
                Metadata = metadata,
                StandardDeviation20 = 22.345m,
                Upper20 = 5200.125m,
                Ema20Center = 5155.435m,
                Lower20 = 5110.745m,
                Position20 = 0.75m,
                IsWarm = true
            },
            LatestItiTrendSignal = new FuturesItiSignalV2ReadModel
            {
                ContractId = metadata.ContractId,
                ValueDate = metadata.ValueDate,
                TimePeriod = TimeFrameType.Daily,
                IntrinsicTimeMode = IntrinsicTimeModeType.Trending,
                IntrinsicTimeTrend = IntrinsicTimeTrendType.UpTrend,
                IntrinsicPrice = 5190,
                TrendDelta = 42.5
            },
            FuturesTdiSignal = new FuturesTdiSignalReadModel
            {
                ContractId = metadata.ContractId,
                ValueDate = metadata.ValueDate,
                TimePeriod = TimeFrameType.FifteenSeconds,
                TDI = FuturesTrendDirectionType.UpTrending,
                TDIStrength = FuturesTrendDirectionStrengthType.High,
                MarketState = FuturesTdiMarketStateType.AboveMidline,
                Cross = FuturesTdiCrossType.Bullish,
                PriceSignalDivergence = 2.345
            }
        };

        var trade = new FuturesTradeSignalUIViewModel(snapshot);
        var eod = new FuturesEodDataUIViewModel(snapshot);

        trade.FiftyDMA.Should().Be("5123.46");
        trade.TwoHundredDMA.Should().Be("4987.65");
        trade.MDITrend.Should().Be("UpTrending");
        trade.Trend.Should().Be("UpTrending");
        trade.TrendDelta.Should().Be("42.50");
        trade.TdiDirection.Should().Be("UpTrending");
        trade.TdiStrength.Should().Be("High");
        trade.TdiMarketState.Should().Be("AboveMidline");
        trade.TdiCross.Should().Be("Bullish");
        trade.TdiDivergence.Should().Be("2.35");
        eod.DailyStdDev.Should().Be("22.35");
        eod.UpperBand.Should().Be("5200.13");
        eod.Mean.Should().Be("5155.44");
        eod.LowerBand.Should().Be("5110.75");
        eod.MDI.Should().Be("75.00");
    }

    [Theory]
    [InlineData(-0.10, "0.00", "DownTrending")]
    [InlineData(0.00, "0.00", "DownTrending")]
    [InlineData(0.2999, "29.99", "DownTrending")]
    [InlineData(0.30, "30.00", "RangeBound")]
    [InlineData(0.5999, "59.99", "RangeBound")]
    [InlineData(0.60, "60.00", "UpTrending")]
    [InlineData(1.00, "100.00", "UpTrending")]
    [InlineData(1.10, "100.00", "UpTrending")]
    public void Mdi_ClampsBollingerPositionAndUsesExactThirtySixtyBoundaries(
        double position,
        string expectedValue,
        string expectedTrend)
    {
        var metadata = Metadata();
        var snapshot = new MarketOutlookReadModel
        {
            ContractId = metadata.ContractId,
            ValueDate = metadata.ValueDate,
            UpdatedAtUtc = DateTime.UtcNow,
            FuturesBbSignal = new FuturesBbSignalReadModel
            {
                Metadata = metadata,
                Position20 = (decimal)position,
                IsWarm = true
            }
        };

        new FuturesEodDataUIViewModel(snapshot).MDI.Should().Be(expectedValue);
        new FuturesTradeSignalUIViewModel(snapshot).MDITrend.Should().Be(expectedTrend);
    }

    static MarketAnalyticsSignalMetadata Metadata()
    {
        var series = MarketSeriesIdentity.ForFuturesSeries(
            new FuturesSeriesId("ES", "calendar-front", "unadjusted", 1));
        var end = new DateTimeOffset(2026, 8, 21, 21, 0, 0, TimeSpan.Zero);
        return new()
        {
            SignalKey = new(series, MarketAnalyticsSignalKind.Ema, TimeFrameType.Daily, "daily-v1"),
            ContractId = "ESU26",
            ValueDate = new(2026, 8, 21),
            ObservationId = FuturesTradeSessionBarId.Create(
                series, TimeFrameType.Daily, end, 1),
            MarketDataAsOfUtc = end,
            CalculatedAtUtc = end,
            SourceSequence = 1,
            SchemaVersion = 1,
            CalculationVersion = "daily-v1",
            CalculationMethod = MarketSignalCalculationMethod.NormalizedHistoricalAggregate,
            IsValid = true
        };
    }
}
