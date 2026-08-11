using FluentAssertions;
using NSubstitute;
using System.ComponentModel;
using System.Reflection;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.Events;
using TomasAI.IFM.Domain.Fund.Shared.ServiceApi;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Service.StatusConsole;
using TomasAI.IFM.UI.EventConsumer;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.UI.Net.ViewModels.Operations;
using TomasAI.IFM.UI.Net.ViewModels.Trade;

namespace TomasAI.IFM.UI.Presentation.UnitTests.ViewModels;

public class TradeOrderEditorViewModelTests
{
    [Fact]
    public async Task LoadOperation_PublishesNestedSelectionAndDateFilteredState()
    {
        var subject = CreateSubject();
        subject.ViewModel.SetOrderDateRange(new DateTime(2026, 8, 1), new DateTime(2026, 8, 31, 23, 59, 59));

        await subject.ViewModel.LoadOperation.ExecuteAsync();

        subject.ViewModel.Funds.Should().ContainSingle();
        subject.ViewModel.FundOrders.Should().ContainSingle(order => order.OrderId == 101);
        subject.ViewModel.FundOrderTrades.Should().ContainSingle(trade => trade.TradeId == 7);
        subject.ViewModel.SelectedFund!.FundId.Should().Be(17);
        subject.ViewModel.SelectedFundOrder!.OrderId.Should().Be(101);
        subject.ViewModel.SelectedFundOrderTrade!.TradeId.Should().Be(7);
        subject.ViewModel.CanSubmitOrder.Should().BeTrue();
        subject.ViewModel.GetFund(-1).Should().BeNull();
        subject.ViewModel.GetFundOrder(99).Should().BeNull();
        subject.ViewModel.GetFundOrderTrade(-1).Should().BeNull();
        await subject.ViewModel.DisposeAsync();
    }

    [Fact]
    public async Task AddOrder_AwaitsOnlyItsCorrelatedTerminalEventAndRefreshesState()
    {
        var commandId = Guid.NewGuid();
        var addedOrder = Order(102, new DateTime(2026, 8, 12));
        var subject = CreateSubject();
        subject.CommandApi.AddOrderToFundAsync(addedOrder).Returns(new ServiceOk<Guid>(commandId));
        await subject.ViewModel.InitializeAsync(CancellationToken.None);
        await subject.ViewModel.LoadOperation.ExecuteAsync();

        var operation = subject.ViewModel.AddOrderToFund(addedOrder);
        await WaitForCommandAsync(subject.ViewModel, commandId);
        await subject.Events.PublishAsync(new OrderAddedToFundCompleteEvent
        {
            CommandId = Guid.NewGuid(),
            FundOrder = addedOrder
        });
        operation.IsCompleted.Should().BeFalse("unrelated terminal events must not complete a command");

        await subject.Events.PublishAsync(new OrderAddedToFundCompleteEvent
        {
            CommandId = commandId,
            FundOrder = addedOrder
        });
        await operation;

        subject.ViewModel.CommandId.Should().BeEmpty();
        subject.ViewModel.LastChange!.Kind.Should().Be(TradeOrderEditorChangeKind.OrderAdded);
        subject.ViewModel.LastStatusMessage.Should().Contain("102");
        subject.ViewModel.Funds.Single().Orders.Should().HaveCount(2);
        subject.ViewModel.Funds.Single().Orders.Single(order => order.OrderId == 101).Trades.Should().ContainSingle();
        await subject.QueryApi.Received(2).GetFundsAsync();
        await subject.ViewModel.DisposeAsync();
    }

    [Fact]
    public async Task CompletionBeforeCommandResponse_IsBufferedAndCorrelated()
    {
        var commandId = Guid.NewGuid();
        var addedOrder = Order(102, new DateTime(2026, 8, 12));
        var subject = CreateSubject();
        subject.CommandApi.AddOrderToFundAsync(addedOrder).Returns(_ => PublishEarlyAsync());
        await subject.ViewModel.InitializeAsync(CancellationToken.None);
        await subject.ViewModel.LoadOperation.ExecuteAsync();

        await subject.ViewModel.AddOrderToFund(addedOrder);

        subject.ViewModel.LastChange!.Kind.Should().Be(TradeOrderEditorChangeKind.OrderAdded);
        subject.ViewModel.CommandId.Should().BeEmpty();
        await subject.ViewModel.DisposeAsync();

        async Task<ServiceResult<Guid>> PublishEarlyAsync()
        {
            await subject.Events.PublishAsync(new OrderAddedToFundCompleteEvent
            {
                CommandId = commandId,
                FundOrder = addedOrder
            });
            return new ServiceOk<Guid>(commandId);
        }
    }

