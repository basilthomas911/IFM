using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.Events;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Shared.StatusConsole.ViewModels;
using TomasAI.IFM.UI.EventConsumer;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.UI.Net.Presentation.UnitTests.TestDoubles;
using TomasAI.IFM.UI.Net.ViewModels.App;

namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.ViewModels;

public class IFMAppViewModelTests
{
    [Theory]
    [InlineData(false, "Start Market Feeds")]
    [InlineData(true, "Stop Market Feeds")]
    public void MarketDataFeedActionText_ReflectsFeedLifecycle(
        bool isMarketDataFeedActive,
        string expected)
        => IFMAppViewModel.GetMarketDataFeedActionText(isMarketDataFeedActive)
            .Should().Be(expected);

    [Theory]
    [InlineData(MarketDataFeedHealthState.Inactive, "Feed Health: Stopped")]
    [InlineData(MarketDataFeedHealthState.Healthy, "Feed Health: Green")]
    [InlineData(MarketDataFeedHealthState.Intermittent, "Feed Health: Yellow")]
    [InlineData(MarketDataFeedHealthState.Failed, "Feed Health: Failed")]
    [InlineData(MarketDataFeedHealthState.Critical, "Feed Health: Red")]
    [InlineData(MarketDataFeedHealthState.OutsidePositionEntryWindow, "Feed Health: Monitoring Paused")]
    public void MarketDataFeedHealthIndicatorText_ReflectsHealthState(
        MarketDataFeedHealthState state,
        string expected)
        => IFMAppViewModel.GetMarketDataFeedHealthIndicatorText(state)
            .Should().Be(expected);

    [Fact]
    public void StatusLogState_IsNewestFirstAndBounded()
    {
        var viewModel = CreateSubject();

        for (var index = 0; index < 505; index++)
        {
            viewModel.AppendStatusLog(new StatusConsoleLogReadModel(
                new DateTime(2026, 8, 11).AddSeconds(index),
                0,
                LogSourceType.IFMApp,
                $"message-{index}"));
        }

        viewModel.StatusLogs.Should().HaveCount(500);
        viewModel.StatusLogs[0].Message.Should().Be("message-504");
        viewModel.StatusLogs[^1].Message.Should().Be("message-5");
        viewModel.LatestStatusLog.Should().BeSameAs(viewModel.StatusLogs[0]);
        viewModel.StatusLine.Should().Be("message-504");
    }

    [Fact]
    public void RepeatedErrors_AreDistinctObservableNotifications()
    {
        var viewModel = CreateSubject();
        var changes = new List<string?>();
        viewModel.PropertyChanged += (_, eventArgs) => changes.Add(eventArgs.PropertyName);

        viewModel.PublishError(41, "backend unavailable", "Startup Error");
        var first = viewModel.LastError;
        viewModel.PublishError(41, "backend unavailable", "Startup Error");

        viewModel.LastError!.Sequence.Should().BeGreaterThan(first!.Sequence);
        changes.Count(name => name == nameof(IFMAppViewModel.LastError)).Should().Be(2);
    }

