using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.EventConsumer;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.UI.Net.ViewModels.App;
using TomasAI.IFM.UI.Net.ViewModels.Operations;

namespace TomasAI.IFM.UI.Presentation.UnitTests.ViewModels;

public class StatusConsoleViewModelTests
{
    static readonly DateOnly ValueDate = new(2026, 8, 11);

    [Fact]
    public async Task LoadOperations_PublishCoherentTradeAndRatioSnapshots()
    {
        var initialSignal = Signal(1, IntrinsicTimeTrendType.UpTrend);
        var subject = CreateSubject(
            new ServiceOk<FuturesItiSignalV2ReadModel[]>([initialSignal]),
            new ServiceOk<MDIForwardLossRatioReadModel[]>([Ratio(10, IntrinsicTimeTrendType.UpTrend)]),
            new ServiceOk<MDIForwardLossRatioReadModel[]>([Ratio(20, IntrinsicTimeTrendType.DownTrend)]));

        await subject.ViewModel.LoadTradeStatusOperation.ExecuteAsync();
        await subject.ViewModel.LoadMDIForwardLossRatiosOperation.ExecuteAsync();

        subject.ViewModel.TradeSignals.Should().Equal(initialSignal);
        subject.ViewModel.TradeStatus.TradeStatus.Should().Be("No Trade Entry");
        subject.ViewModel.MDIForwardLossRatios.Select(value => value.MDI)
            .Should().Equal("MDI >= 10", "MDI >= 20");
        await subject.AnalyticsApi.Received(1)
            .GetFuturesItiTrendDirectionChangedSignalsAsync("ESZ26", ValueDate, TimeFrameType.Weekly);
    }

    [Fact]
    public async Task Listener_PublishesEventsAndIgnoresEventsAfterStop()
    {
        var subject = CreateSubject();
        var direction = Signal(2, IntrinsicTimeTrendType.UpTrend);
        var extreme = direction with
        {
            SequenceId = 3,
            IntrinsicTimeMode = IntrinsicTimeModeType.TrendExtremeChanged
        };

        await subject.ViewModel.InitializeAsync(CancellationToken.None);
        subject.EventSource.PublishDirection(direction);
        subject.EventSource.PublishExtreme(extreme);
        subject.EventSource.PublishTradeSignal(new FuturesTradeSignalV2ReadModel
        {
            ContractId = "ESZ26",
            ValueDate = ValueDate,
            TimePeriod = TimeFrameType.Weekly,
            SequenceId = 4,
            TradeExecuteState = TradeExecuteState.Enter
        });

        subject.ViewModel.TradeSignals.Should().Equal(direction);
        subject.ViewModel.LatestTrendExtreme.Should().BeSameAs(extreme);
        subject.ViewModel.TradeStatus.TradeStatus.Should().Be("Open ShortIronCondor Trade");

        await subject.ViewModel.StopAsync(CancellationToken.None);
        subject.EventSource.PublishDirection(Signal(5, IntrinsicTimeTrendType.DownTrend));

        subject.ViewModel.TradeSignals.Should().Equal(direction);
        subject.EventSource.IsStarted.Should().BeFalse();
    }

    [Fact]
    public async Task LoadFailure_PreservesCodeAndPublishesPresentationError()
    {
        var subject = CreateSubject(
            new ServiceFailed<FuturesItiSignalV2ReadModel[]>(717, "trade status unavailable"));

        var exception = await FluentActions.Awaiting(
                () => subject.ViewModel.LoadTradeStatusOperation.ExecuteAsync())
            .Should().ThrowAsync<ModelOperationException>();

        exception.Which.ErrorCode.Should().Be(717);
        subject.ViewModel.LoadTradeStatusOperation.LastFailure.Should().BeSameAs(exception.Which);
        subject.ViewModel.LastError.Should().NotBeNull();
        subject.ViewModel.LastError!.ErrorCode.Should().Be(717);
        subject.ViewModel.LastError.Caption.Should().Be("Trade Status Error");
        await subject.ViewModel.DisposeAsync();
    }

    [Fact]
    public void PublicSurface_DeclaresNoDelegateCallbacks()
    {
        typeof(StatusConsoleViewModel)
            .GetFields(System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.DeclaredOnly)
            .Where(field => typeof(Delegate).IsAssignableFrom(field.FieldType))
            .Should().BeEmpty();
    }

