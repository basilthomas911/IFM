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
using TomasAI.IFM.UI.Net.ViewModels.Operations;

namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.ViewModels;

public sealed class StrategyOperationsViewModelTests
{
    const string ContractId = "ESZ26";
    static readonly DateOnly ValueDate = new(2026, 8, 21);

    [Fact]
    public async Task Initialize_SubscribesBeforeSnapshotsAndMergesOverlap()
    {
        var daily = Signal(TimeFrameType.Daily, 1, IntrinsicTimeModeType.Trending);
        var weekly = Signal(TimeFrameType.Weekly, 2, IntrinsicTimeModeType.TrendDirectionChanged);
        var monthly = Signal(TimeFrameType.Monthly, 3, IntrinsicTimeModeType.TrendExtremeChanged);
        var subject = CreateSubject();
        subject.QueryApi.GetFuturesItiSignalAsync(ContractId, ValueDate, TimeFrameType.Daily)
            .Returns(_ =>
            {
                subject.EventSource.Publish(daily);
                return Task.FromResult<ServiceResult<FuturesItiSignalV2ReadModel>>(
                    new ServiceOk<FuturesItiSignalV2ReadModel>(daily));
            });
        subject.QueryApi.GetFuturesItiSignalAsync(ContractId, ValueDate, TimeFrameType.Weekly)
            .Returns(Task.FromResult<ServiceResult<FuturesItiSignalV2ReadModel>>(
                new ServiceOk<FuturesItiSignalV2ReadModel>(weekly)));
        subject.QueryApi.GetFuturesItiSignalAsync(ContractId, ValueDate, TimeFrameType.Monthly)
            .Returns(Task.FromResult<ServiceResult<FuturesItiSignalV2ReadModel>>(
                new ServiceOk<FuturesItiSignalV2ReadModel>(monthly)));

        await subject.ViewModel.InitializeAsync(CancellationToken.None);

        subject.ViewModel.IsListening.Should().BeTrue();
        subject.ViewModel.Events.Should().HaveCount(3);
        subject.ViewModel.Events.Select(row => row.TimePeriod)
            .Should().BeEquivalentTo(
                [TimeFrameType.Daily, TimeFrameType.Weekly, TimeFrameType.Monthly]);
        subject.ViewModel.Events.Single(row => row.TimePeriod == TimeFrameType.Daily)
            .IsInitialSnapshot.Should().BeFalse("the live overlap arrived after subscription and won deduplication");
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

        subject.ViewModel.Events.Select(row => row.Mode)
            .Should().BeEquivalentTo(modes);

        await subject.ViewModel.StopAsync(CancellationToken.None);
        subject.EventSource.Publish(Signal(
            TimeFrameType.Daily,
            100,
            IntrinsicTimeModeType.TrendDirectionChanged));

        subject.ViewModel.Events.Should().HaveCount(modes.Length);
        subject.ViewModel.IsListening.Should().BeFalse();
        subject.EventSource.IsStarted.Should().BeFalse();
        await subject.ViewModel.DisposeAsync();
    }

    [Fact]
    public async Task Listener_FiltersContextDeduplicatesAndBoundsHistory()
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
        for (var sequence = 4; sequence <= StrategyOperationsViewModel.EventCapacity + 10; sequence++)
        {
            subject.EventSource.Publish(Signal(
                TimeFrameType.Daily,
                sequence,
                IntrinsicTimeModeType.PredictedIntervalChanged));
        }

        subject.ViewModel.Events.Should().HaveCount(StrategyOperationsViewModel.EventCapacity);
        subject.ViewModel.Events.Should().OnlyContain(row =>
            row.ContractId == ContractId
            && row.ValueDate == ValueDate
            && row.TimePeriod == TimeFrameType.Daily);
        subject.ViewModel.Events.Select(row => row.SequenceId)
            .Should().BeInDescendingOrder();
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

    static Subject CreateSubject()
    {
        var queryApi = Substitute.For<IMarketDataAnalyticsQueryApi>();
        queryApi.GetFuturesItiSignalAsync(
                Arg.Any<string>(),
                Arg.Any<DateOnly>(),
                Arg.Any<TimeFrameType>())
            .Returns(Task.FromResult<ServiceResult<FuturesItiSignalV2ReadModel>>(
                new ServiceOk<FuturesItiSignalV2ReadModel>(null!)));

        var consumer = Substitute.For<IFuturesItiSignalUIEventConsumer>();
        var eventSource = new TestEventSource(consumer);
        var model = new StrategyOperationsModel(queryApi, consumer);
        return new Subject(
            new StrategyOperationsViewModel(model, ContractId, ValueDate),
            queryApi,
            eventSource);
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
