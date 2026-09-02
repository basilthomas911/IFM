using FluentAssertions;
using NSubstitute;
using System.ComponentModel;
using System.Reflection;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.Events;
using TomasAI.IFM.Domain.Fund.Shared.ServiceApi;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Service.StatusConsole;
using TomasAI.IFM.UI.EventConsumer;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.UI.Net.Presentation.UnitTests.TestDoubles;
using TomasAI.IFM.UI.Net.ViewModels.Operations;
using TomasAI.IFM.UI.Net.ViewModels.Trade;

namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.ViewModels;

public class TradeOrderEditorViewModelTests
{
    static readonly DateTimeOffset EntryWindowUtc =
        new(2026, 8, 11, 14, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task LoadOperation_PublishesPortfolioFundAndDateScopedCanonicalState()
    {
        var subject = CreateSubject();
        subject.ViewModel.SetOrderDateRange(new DateTime(2026, 8, 1), new DateTime(2026, 8, 31, 23, 59, 59));

        await subject.ViewModel.LoadOperation.ExecuteAsync();

        subject.ViewModel.Portfolios.Should().ContainSingle(portfolio => portfolio.PortfolioId == 41);
        subject.ViewModel.Funds.Should().ContainSingle();
        subject.ViewModel.CanonicalOrders.Should().ContainSingle(order => order.OrderId == 101);
        subject.ViewModel.SelectedFund!.FundId.Should().Be(17);
        subject.ViewModel.CanCreateOrder.Should().BeTrue();
        var trades = await subject.ViewModel.GetCanonicalTradesAsync(101);
        trades.Should().ContainSingle(trade => trade.TradeId == 7);
        subject.ViewModel.GetFund(-1).Should().BeNull();
        subject.ViewModel.GetFundOrder(99).Should().BeNull();
        subject.ViewModel.GetFundOrderTrade(-1).Should().BeNull();
        await subject.PortfolioQueryApi.Received(1).GetOrdersAsync(
            41,
            17,
            new DateOnly(2026, 8, 1),
            200,
            null,
            Arg.Any<CancellationToken>());
        await subject.ViewModel.DisposeAsync();
    }

    [Fact]
    public async Task LoadOperation_ExcludesInactivePortfolioFundMandates()
    {
        var subject = CreateSubject();
        subject.PortfolioQueryApi.GetFundsAsync(
                Arg.Any<int>(),
                Arg.Any<FundOperatingState?>(),
                Arg.Any<int>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(new ServiceOk<PortfolioPage<FundMandateReadModel>>(Page(
                FundMandate(),
                FundMandate() with
                {
                    FundId = 18,
                    FundCode = "ARCHIVE",
                    Name = "Archived",
                    OperatingState = FundOperatingState.Retired
                })));

        await subject.ViewModel.LoadOperation.ExecuteAsync();

        subject.ViewModel.Funds.Should().ContainSingle(fund => fund.Name == "Paper");
        subject.ViewModel.SelectedFund!.FundId.Should().Be(17);
        await subject.ViewModel.DisposeAsync();
    }

    [Fact]
    public async Task PositionEntryWindow_BlocksOpenWithoutExceptionButAlwaysAllowsClose()
    {
        var afterHours = new ManualTimeProvider(
            new DateTimeOffset(2026, 8, 11, 22, 0, 0, TimeSpan.Zero));
        var subject = CreateSubject(afterHours);
        await subject.ViewModel.LoadOperation.ExecuteAsync();

        subject.ViewModel.CanSubmitOrderAction(OrderActionType.Open).Should().BeFalse();
        subject.ViewModel.ValidateOrderSubmission(OrderActionType.Open).Should().BeFalse();
        subject.ViewModel.LastError!.Caption.Should().Be("Position Entry Closed");

        subject.ViewModel.CanSubmitOrderAction(OrderActionType.Close).Should().BeTrue();
        subject.ViewModel.ValidateOrderSubmission(OrderActionType.Close).Should().BeTrue();
        await subject.ViewModel.DisposeAsync();
    }

    [Fact]
    public async Task CreateManualOrder_UsesPortfolioAuthorityAndRefreshesCanonicalOrders()
    {
        var subject = CreateSubject();
        var createdOrder = CanonicalOrder(
            102,
            new DateTime(2026, 8, 12, 14, 0, 0, DateTimeKind.Utc));
        var reservation = new FundCompositionReservationResult
        {
            Order = createdOrder,
            Trades = [CanonicalTrade() with { OrderId = 102, TradeId = 8 }],
            AggregateVersion = 1,
            CommittedOnUtc = createdOrder.CreatedOnUtc,
            Disposition = ReservationDisposition.Committed,
            CanonicalRequestSha256 = "manual-order-hash"
        };
        CreateManualFundOrderRequest? capturedRequest = null;
        subject.PortfolioFundCommandApi.CreateManualOrderAsync(
                Arg.Do<CreateManualFundOrderRequest>(request => capturedRequest = request),
                Arg.Any<CancellationToken>())
            .Returns(new ServiceOk<FundCompositionReservationResult>(reservation));
        subject.PortfolioQueryApi.GetOrdersAsync(
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<DateOnly>(),
                Arg.Any<int>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(
                new ServiceOk<PortfolioPage<FundOrderProjectionReadModel>>(Page(CanonicalOrder())),
                new ServiceOk<PortfolioPage<FundOrderProjectionReadModel>>(Page(CanonicalOrder(), createdOrder)));
        await subject.ViewModel.LoadOperation.ExecuteAsync();

        var result = await subject.ViewModel.CreateManualOrderAsync(
            Order(102, new DateTime(2026, 8, 12)));

        result.Should().BeSameAs(reservation);
        capturedRequest.Should().NotBeNull();
        capturedRequest!.PortfolioId.Should().Be(41);
        capturedRequest.PortfolioVersion.Should().Be(3);
        capturedRequest.FundId.Should().Be(17);
        capturedRequest.FundMandateVersion.Should().Be(4);
        capturedRequest.UnderlyingRoot.Should().Be("ESZ26");
        subject.ViewModel.CanonicalOrders.Should().HaveCount(2);
        subject.ViewModel.LastStatusMessage.Should().Contain("102");
        await subject.ViewModel.DisposeAsync();
    }

    [Fact]
    public async Task AddOrder_AwaitsOnlyItsCorrelatedTerminalEventAndRefreshesState()
    {
        var commandId = Guid.NewGuid();
        var addedOrder = Order(102, new DateTime(2026, 8, 12));
        var subject = CreateSubject();
        subject.PortfolioQueryApi.GetOrdersAsync(
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<DateOnly>(),
                Arg.Any<int>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(
                new ServiceOk<PortfolioPage<FundOrderProjectionReadModel>>(Page(CanonicalOrder())),
                new ServiceOk<PortfolioPage<FundOrderProjectionReadModel>>(Page(
                    CanonicalOrder(),
                    CanonicalOrder(102, new DateTime(2026, 8, 12, 14, 0, 0, DateTimeKind.Utc)))));
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
        subject.ViewModel.CanonicalOrders.Should().HaveCount(2);
        subject.ViewModel.CanonicalOrders.Should().Contain(order => order.OrderId == 102);
        await subject.PortfolioQueryApi.Received(2).GetPortfoliosAsync(
            PortfolioOperatingState.Active,
            200,
            null,
            Arg.Any<CancellationToken>());
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
            .Should().ThrowAsync<UiServiceOperationException>();
        exception.Which.ErrorCode.Should().Be(722);
        subject.ViewModel.LastError!.ErrorCode.Should().Be(722);
        subject.ViewModel.CommandId.Should().BeEmpty();
        await subject.ViewModel.DisposeAsync();
    }

    [Fact]
    public async Task ChangeTradeState_AwaitsExactTerminalAndPublishesTheChange()
    {
        var commandId = Guid.NewGuid();
        var initial = Trade();
        var subject = CreateSubject();
        subject.CommandApi.ChangeFundOrderTradeStateAsync(initial.Id, TradeState.OrderSubmitted)
            .Returns(new ServiceOk<Guid>(commandId));
        await subject.ViewModel.InitializeAsync(CancellationToken.None);
        await subject.ViewModel.LoadOperation.ExecuteAsync();

        var operation = subject.ViewModel.ChangeFundOrderTradeState(initial.Id, TradeState.OrderSubmitted);
        await WaitForCommandAsync(subject.ViewModel, commandId);
        await subject.Events.PublishAsync(new FundOrderTradeStateChangedCompleteEvent
        {
            CommandId = commandId,
            FundOrderTradeId = initial.Id,
            TradeState = TradeState.OrderSubmitted
        });
        await operation;

        subject.ViewModel.LastChange!.Kind.Should().Be(TradeOrderEditorChangeKind.TradeStateChanged);
        subject.ViewModel.LastStatusMessage.Should().Contain("OrderSubmitted");
        subject.ViewModel.CommandId.Should().BeEmpty();
        await subject.PortfolioQueryApi.Received(2).GetPortfoliosAsync(
            PortfolioOperatingState.Active,
            200,
            null,
            Arg.Any<CancellationToken>());
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

    static Subject CreateSubject(TimeProvider? timeProvider = null)
    {
        var queryApi = Substitute.For<IFundQueryApi>();
        queryApi.GetFundsAsync().Returns(new ServiceOk<FundReadModel[]>([Fund()]));
        queryApi.GetFundOrdersAsync().Returns(new ServiceOk<FundOrderReadModel[]>(
            [Order(101, new DateTime(2026, 8, 11)), Order(99, new DateTime(2026, 6, 1))]));
        queryApi.GetFundOrderTradesAsync().Returns(new ServiceOk<FundOrderTradeReadModel[]>([Trade()]));
        var commandApi = Substitute.For<IFundCommandApi>();
        var portfolioQueryApi = Substitute.For<IPortfolioQueryApi>();
        portfolioQueryApi.GetPortfoliosAsync(
                Arg.Any<PortfolioOperatingState?>(),
                Arg.Any<int>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(new ServiceOk<PortfolioPage<PortfolioReadModel>>(Page(Portfolio())));
        portfolioQueryApi.GetFundsAsync(
                Arg.Any<int>(),
                Arg.Any<FundOperatingState?>(),
                Arg.Any<int>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(new ServiceOk<PortfolioPage<FundMandateReadModel>>(Page(FundMandate())));
        portfolioQueryApi.GetOrdersAsync(
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<DateOnly>(),
                Arg.Any<int>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(call => new ServiceOk<PortfolioPage<FundOrderProjectionReadModel>>(
                call.ArgAt<DateOnly>(2) == new DateOnly(2026, 8, 1)
                    ? Page(CanonicalOrder())
                    : Page<FundOrderProjectionReadModel>()));
        portfolioQueryApi.GetOrderTradesAsync(
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(new ServiceOk<PortfolioPage<FundOrderTradeProjectionReadModel>>(Page(CanonicalTrade())));
        var portfolioFundCommandApi = Substitute.For<IPortfolioFundCommandApi>();
        var riskConsumer = Substitute.For<IFundRiskMarginUIEventConsumer>();
        var eventConsumers = new EventHarness();
        var referenceApi = Substitute.For<IReferenceQueryApi>();
        var appRoot = Substitute.For<IAppRoot>();
        appRoot.Services.PortfolioQueries.Returns(portfolioQueryApi);
        appRoot.Services.PortfolioFundCommands.Returns(portfolioFundCommandApi);
        appRoot.Services.FundQueries.Returns(new FundQueryService(queryApi));
        appRoot.Services.FundCommands.Returns(new FundCommandService(
            commandApi,
            riskConsumer,
            eventConsumers.TradeStateConsumer));
        appRoot.Services.FundOrderEvents.Returns(new FundOrderEventService(eventConsumers.FundConsumer));
        appRoot.Services.StatusConsole.Returns(new StatusConsoleService(
            Substitute.For<IStatusConsoleWriter>(),
            Substitute.For<IStatusConsoleEventConsumer>()));
        var viewModel = new TradeOrderEditorViewModel(
            appRoot,
            new DateOnly(2026, 8, 11),
            [Contract()],
            UiServiceFactory.CreateReference(referenceApi),
            timeProvider ?? new ManualTimeProvider(EntryWindowUtc));
        return new Subject(
            viewModel,
            queryApi,
            commandApi,
            portfolioQueryApi,
            portfolioFundCommandApi,
            eventConsumers);
    }

    static PortfolioReadModel Portfolio()
        => new()
        {
            PortfolioId = 41,
            Name = "Trading Portfolio",
            PortfolioVersion = 3,
            OperatingState = PortfolioOperatingState.Active,
            EffectiveFromUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ActivePolicyId = 9,
            ActivePolicyVersion = 2,
            CreatedOnUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            CreatedBy = "test"
        };

    static FundMandateReadModel FundMandate()
        => new()
        {
            PortfolioId = 41,
            FundId = 17,
            FundCode = "PAPER",
            Name = "Paper",
            FundMandateVersion = 4,
            TradingYear = 2026,
            OperatingState = FundOperatingState.Active,
            EffectiveFromUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            DecisionHorizon = "Daily",
            Objective = "Paper trading",
            UnderlyingUniverse = ["ES"],
            EligibleAssetTypes = ["FUT", "OPT"],
            PermittedTradeFamilies = ["Futures", "VerticalSpreads", "IronCondor"],
            CreatedOnUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            CreatedBy = "test"
        };

    static FundOrderProjectionReadModel CanonicalOrder(
        int orderId = 101,
        DateTime? createdOnUtc = null)
        => new()
        {
            PortfolioId = 41,
            FundId = 17,
            OrderId = orderId,
            WorkflowId = Guid.Parse("bb91b2db-20b5-42c4-aa4b-9023daf1328b"),
            Status = "Reserved",
            CreatedOnUtc = createdOnUtc ?? new DateTime(2026, 8, 11, 14, 0, 0, DateTimeKind.Utc),
            CreatedBy = "test",
            AggregateVersion = 1,
            ExpiresAtUtc = new DateTime(2026, 8, 12, 14, 0, 0, DateTimeKind.Utc),
            IdempotencyKey = Guid.Parse("fb971b1f-f42f-4eaa-b885-2ae9c4e6118c"),
            Origin = CompositionOrigin.ManualUi,
            OperatorReference = $"Order {orderId}"
        };

    static FundOrderTradeProjectionReadModel CanonicalTrade()
        => new()
        {
            PortfolioId = 41,
            FundId = 17,
            OrderId = 101,
            TradeId = 7,
            TradeFamily = "IronCondor",
            InstructionReference = "P:4500:4550 X C:5000:5050",
            LegOrdinal = 1,
            AggregateVersion = 1,
            DirectionOrBias = "Neutral",
            TradeAction = "Sell",
            UnderlyingRoot = "ES",
            RequestedTradeDate = new DateOnly(2026, 8, 11),
            RequestedMaturityDate = new DateOnly(2026, 9, 18)
        };

    static PortfolioPage<T> Page<T>(params T[] items)
        => new() { Items = items, PageSize = 200 };

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

    static FuturesContractV3ReadModel Contract()
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
        IPortfolioQueryApi PortfolioQueryApi,
        IPortfolioFundCommandApi PortfolioFundCommandApi,
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