    [Fact]
    public void ShellSurface_IsObservableAndDeclaresNoDelegateCallbacks()
    {
        var viewModel = CreateSubject();

        viewModel.IsMenuEnabled.Should().BeTrue(
            "primary navigation must remain available when no market value date exists");
        viewModel.IsMarketDataFeedActive.Should().BeFalse();
        viewModel.MarketDataFeedHealthState.Should().Be(MarketDataFeedHealthState.Inactive);
        viewModel.IsMarketDataFeedOperationInProgress.Should().BeFalse();
        viewModel.CanToggleMarketDataFeed.Should().BeFalse();
        viewModel.MarketDataFeedActionText.Should().Be("Start Market Feeds");
        viewModel.MarketDataFeedHealthIndicatorText.Should().Be("Feed Health: Stopped");
        viewModel.MarketDataFeedStateText.Should().Be("Market Feed: Inactive");
        viewModel.IsCloseRequested.Should().BeFalse();
        viewModel.StartupOperation.Should().NotBeNull();
        viewModel.ShutdownOperation.Should().NotBeNull();
        viewModel.MarketOutlook.Should().BeNull();
        viewModel.FuturesBarSnapshots.Should().BeEmpty();
        viewModel.FuturesTradeSignal.Should().BeNull();
        viewModel.LatestTradePlacement.Should().BeNull();
        viewModel.TradePlacements.Should().BeEmpty();
        viewModel.MarketDataStreamMetrics.MarketOutlook.IsOpen.Should().BeFalse();
        viewModel.MarketDataStreamMetrics.FuturesBars.Should().BeEmpty();
        viewModel.RealtimeStreamMetrics.FuturesTradeSignals.IsOpen.Should().BeFalse();
        viewModel.RealtimeStreamMetrics.TradePlacements.IsOpen.Should().BeFalse();
        viewModel.RealtimeStreamMetrics.StatusConsole.IsOpen.Should().BeFalse();
        typeof(IIFMAppLiveViewAdapter).GetMethod("UpdateMarketOutlook").Should().BeNull();
        typeof(IIFMAppLiveViewAdapter).GetMethod("UpdateMarketData").Should().BeNull();
        typeof(IIFMAppLiveViewAdapter).GetMethod("UpdateTradeSignal").Should().BeNull();
        typeof(IIFMAppLiveViewAdapter).GetMethod("NotifyTradePlacement").Should().BeNull();
        typeof(IFMAppViewModel)
            .GetFields(System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.DeclaredOnly)
            .Where(field => typeof(Delegate).IsAssignableFrom(field.FieldType))
            .Should().BeEmpty();
    }

    [Fact]
    public void RealtimeSnapshots_AreObservableOrderedAndBounded()
    {
        var viewModel = CreateSubject();
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, eventArgs) => changed.Add(eventArgs.PropertyName);

        viewModel.PublishFuturesTradeSignal(new FuturesTradeSignalV2ReadModel
        {
            ContractId = "ESZ26",
            ValueDate = new DateOnly(2026, 8, 11),
            RSI = 52.5
        });
        var placements = Enumerable.Range(0, 505)
            .Select(index => (index % 3) switch
            {
                0 => (TomasAI.IFM.Shared.EventSourcing.IEvent)new TradePlacementSetEvent(),
                1 => new TradePlacementWaitEvent(),
                _ => new TradePlacementClearedEvent()
            })
            .ToArray();
        viewModel.PublishTradePlacementBatch(placements);

