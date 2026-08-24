using FluentAssertions;
using NSubstitute;
using System.ComponentModel;
using System.Reflection;
using TomasAI.IFM.Domain.Fund.Shared.Events;
using TomasAI.IFM.Domain.Fund.Shared.ServiceApi;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Events;
using TomasAI.IFM.Domain.Trade.Shared.ServiceApi;
using TomasAI.IFM.Domain.Trade.Shared.TradeOrder.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.EventConsumer;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.UI.Net.ViewModels.Operations;
using TomasAI.IFM.UI.Net.ViewModels.Trade;

namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.ViewModels;

public class EndOfDayAndConfirmationViewModelTests
{
    static readonly DateOnly ValueDate = new(2026, 8, 11);

    [Fact]
    public async Task LoadOperation_PublishesOneCoherentSnapshotAndDateChangeInvalidatesIt()
    {
        var subject = CreateSubject();
        await subject.ViewModel.InitializeAsync(CancellationToken.None);

        await subject.ViewModel.LoadOperation.ExecuteAsync();

        subject.ViewModel.Snapshot.Should().Be(new EndOfDayProcessSnapshot(
            6400m, 6420m, 6380m, 6410m, 1200, 0m, 100_000m));
        subject.ViewModel.CanRun.Should().BeTrue();
        subject.ViewModel.SetValueDate(ValueDate.AddDays(1));
        subject.ViewModel.Snapshot.Should().BeNull();
        subject.ViewModel.CanRun.Should().BeFalse();
        await subject.ViewModel.DisposeAsync();
    }

    [Fact]
    public async Task RunOperation_IgnoresUnrelatedEventAndAwaitsCorrelatedCompletion()
    {
        var commandId = Guid.NewGuid();
        var subject = CreateSubject(commandId);
        await subject.ViewModel.InitializeAsync(CancellationToken.None);
        await subject.ViewModel.LoadOperation.ExecuteAsync();

        var operation = subject.ViewModel.RunOperation.ExecuteAsync();
        await WaitForCommandAsync(subject.ViewModel, commandId);
        await subject.Events.PublishAsync(new EndOfDayFundTransactionProcessedCompleteEvent
        {
            CommandId = Guid.NewGuid(),
            CorrelationId = Guid.NewGuid()
        });
        operation.IsCompleted.Should().BeFalse();
        await subject.Events.PublishAsync(new EndOfDayFundTransactionProcessedCompleteEvent
        {
            CommandId = Guid.NewGuid(),
            CorrelationId = commandId
        });
        await operation;

        subject.ViewModel.IsCompleted.Should().BeTrue();
        subject.ViewModel.CommandId.Should().BeEmpty();
        subject.ViewModel.LastStatusMessage.Should().Contain("completed");
        await subject.ViewModel.DisposeAsync();
    }

    [Fact]
    public async Task CompletionBeforeCommandResponse_IsBuffered()
    {
        var commandId = Guid.NewGuid();
        var subject = CreateSubject();
        ConfigureCommand(subject.CommandApi, PublishEarlyAsync);
        await subject.ViewModel.InitializeAsync(CancellationToken.None);
        await subject.ViewModel.LoadOperation.ExecuteAsync();

        await subject.ViewModel.RunOperation.ExecuteAsync();

        subject.ViewModel.IsCompleted.Should().BeTrue();
        subject.ViewModel.CommandId.Should().BeEmpty();
        await subject.ViewModel.DisposeAsync();

        async Task<ServiceResult<Guid>> PublishEarlyAsync()
        {
            await subject.Events.PublishAsync(new EndOfDayFundTransactionProcessedCompleteEvent
            {
                CommandId = Guid.NewGuid(),
                CorrelationId = commandId
            });
            return new ServiceOk<Guid>(commandId);
        }
    }

