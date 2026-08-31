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
        var snapshot = new MarketOutlookSnapshotReadModel
        {
            ContractId = "ESU26",
            ValueDate = new DateOnly(2026, 8, 21),
            Revision = 1,
            UpdatedOn = DateTime.UtcNow,
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

    [Fact]
    public void PartialSnapshot_UsesEachItiModeOnlyForItsOwnDisplayField()
    {
        var valueDate = new DateOnly(2026, 8, 21);
        var snapshot = new MarketOutlookSnapshotReadModel
        {
            ContractId = "ESU26",
            ValueDate = valueDate,
            Revision = 2,
            UpdatedOn = DateTime.UtcNow,
            TrendExtremeChange = new FuturesItiSignalV2ReadModel
            {
                ContractId = "ESU26",
                ValueDate = valueDate,
                IntrinsicTimeMode = IntrinsicTimeModeType.TrendExtremeChanged,
                TrendExtreme = 6_525.5
            }
        };

        var model = new FuturesTradeSignalUIViewModel(snapshot);

        model.TrendExtreme.Should().Be("6525.50");
        model.Trend.Should().Be("N/A");
        model.TrendReversal.Should().Be("N/A");
    }

    [Fact]
    public void TypedDailyAnalyticsSupplyEmaAndBollingerDisplayValues()
    {
        var metadata = Metadata();
        var snapshot = new MarketOutlookSnapshotReadModel
        {
            ContractId = metadata.ContractId,
            ValueDate = metadata.ValueDate,
            Revision = 3,
            UpdatedOn = DateTime.UtcNow,
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
                IsWarm = true
            }
        };

        var trade = new FuturesTradeSignalUIViewModel(snapshot);
        var eod = new FuturesEodDataUIViewModel(snapshot);

        trade.FiftyDMA.Should().Be("5123.46");
        trade.TwoHundredDMA.Should().Be("4987.65");
        eod.DailyStdDev.Should().Be("22.35");
        eod.UpperBand.Should().Be("5200.13");
        eod.Mean.Should().Be("5155.44");
        eod.LowerBand.Should().Be("5110.75");
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