        viewModel.FuturesTradeSignal!.ContractId.Should().Be("ESZ26");
        viewModel.FuturesTradeSignal.RSI.Should().Be("52.50");
        viewModel.TradePlacements.Should().HaveCount(500);
        viewModel.LatestTradePlacement.Should().BeSameAs(viewModel.TradePlacements[0]);
        viewModel.LatestTradePlacement!.PlaceTrade.Should().Be("Yes");
        changed.Should().Contain(nameof(IFMAppViewModel.FuturesTradeSignal));
        changed.Should().Contain(nameof(IFMAppViewModel.TradePlacements));
        changed.Should().Contain(nameof(IFMAppViewModel.LatestTradePlacement));
    }

    [Fact]
    public void StatusLogBatch_PreservesNewestFirstDisplayOrder()
    {
        var viewModel = CreateSubject();
        var received = new DateTime(2026, 8, 11, 9, 30, 0);
        var logs = Enumerable.Range(0, 3)
            .Select(index => new StatusConsoleLogReadModel(
                received.AddSeconds(index),
                0,
                LogSourceType.IFMApp,
                $"message-{index}"))
            .ToArray();

        viewModel.AppendStatusLogs(logs);

        viewModel.StatusLogs.Select(log => log.Message)
            .Should().Equal("message-2", "message-1", "message-0");
        viewModel.LatestStatusLog.Should().BeSameAs(logs[2]);
        viewModel.StatusLine.Should().Be("message-2");
    }

    [Fact]
    public void MarketDataSnapshots_AreObservableSortedAndBoundedPerSymbol()
    {
        var marketCurrentTime = new DateTimeOffset(2026, 8, 11, 16, 0, 0, TimeSpan.Zero);
        var viewModel = CreateSubject(new ManualTimeProvider(marketCurrentTime));
        var valueDate = new DateOnly(2026, 8, 11);
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, eventArgs) => changed.Add(eventArgs.PropertyName);
        var firstBarDate = marketCurrentTime.UtcDateTime.AddSeconds(-(2_055 - 1) * 10);
        var bars = Enumerable.Range(0, 2_055)
            .Select(index => new FuturesBarDataReadModel(
                "ESZ26",
                "ES",
                valueDate,
                firstBarDate.AddSeconds(index * 10),
                BarRateType.FifteenSeconds,
                5_000m + index,
                5_100,
                4_900))
            .Reverse();

        viewModel.PublishMarketOutlook(new FuturesEodDataV2ReadModel(
            "ESZ26",
            valueDate,
            "ES",
            5_000m,
            5_100m,
            4_900m,
            5_050m,
            1_000));
        viewModel.PublishFuturesBarSnapshot("ES", bars);

        viewModel.MarketOutlook.Should().NotBeNull();
        viewModel.MarketOutlook!.ClosePrice.Should().Be("5050.00");
        viewModel.FuturesBarSnapshots["ES"].Should().HaveCount(2_048);
        viewModel.FuturesBarSnapshots["ES"].Should().BeInAscendingOrder(bar => bar.BarDate);
        viewModel.LatestFuturesBarSnapshot!.Symbol.Should().Be("ES");
        viewModel.LatestFuturesBarSnapshot.Bars.Should().BeSameAs(viewModel.FuturesBarSnapshots["ES"]);
        changed.Should().Contain(nameof(IFMAppViewModel.MarketOutlook));
        changed.Should().Contain(nameof(IFMAppViewModel.FuturesBarSnapshots));
        changed.Should().Contain(nameof(IFMAppViewModel.LatestFuturesBarSnapshot));
    }

    [Fact]
    public void MarketOutlookUpdate_AcceptsOnlyTheActiveEsContract()
    {
        var valueDate = new DateOnly(2026, 8, 11);
        var es = new FuturesEodDataV2ReadModel(
            "ESZ26", valueDate, "ES", 5_000m, 5_100m, 4_900m, 5_050m, 1_000);
        var staleEs = es with { ContractId = "ESU26" };
        var nq = es with { ContractId = "NQZ26", Symbol = "NQ" };

        IFMAppViewModel.IsMarketOutlookUpdate("ESZ26", es).Should().BeTrue();
        IFMAppViewModel.IsMarketOutlookUpdate("ESZ26", staleEs).Should().BeFalse();
        IFMAppViewModel.IsMarketOutlookUpdate("ESZ26", nq).Should().BeFalse();
        IFMAppViewModel.IsMarketOutlookUpdate(null, es).Should().BeFalse();
    }

    [Fact]
    public async Task MarketOutlookSnapshot_RefreshesPercentageAndRejectsStaleRevision()
    {
        var viewModel = CreateSubject();
        var valueDate = new DateOnly(2026, 8, 11);
        var source = new FuturesEodDataV2ReadModel(
            "ESZ26",
            valueDate,
            "ES",
            5400m,
            5500m,
            5350m,
            5425m,
            1_000,
            0.0046,
            priceDirection: PriceDirectionType.Rising);

        await viewModel.ProcessMarketOutlookSnapshotAsync(
            Snapshot(2, source),
            CancellationToken.None);
        await viewModel.ProcessMarketOutlookSnapshotAsync(
            Snapshot(1, source with
            {
                ClosePrice = 5375m,
                DailyPercentChange = -0.0046,
                PriceDirection = PriceDirectionType.Falling
            }),
            CancellationToken.None);

        viewModel.MarketOutlook.Should().NotBeNull();
        viewModel.MarketOutlook!.ClosePrice.Should().Be("5425.00");
        viewModel.MarketOutlook.DailyPercentChange.Should().Be($"{0.0046:P2}");

        await viewModel.ProcessMarketOutlookSnapshotAsync(
            Snapshot(3, source with
            {
                ClosePrice = 5375m,
                DailyPercentChange = -0.0046,
                PriceDirection = PriceDirectionType.Falling
            }),
            CancellationToken.None);

        viewModel.MarketOutlook.ClosePrice.Should().Be("5375.00");
        viewModel.MarketOutlook.DailyPercentChange.Should().Be($"{-0.0046:P2}");

        MarketOutlookSnapshotReadModel Snapshot(
            long revision,
            FuturesEodDataV2ReadModel eod) => new()
            {
                ContractId = eod.ContractId,
                ValueDate = eod.ValueDate,
                Revision = revision,
                UpdatedOn = DateTime.UtcNow,
                FuturesEodData = eod,
                MissingInputs = "RSI"
            };
    }

    [Fact]
    public void MarketDataSnapshots_KeepOnlyNewestContinuousFifteenSecondSegment()
    {
        var marketCurrentTime = new DateTimeOffset(2026, 8, 11, 16, 0, 0, TimeSpan.Zero);
        var viewModel = CreateSubject(new ManualTimeProvider(marketCurrentTime));
        var valueDate = DateOnly.FromDateTime(marketCurrentTime.UtcDateTime);
        FuturesBarDataReadModel Bar(DateTime barDate, BarRateType rateType, decimal value) => new(
            "ESZ26",
            "ES",
            valueDate,
            barDate,
            rateType,
            value,
            0,
            0);
        FuturesBarDataReadModel[] bars =
        [
            Bar(marketCurrentTime.UtcDateTime.AddHours(-7), BarRateType.FifteenSeconds, 4_900m),
            Bar(marketCurrentTime.UtcDateTime.AddHours(-5), BarRateType.FifteenSeconds, 5_000m),
            Bar(marketCurrentTime.UtcDateTime.AddHours(-5).AddSeconds(15), BarRateType.FifteenSeconds, 5_001m),
            Bar(marketCurrentTime.UtcDateTime.AddMinutes(-1), BarRateType.Minute, 5_100m),
            Bar(marketCurrentTime.UtcDateTime.AddSeconds(-30), BarRateType.FifteenSeconds, 5_200m),
            Bar(marketCurrentTime.UtcDateTime.AddSeconds(-15), BarRateType.FifteenSeconds, 5_201m),
            Bar(marketCurrentTime.UtcDateTime, BarRateType.FifteenSeconds, 5_202m)
        ];

        viewModel.PublishFuturesBarSnapshot("ES", bars);

        viewModel.FuturesBarSnapshots["ES"]
            .Select(bar => bar.BarValue)
            .Should().Equal(5_200m, 5_201m, 5_202m);
    }

    [Fact]
    public void FuturesBarChartWindow_IsSixHoursBackFromMarketCurrentTime()
    {
        var marketCurrentTime = new DateTime(2026, 8, 11, 16, 0, 0, DateTimeKind.Utc);

        var (startDate, endDate) = IFMAppViewModel.GetFuturesBarChartWindow(marketCurrentTime);

        startDate.Should().Be(marketCurrentTime.AddHours(-6));
        endDate.Should().Be(marketCurrentTime.AddSeconds(1));
    }

    [Fact]
    public void UiDispatchMetrics_RecordLastAndMaximumLatency()
    {
        var viewModel = CreateSubject();

        viewModel.RecordUiDispatch(TimeSpan.FromMilliseconds(9), TimeSpan.FromMilliseconds(2));
        viewModel.RecordUiDispatch(TimeSpan.FromMilliseconds(3), TimeSpan.FromMilliseconds(6));

        viewModel.UiDispatchMetrics.DispatchCount.Should().Be(2);
        viewModel.UiDispatchMetrics.LastDispatchDelay.Should().Be(TimeSpan.FromMilliseconds(3));
        viewModel.UiDispatchMetrics.MaximumDispatchDelay.Should().Be(TimeSpan.FromMilliseconds(9));
        viewModel.UiDispatchMetrics.LastRenderDuration.Should().Be(TimeSpan.FromMilliseconds(6));
        viewModel.UiDispatchMetrics.MaximumRenderDuration.Should().Be(TimeSpan.FromMilliseconds(6));
    }

    static IFMAppViewModel CreateSubject(TimeProvider? timeProvider = null)
    {
        var commandResponseConsumer = Substitute.For<ICommandResponseUIEventConsumer>();
        var eventModel = new CommandResponseEventService(commandResponseConsumer);
        var appRoot = Substitute.For<IAppRoot>();
        appRoot.Services.CommandResponses.Returns(eventModel);
        return new IFMAppViewModel(
            appRoot,
            new Version(1, 2, 3),
            "Test",
            Substitute.For<IIFMAppLiveViewAdapter>(),
            Substitute.For<IEconomicCalendarService>(),
            timeProvider);
    }
}
