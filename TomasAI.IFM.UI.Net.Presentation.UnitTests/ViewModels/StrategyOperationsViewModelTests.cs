using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.EventConsumer;
using TomasAI.IFM.UI.Net.Models.Operations;
using TomasAI.IFM.UI.Net.Presentation.UnitTests.TestDoubles;
using TomasAI.IFM.UI.Net.ViewModels.Operations;

namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.ViewModels;

public sealed class StrategyOperationsViewModelTests
{
    const string ContractId = "ESZ26";
    static readonly DateOnly ValueDate = new(2026, 8, 21);

    [Fact]
    public async Task Initialize_SubscribesBeforeHistoryAndPublishesCompleteSelectedTimeFrame()
    {
        var daily = Signal(TimeFrameType.Daily, 1, IntrinsicTimeModeType.Trending);
        var dailyDirection = Signal(TimeFrameType.Daily, 4, IntrinsicTimeModeType.TrendDirectionChanged);
        var weekly = Signal(TimeFrameType.Weekly, 2, IntrinsicTimeModeType.TrendDirectionChanged);
        var monthly = Signal(TimeFrameType.Monthly, 3, IntrinsicTimeModeType.TrendExtremeChanged);
        var subject = CreateSubject();
        subject.QueryApi.GetFuturesItiSignalHistoryAsync(ContractId, ValueDate, TimeFrameType.Daily)
            .Returns(_ =>
            {
                subject.EventSource.Publish(daily);
                return Task.FromResult<ServiceResult<FuturesItiSignalV2ReadModel[]>>(
                    new ServiceOk<FuturesItiSignalV2ReadModel[]>([daily, dailyDirection]));
            });
        subject.QueryApi.GetFuturesItiSignalHistoryAsync(ContractId, ValueDate, TimeFrameType.Weekly)
            .Returns(Task.FromResult<ServiceResult<FuturesItiSignalV2ReadModel[]>>(
                new ServiceOk<FuturesItiSignalV2ReadModel[]>([weekly])));
        subject.QueryApi.GetFuturesItiSignalHistoryAsync(ContractId, ValueDate, TimeFrameType.Monthly)
            .Returns(Task.FromResult<ServiceResult<FuturesItiSignalV2ReadModel[]>>(
                new ServiceOk<FuturesItiSignalV2ReadModel[]>([monthly])));

        await subject.ViewModel.InitializeAsync(CancellationToken.None);

        subject.ViewModel.IsListening.Should().BeTrue();
        subject.ViewModel.TimeFrames.Should().Equal(
            TimeFrameType.Daily,
            TimeFrameType.Weekly,
            TimeFrameType.Monthly);
        subject.ViewModel.SelectedTimeFrame.Should().Be(TimeFrameType.Daily);
        subject.ViewModel.StatusText.Should().StartWith("Intrinsic Time Daily:");
        subject.ViewModel.Events.Should().HaveCount(2)
            .And.OnlyContain(row => row.TimePeriod == TimeFrameType.Daily);
        subject.ViewModel.Events.Single(row => row.SequenceId == daily.SequenceId)
            .IsHistorical.Should().BeFalse("the live overlap arrived after subscription and won deduplication");
        subject.ViewModel.Events.Single(row => row.SequenceId == dailyDirection.SequenceId)
            .IsHistorical.Should().BeTrue();

        subject.ViewModel.SelectedTimeFrame = TimeFrameType.Weekly;
        subject.ViewModel.StatusText.Should().StartWith("Intrinsic Time Weekly:");
        subject.ViewModel.Events.Should().ContainSingle()
            .Which.TimePeriod.Should().Be(TimeFrameType.Weekly);
        subject.ViewModel.SelectedTimeFrame = TimeFrameType.Monthly;
        subject.ViewModel.Events.Should().ContainSingle()
            .Which.TimePeriod.Should().Be(TimeFrameType.Monthly);
        await subject.ViewModel.DisposeAsync();
    }