    [Fact]
    public async Task TerminalFailure_PreservesCodeAndAllowsRetry()
    {
        var commandId = Guid.NewGuid();
        var subject = CreateSubject(commandId);
        await subject.ViewModel.InitializeAsync(CancellationToken.None);
        await subject.ViewModel.LoadOperation.ExecuteAsync();

        var operation = subject.ViewModel.RunOperation.ExecuteAsync();
        await WaitForCommandAsync(subject.ViewModel, commandId);
        await subject.Events.PublishAsync(new EndOfDayFundTransactionProcessedFailEvent
        {
            CommandId = Guid.NewGuid(),
            CorrelationId = commandId,
            ErrorCode = 731,
            ErrorMessage = "position projection failed"
        });

        var exception = await FluentActions.Awaiting(() => operation)
            .Should().ThrowAsync<UiServiceOperationException>();
        exception.Which.ErrorCode.Should().Be(731);
        subject.ViewModel.LastError!.ErrorCode.Should().Be(731);
        subject.ViewModel.CommandId.Should().BeEmpty();
        subject.ViewModel.RunOperation.CanExecute.Should().BeTrue();
        await subject.ViewModel.DisposeAsync();
    }

    [Fact]
    public async Task LoadFailure_PublishesCodedErrorAndLeavesNoPartialSnapshot()
    {
        var subject = CreateSubject();
        subject.FundApi.GetFundsAsync().Returns(
            new ServiceFailed<FundReadModel[]>(744, "fund query unavailable"));

        var exception = await FluentActions.Awaiting(
                () => subject.ViewModel.LoadOperation.ExecuteAsync())
            .Should().ThrowAsync<UiServiceOperationException>();

        exception.Which.ErrorCode.Should().Be(744);
        subject.ViewModel.LastError!.ErrorCode.Should().Be(744);
        subject.ViewModel.Snapshot.Should().BeNull();
        await subject.ViewModel.DisposeAsync();
    }

    [Fact]
    public async Task LifecycleOwnsListenerAndViewModelHasNoCallbacks()
    {
        var subject = CreateSubject();

        await subject.ViewModel.InitializeAsync(CancellationToken.None);
        subject.Events.IsStarted.Should().BeTrue();
        await subject.ViewModel.StopAsync(CancellationToken.None);
        subject.Events.IsStarted.Should().BeFalse();

        AssertObservableWithoutCallbacks<EndOfDayProcessViewModel>();
        await subject.ViewModel.DisposeAsync();
    }

    [Fact]
    public void ConfirmationState_IsObservableSafeAndDisablesUnimplementedBrokerFill()
    {
        var order = new TradeOrderReadModel { OrderDescription = "ES iron condor" };
        var viewModel = new TradeOrderConfirmationViewModel(order);

        viewModel.TradeOrder.Should().BeSameAs(order);
        viewModel.SelectedTradeFillType.Should().Be(TradeFillType.Manual);
        viewModel.CanConfirm.Should().BeTrue();
        viewModel.SelectTradeFillType(-1).Should().BeFalse();
        viewModel.SelectTradeFillType(1).Should().BeTrue();
        viewModel.SelectedTradeFillType.Should().Be(TradeFillType.Broker);
        viewModel.CanConfirm.Should().BeFalse();
        AssertObservableWithoutCallbacks<TradeOrderConfirmationViewModel>();
    }

    static Subject CreateSubject(Guid? commandId = null)
    {
        var fundApi = Substitute.For<IFundQueryApi>();
        fundApi.GetFundsAsync().Returns(new ServiceOk<FundReadModel[]>(
            [new FundReadModel(17, "Paper", "Paper trading", 100_000m, false, DateTime.UtcNow, "test")]));
        var tradeApi = Substitute.For<ITradeQueryApi>();
        tradeApi.GetOptionTradeAsync(101, 7).Returns(new ServiceOk<OptionTradeReadModel>(OptionTrade()));
        var marketDataApi = Substitute.For<IMarketDataFeedQueryApi>();
        marketDataApi.GetFuturesEodDataAsync("ESZ26", ValueDate)
            .Returns(new ServiceOk<FuturesEodDataV2ReadModel>(Eod()));
        var commandApi = Substitute.For<ITradeCommandApi>();
        if (commandId is not null)
            ConfigureCommand(commandApi, () => Task.FromResult<ServiceResult<Guid>>(new ServiceOk<Guid>(commandId.Value)));
        var events = new EventHarness();
        var appRoot = Substitute.For<IAppRoot>();
        appRoot.Services.FundQueries.Returns(new FundQueryService(fundApi));
        appRoot.Services.TradeQueries.Returns(new TradeQueryService(tradeApi));
        appRoot.Services.FeedQueries.Returns(new MarketDataFeedQueryService(marketDataApi));
        appRoot.Services.TradeCommands.Returns(new TradeCommandService(commandApi));
        appRoot.Services.EndOfDayEvents.Returns(new EndOfDayProcessEventService(events.Consumer));
        var parameter = new TradeEndOfDayParameter
        {
            FundId = 17,
            OrderId = 101,
            TradeId = 7,
            TradeType = TradeType.ShortIronCondor,
            BaseContractId = "ESZ26",
            ValueDate = ValueDate
        };
        return new Subject(new EndOfDayProcessViewModel(appRoot, parameter), fundApi, commandApi, events);
    }