    [Fact]
    public async Task TerminalFailure_PreservesErrorCodeAndClearsCorrelation()
    {
        var commandId = Guid.NewGuid();
        var subject = CreateSubject();
        var order = subject.ViewModel.GetFundOrder(0) ?? Order(101, new DateTime(2026, 8, 11));
        subject.CommandApi.AddOrderToFundAsync(order).Returns(new ServiceOk<Guid>(commandId));
        await subject.ViewModel.InitializeAsync(CancellationToken.None);

        var operation = subject.ViewModel.AddOrderToFund(order);
        await WaitForCommandAsync(subject.ViewModel, commandId);
        await subject.Events.PublishAsync(new OrderAddedToFundFailEvent
        {
            CommandId = commandId,
            ErrorCode = 722,
            ErrorMessage = "fund order rejected"
        });

        var exception = await FluentActions.Awaiting(() => operation)
            .Should().ThrowAsync<ModelOperationException>();
        exception.Which.ErrorCode.Should().Be(722);
        subject.ViewModel.LastError!.ErrorCode.Should().Be(722);
        subject.ViewModel.CommandId.Should().BeEmpty();
        await subject.ViewModel.DisposeAsync();
    }

    [Fact]
    public async Task EmbeddedCommandCompletionBeforeCorrelationCallback_IsRetained()
    {
        var commandId = Guid.NewGuid();
        var subject = CreateSubject();
        var addedOrder = Order(102, new DateTime(2026, 8, 12));
        await subject.ViewModel.InitializeAsync(CancellationToken.None);
        await subject.Events.PublishAsync(new OrderAddedToFundCompleteEvent
        {
            CommandId = commandId,
            FundOrder = addedOrder
        });

        subject.ViewModel.SetCommandId(commandId);
        for (var attempt = 0; attempt < 100 && subject.ViewModel.LastChange is null; attempt++)
            await Task.Delay(5);

        subject.ViewModel.LastChange!.Kind.Should().Be(TradeOrderEditorChangeKind.OrderAdded);
        subject.ViewModel.CommandId.Should().BeEmpty();
        await subject.ViewModel.DisposeAsync();
    }

    [Fact]
    public async Task Lifecycle_OwnsBothEventConsumersAndPublicStateHasNoCallbacks()
    {
        var subject = CreateSubject();

        await subject.ViewModel.InitializeAsync(CancellationToken.None);
        subject.Events.FundListenerStarted.Should().BeTrue();
        subject.Events.TradeStateListenerStarted.Should().BeTrue();
        await subject.ViewModel.StopAsync(CancellationToken.None);
        subject.Events.FundListenerStarted.Should().BeFalse();
        subject.Events.TradeStateListenerStarted.Should().BeFalse();

        typeof(INotifyPropertyChanged).IsAssignableFrom(typeof(TradeOrderEditorViewModel)).Should().BeTrue();
        typeof(TradeOrderEditorViewModel)
            .GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(member => member switch
            {
                FieldInfo field => typeof(Delegate).IsAssignableFrom(field.FieldType),
                PropertyInfo property => typeof(Delegate).IsAssignableFrom(property.PropertyType),
                _ => false
            })
            .Should().BeEmpty();
        await subject.ViewModel.DisposeAsync();
    }

    static Subject CreateSubject()
    {
        var queryApi = Substitute.For<IFundQueryApi>();
        queryApi.GetFundsAsync().Returns(new ServiceOk<FundReadModel[]>([Fund()]));
        queryApi.GetFundOrdersAsync().Returns(new ServiceOk<FundOrderReadModel[]>(
            [Order(101, new DateTime(2026, 8, 11)), Order(99, new DateTime(2026, 6, 1))]));
        queryApi.GetFundOrderTradesAsync().Returns(new ServiceOk<FundOrderTradeReadModel[]>([Trade()]));
        var commandApi = Substitute.For<IFundCommandApi>();
        var riskConsumer = Substitute.For<IFundRiskMarginUIEventConsumer>();
        var eventConsumers = new EventHarness();
        var referenceApi = Substitute.For<IReferenceQueryApi>();
        var appRoot = Substitute.For<IAppRoot>();
        appRoot.GetModel<FundQueryModel>().Returns(new FundQueryModel(queryApi));
        appRoot.GetModel<FundCommandModel>().Returns(new FundCommandModel(
            commandApi,
            riskConsumer,
            eventConsumers.TradeStateConsumer));
        appRoot.GetModel<ReferenceQueryModel>().Returns(new ReferenceQueryModel(referenceApi));
        appRoot.GetModel<FundOrderEventModel>().Returns(new FundOrderEventModel(eventConsumers.FundConsumer));
        appRoot.GetModel<StatusConsoleModel>().Returns(new StatusConsoleModel(
            Substitute.For<IStatusConsoleWriter>(),
            Substitute.For<IStatusConsoleEventConsumer>()));
        var viewModel = new TradeOrderEditorViewModel(appRoot, new DateOnly(2026, 8, 11), [Contract()]);
        return new Subject(viewModel, queryApi, commandApi, eventConsumers);
    }