    [Fact]
    public async Task Listener_PublishesEveryItiModeAndStopsCleanly()
    {
        var subject = CreateSubject();
        await subject.ViewModel.InitializeAsync(CancellationToken.None);

        var modes = Enum.GetValues<IntrinsicTimeModeType>();
        for (var index = 0; index < modes.Length; index++)
        {
            subject.EventSource.Publish(Signal(
                (TimeFrameType)((index % 3) + (int)TimeFrameType.Daily),
                index + 1,
                modes[index]));
        }

        foreach (var timeFrame in subject.ViewModel.TimeFrames)
        {
            subject.ViewModel.SelectedTimeFrame = timeFrame;
            var expectedModes = modes
                .Where((_, index) =>
                    (TimeFrameType)((index % 3) + (int)TimeFrameType.Daily) == timeFrame);
            subject.ViewModel.Events.Select(row => row.Mode)
                .Should().BeEquivalentTo(expectedModes);
            subject.ViewModel.Events.Should().OnlyContain(row => row.TimePeriod == timeFrame);
        }

        subject.ViewModel.SelectedTimeFrame = TimeFrameType.Daily;
        var retainedDailyCount = subject.ViewModel.Events.Count;
        await subject.ViewModel.StopAsync(CancellationToken.None);
        subject.EventSource.Publish(Signal(
            TimeFrameType.Daily,
            100,
            IntrinsicTimeModeType.TrendDirectionChanged));

        subject.ViewModel.Events.Should().HaveCount(retainedDailyCount);
        subject.ViewModel.IsListening.Should().BeFalse();
        subject.EventSource.IsStarted.Should().BeFalse();
        await subject.ViewModel.DisposeAsync();
    }

    [Fact]
    public async Task Listener_FiltersContextDeduplicatesAndRetainsCompleteHistory()
    {
        var subject = CreateSubject();
        await subject.ViewModel.InitializeAsync(CancellationToken.None);

        subject.EventSource.Publish(Signal(TimeFrameType.OneMinute, 1, IntrinsicTimeModeType.Trending));
        subject.EventSource.Publish(Signal(TimeFrameType.Daily, 2, IntrinsicTimeModeType.Trending) with
        {
            ContractId = "NQZ26"
        });

        var duplicate = Signal(TimeFrameType.Daily, 3, IntrinsicTimeModeType.Trending);
        subject.EventSource.Publish(duplicate);
        subject.EventSource.Publish(duplicate);
        for (var sequence = 4; sequence <= 520; sequence++)
        {
            subject.EventSource.Publish(Signal(
                TimeFrameType.Daily,
                sequence,
                IntrinsicTimeModeType.PredictedIntervalChanged));
        }

        subject.ViewModel.Events.Should().HaveCount(518);
        subject.ViewModel.Events.Should().OnlyContain(row =>
            row.ContractId == ContractId
            && row.ValueDate == ValueDate
            && row.TimePeriod == TimeFrameType.Daily);
        subject.ViewModel.Events.Select(row => row.SequenceId)
            .Should().BeInDescendingOrder();
        await subject.ViewModel.DisposeAsync();
    }