    static Subject CreateSubject(
        ServiceResult<FuturesItiSignalV2ReadModel[]>? signalResult = null,
        ServiceResult<MDIForwardLossRatioReadModel[]>? upTrendResult = null,
        ServiceResult<MDIForwardLossRatioReadModel[]>? downTrendResult = null)
    {
        signalResult ??= new ServiceOk<FuturesItiSignalV2ReadModel[]>([]);
        upTrendResult ??= new ServiceOk<MDIForwardLossRatioReadModel[]>([]);
        downTrendResult ??= new ServiceOk<MDIForwardLossRatioReadModel[]>([]);

        var analyticsApi = Substitute.For<IMarketDataAnalyticsQueryApi>();
        analyticsApi.GetFuturesItiTrendDirectionChangedSignalsAsync(
                Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<TimeFrameType>())
            .Returns(Task.FromResult(signalResult));

        var referenceApi = Substitute.For<IReferenceQueryApi>();
        referenceApi.GetMDIForwardLossRatiosAsync(
                IntrinsicTimeTrendType.UpTrend, TradeType.ShortIronCondor)
            .Returns(Task.FromResult(upTrendResult));
        referenceApi.GetMDIForwardLossRatiosAsync(
                IntrinsicTimeTrendType.DownTrend, TradeType.LongIronCondor)
            .Returns(Task.FromResult(downTrendResult));

        var consumer = Substitute.For<IFuturesItiSignalUIEventConsumer>();
        var eventSource = new TestEventSource(consumer);
        var appRoot = Substitute.For<IAppRoot>();
        appRoot.GetModel<MarketDataAnalyticsQueryModel>()
            .Returns(new MarketDataAnalyticsQueryModel(analyticsApi));
        appRoot.GetModel<ReferenceQueryModel>().Returns(new ReferenceQueryModel(referenceApi));
        appRoot.GetModel<MarketDataAnalyticsEventModel>()
            .Returns(new MarketDataAnalyticsEventModel(consumer));

        return new Subject(
            new StatusConsoleViewModel(appRoot, "ESZ26", ValueDate),
            analyticsApi,
            eventSource);
    }

    static FuturesItiSignalV2ReadModel Signal(long sequenceId, IntrinsicTimeTrendType trend)
        => new()
        {
            ContractId = "ESZ26",
            ValueDate = ValueDate,
            TimePeriod = TimeFrameType.Weekly,
            SequenceId = sequenceId,
            IntrinsicTime = new DateTime(2026, 8, 11, 10, 0, 0, DateTimeKind.Utc),
            IntrinsicTimeTrend = trend,
            IntrinsicTimeMode = IntrinsicTimeModeType.TrendDirectionChanged,
            IntrinsicPrice = 6400,
            TargetDelta = 20
        };

    static MDIForwardLossRatioReadModel Ratio(int mdi, IntrinsicTimeTrendType trend)
        => new(
            mdi,
            trend,
            trend == IntrinsicTimeTrendType.UpTrend
                ? TradeType.ShortIronCondor
                : TradeType.LongIronCondor,
            0.25,
            "test",
            DateTime.UtcNow,
            "test",
            DateTime.UtcNow);

    sealed record Subject(
        StatusConsoleViewModel ViewModel,
        IMarketDataAnalyticsQueryApi AnalyticsApi,
        TestEventSource EventSource);

    sealed class TestEventSource
    {
        Action<FuturesItiSignalV2ReadModel>? _direction;
        Action<FuturesItiSignalV2ReadModel>? _extreme;
        Action<FuturesTradeSignalV2ReadModel>? _tradeSignal;

        public TestEventSource(IFuturesItiSignalUIEventConsumer consumer)
        {
            consumer.StartAsync(
                    Arg.Any<Action<FuturesItiSignalV2ReadModel>>(),
                    Arg.Any<Action<FuturesItiSignalV2ReadModel>>(),
                    Arg.Any<Action<FuturesTradeSignalV2ReadModel>>())
                .Returns(call =>
                {
                    _direction = call.ArgAt<Action<FuturesItiSignalV2ReadModel>>(0);
                    _extreme = call.ArgAt<Action<FuturesItiSignalV2ReadModel>>(1);
                    _tradeSignal = call.ArgAt<Action<FuturesTradeSignalV2ReadModel>>(2);
                    IsStarted = true;
                    return ValueTask.CompletedTask;
                });
            consumer.StopAsync().Returns(_ =>
            {
                IsStarted = false;
                return ValueTask.CompletedTask;
            });
        }

        public bool IsStarted { get; private set; }

        public void PublishDirection(FuturesItiSignalV2ReadModel signal)
            => (_direction ?? throw new InvalidOperationException("Listener not started."))(signal);

        public void PublishExtreme(FuturesItiSignalV2ReadModel signal)
            => (_extreme ?? throw new InvalidOperationException("Listener not started."))(signal);

        public void PublishTradeSignal(FuturesTradeSignalV2ReadModel signal)
            => (_tradeSignal ?? throw new InvalidOperationException("Listener not started."))(signal);
    }
}