    static FundReadModel Fund()
        => new(17, "Paper", "Paper trading", 100_000m, false, DateTime.UtcNow, "test");

    static FundOrderReadModel Order(int orderId, DateTime orderDate)
        => new(
            17,
            orderId,
            orderDate,
            TomasAI.IFM.Domain.Fund.Shared.OrderStatus.Open,
            "ESZ26",
            new DateOnly(2026, 8, 11),
            new DateOnly(2026, 9, 18),
            $"Order {orderId}",
            DateTime.UtcNow,
            "test",
            null,
            "test");

    static FundOrderTradeReadModel Trade()
        => new(
            17,
            101,
            7,
            TradeType.ShortIronCondor,
            new DateOnly(2026, 8, 11),
            new DateOnly(2026, 9, 18),
            TradeState.NewTrade,
            TradeAction.Sell,
            "P:4500:4550 X C:5000:5050",
            true,
            "ES",
            DateTime.UtcNow,
            "test",
            null,
            "test");

    static FuturesContractV2ReadModel Contract()
        => new(
            "ESZ26",
            "ESZ26",
            "ES",
            "ESZ26",
            "FUT",
            "USD",
            "CME",
            "50",
            new DateOnly(2026, 12, 18),
            true);

    static async Task WaitForCommandAsync(TradeOrderEditorViewModel viewModel, Guid commandId)
    {
        for (var attempt = 0; attempt < 100 && viewModel.CommandId != commandId; attempt++)
            await Task.Delay(5);
        viewModel.CommandId.Should().Be(commandId);
    }

    sealed record Subject(
        TradeOrderEditorViewModel ViewModel,
        IFundQueryApi QueryApi,
        IFundCommandApi CommandApi,
        EventHarness Events);

    sealed class EventHarness
    {
        Func<IEvent, ValueTask>? _fundListener;
        Func<FundOrderTradeStateChangedCompleteEvent, ValueTask>? _stateCompleteListener;
        Func<FundOrderTradeStateChangedFailEvent, ValueTask>? _stateFailListener;

        public EventHarness()
        {
            FundConsumer = Substitute.For<IFundOrderUIEventConsumer>();
            TradeStateConsumer = Substitute.For<IFundOrderTradeStateUIEventConsumer>();
            FundConsumer.StartAsync(Arg.Do<Func<IEvent, ValueTask>>(listener => _fundListener = listener))
                .Returns(ValueTask.CompletedTask);
            FundConsumer.StopAsync().Returns(_ =>
            {
                _fundListener = null;
                return ValueTask.CompletedTask;
            });
            TradeStateConsumer.StartAsync(
                    Arg.Do<Func<FundOrderTradeStateChangedCompleteEvent, ValueTask>>(listener => _stateCompleteListener = listener),
                    Arg.Do<Func<FundOrderTradeStateChangedFailEvent, ValueTask>>(listener => _stateFailListener = listener))
                .Returns(ValueTask.CompletedTask);
            TradeStateConsumer.StopAsync().Returns(_ =>
            {
                _stateCompleteListener = null;
                _stateFailListener = null;
                return ValueTask.CompletedTask;
            });
        }

        public IFundOrderUIEventConsumer FundConsumer { get; }
        public IFundOrderTradeStateUIEventConsumer TradeStateConsumer { get; }
        public bool FundListenerStarted => _fundListener is not null;
        public bool TradeStateListenerStarted => _stateCompleteListener is not null && _stateFailListener is not null;

        public ValueTask PublishAsync(IEvent @event)
            => @event switch
            {
                FundOrderTradeStateChangedCompleteEvent complete => _stateCompleteListener!(complete),
                FundOrderTradeStateChangedFailEvent fail => _stateFailListener!(fail),
                _ => _fundListener!(@event)
            };
    }
}