    [Fact]
    public async Task Reconciliation_RecoversEveryPointAfterNotificationGap()
    {
        var timeProvider = new ManualTimeProvider(
            new DateTimeOffset(2026, 8, 21, 14, 0, 0, TimeSpan.Zero));
        var interval = TimeSpan.FromMinutes(1);
        var missed = Signal(
            TimeFrameType.Daily,
            10,
            IntrinsicTimeModeType.TrendExtremeChanged);
        var authoritativeHead = Signal(
            TimeFrameType.Daily,
            11,
            IntrinsicTimeModeType.TrendDirectionChanged);
        var dailyHistoryCalls = 0;
        var subject = CreateSubject(timeProvider, interval);
        subject.QueryApi.GetFuturesItiSignalHistoryAsync(
                ContractId,
                ValueDate,
                TimeFrameType.Daily)
            .Returns(_ => Task.FromResult<ServiceResult<FuturesItiSignalV2ReadModel[]>>(
                new ServiceOk<FuturesItiSignalV2ReadModel[]>(
                    Interlocked.Increment(ref dailyHistoryCalls) == 1
                        ? []
                        : [missed, authoritativeHead])));
        subject.QueryApi.GetFuturesItiSignalAsync(
                ContractId,
                ValueDate,
                TimeFrameType.Daily)
            .Returns(Task.FromResult<ServiceResult<FuturesItiSignalV2ReadModel>>(
                new ServiceOk<FuturesItiSignalV2ReadModel>(authoritativeHead)));

        await subject.ViewModel.InitializeAsync(CancellationToken.None);
        subject.ViewModel.Events.Should().BeEmpty();

        timeProvider.Advance(interval);
        await WaitUntilAsync(() => subject.ViewModel.Events.Count == 2);

        subject.ViewModel.Events.Select(row => row.SequenceId).Should().Equal(11, 10);
        subject.ViewModel.Events.Should().OnlyContain(row => row.IsHistorical);
        dailyHistoryCalls.Should().Be(2, "the changed head triggers one bounded history catch-up");
        await subject.ViewModel.DisposeAsync();
    }

    [Fact]
    public async Task Reconciliation_DoesNotReloadHistoryAfterLiveNotificationArrives()
    {
        var timeProvider = new ManualTimeProvider(
            new DateTimeOffset(2026, 8, 21, 14, 0, 0, TimeSpan.Zero));
        var interval = TimeSpan.FromMinutes(1);
        var live = Signal(
            TimeFrameType.Daily,
            12,
            IntrinsicTimeModeType.TrendReversalChanged);
        var dailyHistoryCalls = 0;
        var currentCalls = 0;
        var subject = CreateSubject(timeProvider, interval);
        subject.QueryApi.GetFuturesItiSignalHistoryAsync(
                ContractId,
                ValueDate,
                TimeFrameType.Daily)
            .Returns(_ =>
            {
                Interlocked.Increment(ref dailyHistoryCalls);
                return Task.FromResult<ServiceResult<FuturesItiSignalV2ReadModel[]>>(
                    new ServiceOk<FuturesItiSignalV2ReadModel[]>([]));
            });
        subject.QueryApi.GetFuturesItiSignalAsync(
                ContractId,
                ValueDate,
                TimeFrameType.Daily)
            .Returns(_ =>
            {
                Interlocked.Increment(ref currentCalls);
                return Task.FromResult<ServiceResult<FuturesItiSignalV2ReadModel>>(
                    new ServiceOk<FuturesItiSignalV2ReadModel>(live));
            });

        await subject.ViewModel.InitializeAsync(CancellationToken.None);
        subject.EventSource.Publish(live);
        subject.ViewModel.Events.Should().ContainSingle()
            .Which.IsHistorical.Should().BeFalse();

        timeProvider.Advance(interval);
        await WaitUntilAsync(() => Volatile.Read(ref currentCalls) == 1);

        dailyHistoryCalls.Should().Be(1, "the live notification already supplied the authoritative head");
        subject.ViewModel.Events.Should().ContainSingle();
        await subject.ViewModel.DisposeAsync();
    }

    [Fact]
    public async Task Operations_DefaultsToStrategyAndAllowsEveryTab()
    {
        var subject = CreateSubject();
        var operations = new OperationsViewModel(subject.ViewModel);

        operations.SelectedView.Should().Be(OperationsViewType.Strategy);
        foreach (var view in Enum.GetValues<OperationsViewType>())
        {
            operations.SelectView(view);
            operations.SelectedView.Should().Be(view);
        }

        await operations.DisposeAsync();
    }

