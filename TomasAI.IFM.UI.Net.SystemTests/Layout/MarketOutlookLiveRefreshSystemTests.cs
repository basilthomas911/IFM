using FluentAssertions;
using System.Windows.Forms;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesBbSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesEmaSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.UI.Net.ViewModels.MarketData;
using TomasAI.IFM.UI.Net.Views.App;

namespace TomasAI.IFM.UI.Net.SystemTests.Layout;

public sealed class MarketOutlookLiveRefreshSystemTests
{
    [Fact]
    public async Task ConsecutiveWholeSnapshots_ReplacePriceAnalyticsAndAllFiveItiControls()
    {
        var completion = new TaskCompletionSource<(string[] First, string[] Second)>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                using var view = new MarketOutlookView();
                view.CreateControl();

                Refresh(view, Snapshot(5_100m, 0.01, 5_050m, 5_000m, 20m, 5_140m, 5_100m, 5_060m));
                var first = Capture(view);
                Refresh(view, Snapshot(5_101m, 0.0102, 5_051m, 5_001m, 21m, 5_143m, 5_101m, 5_059m));
                var second = Capture(view);

                completion.SetResult((first, second));
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        var result = await completion.Task.WaitAsync(TimeSpan.FromSeconds(10));
        thread.Join(TimeSpan.FromSeconds(10)).Should().BeTrue();
        result.Second.Should().NotEqual(result.First);
        result.Second.Zip(result.First).Should().OnlyContain(pair => pair.First != pair.Second,
            "each displayed price, Analytics and ITI value must be replaced by the next whole snapshot");
    }

    static void Refresh(MarketOutlookView view, MarketOutlookReadModel snapshot)
    {
        view.RefreshView(new FuturesEodDataUIViewModel(snapshot));
        view.RefreshView(new FuturesTradeSignalUIViewModel(snapshot));
    }

    static string[] Capture(Control view) =>
    [
        Text(view, "txtCloseRT"),
        Text(view, "txtPercentChangeRT"),
        Text(view, "txt50DMA"),
        Text(view, "txt200DMA"),
        Text(view, "txtStdDevRT"),
        Text(view, "txtUpperBandRT"),
        Text(view, "txtMeanRT"),
        Text(view, "txtLowerBandRT"),
        Text(view, "txtUpTrendLimit"),
        Text(view, "txtDownTrendLimit"),
        Text(view, "txtExtremeLimit"),
        Text(view, "txtReversalLimit"),
        Text(view, "txtTrendDelta")
    ];

    static string Text(Control view, string name) =>
        view.Controls.Find(name, true).OfType<TextBox>().Single().Text;

    static MarketOutlookReadModel Snapshot(
        decimal close,
        double percentChange,
        decimal ema50,
        decimal ema200,
        decimal standardDeviation,
        decimal upper,
        decimal center,
        decimal lower)
    {
        var metadata = Metadata();
        return new()
        {
            ContractId = metadata.ContractId,
            ValueDate = metadata.ValueDate,
            UpdatedAtUtc = DateTime.UtcNow,
            FuturesEodData = new FuturesEodDataV2ReadModel(
                metadata.ContractId,
                metadata.ValueDate,
                "ES",
                5_050m,
                5_125m,
                5_025m,
                close,
                10_000,
                percentChange),
            FuturesEmaSignal = new()
            {
                Metadata = metadata,
                Ema20 = center,
                Ema50 = ema50,
                Ema200 = ema200,
                IsWarm = true
            },
            FuturesBbSignal = new()
            {
                Metadata = metadata,
                StandardDeviation20 = standardDeviation,
                Upper20 = upper,
                Ema20Center = center,
                Lower20 = lower,
                Position20 = 0.5m,
                IsWarm = true
            },
            LatestItiTrendSignal = new()
            {
                ContractId = metadata.ContractId,
                ValueDate = metadata.ValueDate,
                TimePeriod = TimeFrameType.Daily,
                IntrinsicTimeMode = IntrinsicTimeModeType.Trending,
                IntrinsicTimeTrend = IntrinsicTimeTrendType.UpTrend,
                UpTrendTrigger = (double)(close + 10m),
                DownTrendTrigger = (double)(close - 10m),
                TrendExtreme = (double)(close + 20m),
                TrendReversal = (double)(close - 20m),
                TrendDelta = (double)(close - 5_000m)
            }
        };
    }

    static MarketAnalyticsSignalMetadata Metadata()
    {
        var series = MarketSeriesIdentity.ForFuturesSeries(
            new FuturesSeriesId("ES", "calendar-front", "unadjusted", 1));
        var end = new DateTimeOffset(2026, 9, 1, 20, 0, 0, TimeSpan.Zero);
        return new()
        {
            SignalKey = new(series, MarketAnalyticsSignalKind.Ema, TimeFrameType.Daily, "daily-v1"),
            ContractId = "ES20260918",
            ValueDate = new(2026, 9, 1),
            ObservationId = FuturesTradeSessionBarId.Create(series, TimeFrameType.Daily, end, 1),
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