    static void ConfigureCommand(
        ITradeCommandApi commandApi,
        Func<Task<ServiceResult<Guid>>> result)
        => commandApi.ProcessEndOfDayAsync(
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<TradeType>(),
                Arg.Any<DateOnly>(),
                Arg.Any<TradeStatus>(),
                Arg.Any<decimal>(),
                Arg.Any<decimal>(),
                Arg.Any<decimal>(),
                Arg.Any<decimal>(),
                Arg.Any<long>(),
                Arg.Any<string>())
            .Returns(_ => result());

    static OptionTradeReadModel OptionTrade()
        => new(
            101,
            7,
            "Iron Condor",
            ValueDate,
            new DateOnly(2026, 9, 18),
            TradeType.ShortIronCondor,
            TradeState.OrderFilled,
            TradeAction.Sell,
            "ESZ26",
            AssetType.Futures,
            true,
            false,
            DateTime.UtcNow,
            "test",
            DateTime.UtcNow,
            "test");

    static FuturesEodDataV2ReadModel Eod()
        => new(
            "ESZ26",
            ValueDate,
            "ES",
            6400m,
            6420m,
            6380m,
            6410m,
            1200,
            marketDirection: MarketDirectionType.Up,
            marketVolatility: MarketVolatilityType.High,
            priceDirection: PriceDirectionType.Rising,
            priceVolatility: PriceVolatilityType.Rising);

    static async Task WaitForCommandAsync(EndOfDayProcessViewModel viewModel, Guid commandId)
    {
        for (var attempt = 0; attempt < 100 && viewModel.CommandId != commandId; attempt++)
            await Task.Delay(5);
        viewModel.CommandId.Should().Be(commandId);
    }

    static void AssertObservableWithoutCallbacks<T>()
    {
        typeof(INotifyPropertyChanged).IsAssignableFrom(typeof(T)).Should().BeTrue();
        typeof(T).GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(member => member switch
            {
                FieldInfo field => typeof(Delegate).IsAssignableFrom(field.FieldType),
                PropertyInfo property => typeof(Delegate).IsAssignableFrom(property.PropertyType),
                _ => false
            })
            .Should().BeEmpty();
    }

    sealed record Subject(
        EndOfDayProcessViewModel ViewModel,
        IFundQueryApi FundApi,
        ITradeCommandApi CommandApi,
        EventHarness Events);

    sealed class EventHarness
    {
        Func<IEvent, ValueTask>? _listener;

        public EventHarness()
        {
            Consumer = Substitute.For<IEndOfDayProcessUIEventConsumer>();
            Consumer.StartAsync(Arg.Do<Func<IEvent, ValueTask>>(listener => _listener = listener))
                .Returns(ValueTask.CompletedTask);
            Consumer.StopAsync().Returns(_ =>
            {
                _listener = null;
                return ValueTask.CompletedTask;
            });
        }

        public IEndOfDayProcessUIEventConsumer Consumer { get; }
        public bool IsStarted => _listener is not null;
        public ValueTask PublishAsync(IEvent @event) => _listener!(@event);
    }
}