    static Subject CreateSubject(
        TimeProvider? timeProvider = null,
        TimeSpan? reconciliationInterval = null)
    {
        var queryApi = Substitute.For<IMarketDataAnalyticsQueryApi>();
        queryApi.GetFuturesItiSignalAsync(
                Arg.Any<string>(),
                Arg.Any<DateOnly>(),
                Arg.Any<TimeFrameType>())
            .Returns(Task.FromResult<ServiceResult<FuturesItiSignalV2ReadModel>>(
                new ServiceOk<FuturesItiSignalV2ReadModel>(new())));
        queryApi.GetFuturesItiSignalHistoryAsync(
                Arg.Any<string>(),
                Arg.Any<DateOnly>(),
                Arg.Any<TimeFrameType>())
            .Returns(Task.FromResult<ServiceResult<FuturesItiSignalV2ReadModel[]>>(
                new ServiceOk<FuturesItiSignalV2ReadModel[]>([])));

        var consumer = Substitute.For<IFuturesItiSignalUIEventConsumer>();
        var eventSource = new TestEventSource(consumer);
        var model = new StrategyOperationsService(queryApi, consumer);
        return new Subject(
            new StrategyOperationsViewModel(
                model,
                ContractId,
                ValueDate,
                timeProvider,
                reconciliationInterval),
            queryApi,
            eventSource);
    }

    static async Task WaitUntilAsync(Func<bool> condition)
    {
        var timeout = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow >= timeout)
                throw new TimeoutException("The expected reconciled presentation state was not published.");
            await Task.Delay(10);
        }
    }

    static FuturesItiSignalV2ReadModel Signal(
        TimeFrameType period,
        long sequence,
        IntrinsicTimeModeType mode)
        => new()
        {
            ContractId = ContractId,
            ValueDate = ValueDate,
            TimePeriod = period,
            SequenceId = sequence,
            IntrinsicTime = new DateTime(2026, 8, 21, 13, 30, 0, DateTimeKind.Utc)
                .AddSeconds(sequence),
            IntrinsicTimeGroupId = 1,
            IntrinsicTimeLength = sequence,
            IntrinsicPrice = 6500 + sequence,
            IntrinsicTimeTrend = sequence % 2 == 0
                ? IntrinsicTimeTrendType.DownTrend
                : IntrinsicTimeTrendType.UpTrend,
            IntrinsicTimeMode = mode,
            TrendPrice = 6500,
            TrendExtreme = 6520,
            TrendReversal = 6480,
            TrendDelta = 20,
            TargetDelta = 30,
            TradingDays = 20,
            Threshold = 5,
            UpTrendTrigger = 6505,
            DownTrendTrigger = 6495,
            TimeFrameStartValueDate = ValueDate
        };

    sealed record Subject(
        StrategyOperationsViewModel ViewModel,
        IMarketDataAnalyticsQueryApi QueryApi,
        TestEventSource EventSource);

    sealed class TestEventSource
    {
        Action<FuturesItiSignalUpdatedNotifyEvent>? _eventAction;

        public TestEventSource(IFuturesItiSignalUIEventConsumer consumer)
        {
            consumer.StartAsync(
                    Arg.Any<Guid>(),
                    Arg.Any<Action<FuturesItiSignalUpdatedNotifyEvent>>())
                .Returns(call =>
                {
                    _eventAction = call.ArgAt<Action<FuturesItiSignalUpdatedNotifyEvent>>(1);
                    IsStarted = true;
                    return ValueTask.CompletedTask;
                });
            consumer.StopAsync(Arg.Any<Guid>()).Returns(_ =>
            {
                IsStarted = false;
                return ValueTask.CompletedTask;
            });
        }

        public bool IsStarted { get; private set; }

        public void Publish(FuturesItiSignalV2ReadModel signal)
            => (_eventAction ?? throw new InvalidOperationException("Listener not started."))(
                new FuturesItiSignalUpdatedNotifyEvent
                {
                    Subject = new ActorSubject(
                        ActorType.Notify,
                        FuturesItiSignalUpdatedNotifyEvent.Actor,
                        FuturesItiSignalUpdatedNotifyEvent.Verb,
                        signal.EntityId.Format()),
                    Id = Guid.NewGuid(),
                    SourceEventId = Guid.NewGuid(),
                    EntityId = signal.EntityId,
                    CommandId = Guid.NewGuid(),
                    ReceivedOn = signal.IntrinsicTime,
                    FuturesItiSignal = signal
                });
    }
}
