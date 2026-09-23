using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models.Portfolio;
using TomasAI.IFM.UI.Net.Services;
using TomasAI.IFM.UI.Net.Services.Reference;
using TomasAI.IFM.UI.Net.ViewModels.Operations;
using TomasAI.IFM.UI.Net.ViewModels.Trade;

namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.ViewModels;

public sealed class TradeOrderEditorViewModelTests
{
    [Fact]
    public void Emulator_can_open_outside_the_position_entry_window()
    {
        var viewModel = new TradeOrderEditorViewModel(
            Substitute.For<IAppRoot>(), new DateOnly(2026, 9, 20), [],
            Substitute.For<IReferenceDataService>(),
            new ManualTimeProvider(new DateTimeOffset(2026, 9, 20, 12, 0, 0, TimeSpan.Zero)),
            BrokerEnvironment.Emulator);

        viewModel.CanSubmitOrderAction(OrderActionType.Open).Should().BeTrue();
    }

    [Fact]
    public void Live_broker_cannot_open_outside_the_position_entry_window()
    {
        var viewModel = new TradeOrderEditorViewModel(
            Substitute.For<IAppRoot>(), new DateOnly(2026, 9, 20), [],
            Substitute.For<IReferenceDataService>(),
            new ManualTimeProvider(new DateTimeOffset(2026, 9, 20, 12, 0, 0, TimeSpan.Zero)),
            BrokerEnvironment.Live);

        viewModel.CanSubmitOrderAction(OrderActionType.Open).Should().BeFalse();
    }

    [Fact]
    public void UiOrder_DoesNotExposeTradeOwnedFields()
    {
        var canonical = typeof(FundOrderProjectionReadModel).GetProperties()
            .Select(property => property.Name)
            .Except(["UnderlyingRoot", "RequestedTradeDate", "RequestedMaturityDate"])
            .ToHashSet(StringComparer.Ordinal);
        var properties = typeof(PortfolioFundOrderEditorModel).GetProperties()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);

        properties.Should().Contain(canonical);
        properties.Should().NotContain([
            "UnderlyingRoot",
            "RequestedTradeDate",
            "RequestedMaturityDate",
            "BaseContractId",
        ]);
    }

    [Fact]
    public void UiTrade_CoversCanonicalProjectionAndAddsExecutionContext()
    {
        var canonical = typeof(FundOrderTradeProjectionReadModel).GetProperties()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);
        var ui = typeof(PortfolioFundOrderTradeEditorModel).GetProperties()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);

        ui.Should().Contain(canonical);
        ui.Should().Contain(["BaseContractId", "HasFillEvidence", "Id"]);
    }

    [Fact]
    public void TradeReference_UsesBaseContractAndCompactTradeWindow()
    {
        FundOrderTradeReference.Create(
                " ESZ26 ",
                new DateOnly(2026, 9, 18),
                new DateOnly(2026, 12, 18))
            .Should().Be("ESZ26 @ 20260918 - 20261218");
    }

    [Fact]
    public async Task CreateManualOrderFailure_PublishesCodedPresentationError()
    {
        var commands = Substitute.For<IPortfolioFundCommandApi>();
        commands.CreateManualOrderAsync(
                Arg.Any<CreateManualFundOrderRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ServiceResult<FundCompositionReservationResult>>(
                new ServiceFailed<FundCompositionReservationResult>(918, "manual order was rejected")));
        var queries = Substitute.For<IPortfolioQueryApi>();
        queries.GetPortfoliosAsync(
                Arg.Any<PortfolioOperatingState?>(),
                Arg.Any<int>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(new ServiceOk<PortfolioPage<PortfolioReadModel>>(new()
            {
                Items = [new PortfolioReadModel
                {
                    PortfolioId = 1201,
                    PortfolioVersion = 2,
                    OperatingState = PortfolioOperatingState.Active,
                }],
            }));
        queries.GetFundsAsync(
                1201,
                Arg.Any<FundOperatingState?>(),
                Arg.Any<int>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(new ServiceOk<PortfolioPage<FundMandateReadModel>>(new()
            {
                Items = [new FundMandateReadModel
                {
                    PortfolioId = 1201,
                    FundId = 5401,
                    FundMandateVersion = 1,
                    OperatingState = FundOperatingState.Active,
                }],
            }));
        queries.GetOrdersAsync(
                1201,
                5401,
                Arg.Any<DateOnly>(),
                Arg.Any<int>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(new ServiceOk<PortfolioPage<FundOrderProjectionReadModel>>(new()));
        var services = Substitute.For<IUiServiceCatalog>();
        services.PortfolioFundCommands.Returns(commands);
        services.PortfolioQueries.Returns(queries);
        var appRoot = Substitute.For<IAppRoot>();
        appRoot.Services.Returns(services);
        var viewModel = new TradeOrderEditorViewModel(
            appRoot,
            new DateOnly(2026, 9, 18),
            [],
            Substitute.For<IReferenceDataService>());
        await viewModel.LoadOperation.ExecuteAsync();
        var draft = new ManualFundOrderDraftEditorModel(
            5401,
            0,
            DateTime.UtcNow,
            PortfolioOrderEditorStatus.Open,
            "reference",
            DateTime.UtcNow,
            "operator",
            DateTime.UtcNow,
            "operator");

        var exception = await FluentActions.Awaiting(() => viewModel.CreateManualOrderAsync(draft))
            .Should().ThrowAsync<UiServiceOperationException>();

        exception.Which.ErrorCode.Should().Be(918);
        viewModel.LastError.Should().BeEquivalentTo(new
        {
            ErrorCode = 918,
            Message = "manual order was rejected",
            Caption = "Add Order Error",
        });
    }

    [Fact]
    public async Task SelectCanonicalOrderAsync_SlowerPriorSelectionCannotReplaceNewerOrder()
    {
        var firstOrder = new FundOrderProjectionReadModel
        {
            PortfolioId = 1201,
            FundId = 5401,
            OrderId = 16201,
            AggregateVersion = 1,
            CreatedOnUtc = new DateTime(2026, 9, 18, 12, 0, 0, DateTimeKind.Utc),
            Status = "Draft",
        };
        var secondOrder = firstOrder with
        {
            OrderId = 16202,
            CreatedOnUtc = firstOrder.CreatedOnUtc.AddMinutes(-1),
        };
        var firstTrade = new FundOrderTradeProjectionReadModel
        {
            PortfolioId = 1201,
            FundId = 5401,
            OrderId = 16201,
            TradeId = 9101,
            TradeType = TradeType.ShortIronCondor.ToString(),
            TradeState = TradeState.NewTrade.ToString(),
            TradeAction = TradeAction.Sell.ToString(),
            BaseContractId = "ESZ26",
        };
        var secondTrade = firstTrade with { OrderId = 16202, TradeId = 9102 };
        var firstReply = new TaskCompletionSource<ServiceResult<PortfolioPage<FundOrderTradeProjectionReadModel>>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var queries = Substitute.For<IPortfolioQueryApi>();
        queries.GetPortfoliosAsync(
                Arg.Any<PortfolioOperatingState?>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new ServiceOk<PortfolioPage<PortfolioReadModel>>(new()
            {
                Items = [new PortfolioReadModel
                {
                    PortfolioId = 1201,
                    PortfolioVersion = 2,
                    OperatingState = PortfolioOperatingState.Active,
                }],
            }));
        queries.GetFundsAsync(
                1201, Arg.Any<FundOperatingState?>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new ServiceOk<PortfolioPage<FundMandateReadModel>>(new()
            {
                Items = [new FundMandateReadModel
                {
                    PortfolioId = 1201,
                    FundId = 5401,
                    FundMandateVersion = 1,
                    OperatingState = FundOperatingState.Active,
                }],
            }));
        queries.GetOrdersAsync(
                1201, 5401, Arg.Any<DateOnly>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new ServiceOk<PortfolioPage<FundOrderProjectionReadModel>>(new()
            {
                Items = [firstOrder, secondOrder],
            }));
        queries.GetOrderTradesAsync(16201, 200, Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(_ => firstReply.Task);
        queries.GetOrderTradesAsync(16202, 200, Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new ServiceOk<PortfolioPage<FundOrderTradeProjectionReadModel>>(new()
            {
                Items = [secondTrade],
            }));
        var services = Substitute.For<IUiServiceCatalog>();
        services.PortfolioQueries.Returns(queries);
        services.PortfolioFundCommands.Returns(Substitute.For<IPortfolioFundCommandApi>());
        var appRoot = Substitute.For<IAppRoot>();
        appRoot.Services.Returns(services);
        var viewModel = new TradeOrderEditorViewModel(
            appRoot, new DateOnly(2026, 9, 18), [], Substitute.For<IReferenceDataService>());
        await viewModel.LoadOperation.ExecuteAsync();

        var slowerFirstSelection = viewModel.SelectCanonicalOrderAsync(16201);
        await viewModel.SelectCanonicalOrderAsync(16202);
        firstReply.SetResult(new ServiceOk<PortfolioPage<FundOrderTradeProjectionReadModel>>(new()
        {
            Items = [firstTrade],
        }));
        await slowerFirstSelection;

        viewModel.SelectedFundOrder.Should().NotBeNull();
        viewModel.SelectedFundOrder!.OrderId.Should().Be(16202);
        viewModel.FundOrderTrades.Should().ContainSingle();
        viewModel.FundOrderTrades[0].TradeId.Should().Be(9102);
        viewModel.SelectedFundOrderTrade.Should().NotBeNull();
        viewModel.SelectedFundOrderTrade!.TradeId.Should().Be(9102);

        await viewModel.LoadCanonicalOrdersAsync();

        viewModel.SelectedFundOrder.Should().NotBeNull();
        viewModel.SelectedFundOrder!.OrderId.Should().Be(16202);
    }
    [Fact]
    public async Task AddManualTradeSuccess_ReloadsSelectedOrdersProjectedTrades()
    {
        var order = new FundOrderProjectionReadModel
        {
            PortfolioId = 1201,
            FundId = 5401,
            OrderId = 16201,
            AggregateVersion = 1,
            CreatedOnUtc = new DateTime(2026, 9, 18, 12, 0, 0, DateTimeKind.Utc),
            Status = "Open",
        };
        var updatedOrder = order with { AggregateVersion = 2 };
        var projectedTrade = new FundOrderTradeProjectionReadModel
        {
            PortfolioId = 1201,
            FundId = 5401,
            OrderId = 16201,
            TradeId = 17001,
            AggregateVersion = 2,
            TradeType = TradeType.ShortIronCondor.ToString(),
            TradeState = TradeState.NewTrade.ToString(),
            TradeAction = TradeAction.Sell.ToString(),
            RequestedTradeDate = new DateOnly(2026, 9, 18),
            RequestedMaturityDate = new DateOnly(2026, 12, 18),
            BaseContractSymbol = "ES",
            BaseContractId = "ESZ26",
            InstructionReference = "ESZ26 @ 20260918 - 20261218",
            PrimaryTrade = false,
        };
        var existingTrade = projectedTrade with
        {
            TradeId = 17000,
            AggregateVersion = 1,
            PrimaryTrade = true,
            InstructionReference = "ESZ26 opening trade",
        };
        var commands = Substitute.For<IPortfolioFundCommandApi>();
        commands.AddManualTradeAsync(
                Arg.Any<AddManualFundOrderTradeRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(new ServiceOk<FundCompositionReservationResult>(new()
            {
                Order = updatedOrder,
                Trades = [existingTrade, projectedTrade],
                AggregateVersion = 2,
            }));
        var queries = Substitute.For<IPortfolioQueryApi>();
        queries.GetPortfoliosAsync(
                Arg.Any<PortfolioOperatingState?>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new ServiceOk<PortfolioPage<PortfolioReadModel>>(new()
            {
                Items = [new PortfolioReadModel
                {
                    PortfolioId = 1201,
                    PortfolioVersion = 2,
                    OperatingState = PortfolioOperatingState.Active,
                }],
            }));
        queries.GetFundsAsync(
                1201, Arg.Any<FundOperatingState?>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new ServiceOk<PortfolioPage<FundMandateReadModel>>(new()
            {
                Items = [new FundMandateReadModel
                {
                    PortfolioId = 1201,
                    FundId = 5401,
                    FundMandateVersion = 1,
                    OperatingState = FundOperatingState.Active,
                }],
            }));
        queries.GetOrdersAsync(
                1201, 5401, Arg.Any<DateOnly>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(
                new ServiceOk<PortfolioPage<FundOrderProjectionReadModel>>(new() { Items = [order] }),
                new ServiceOk<PortfolioPage<FundOrderProjectionReadModel>>(new() { Items = [updatedOrder] }));
        queries.GetOrderTradesAsync(16201, 200, Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new ServiceOk<PortfolioPage<FundOrderTradeProjectionReadModel>>(new()
            {
                Items = [existingTrade, projectedTrade],
            }));
        var services = Substitute.For<IUiServiceCatalog>();
        services.PortfolioFundCommands.Returns(commands);
        services.PortfolioQueries.Returns(queries);
        var appRoot = Substitute.For<IAppRoot>();
        appRoot.Services.Returns(services);
        var viewModel = new TradeOrderEditorViewModel(
            appRoot, new DateOnly(2026, 9, 18), [], Substitute.For<IReferenceDataService>());
        await viewModel.LoadOperation.ExecuteAsync();
        var trade = new PortfolioFundOrderTradeEditorModel
        {
            PortfolioId = 1201,
            FundId = 5401,
            OrderId = 16201,
            TradeId = 17001,
            TradeType = TradeType.ShortIronCondor,
            RequestedTradeDate = new DateOnly(2026, 9, 18),
            RequestedMaturityDate = new DateOnly(2026, 12, 18),
            TradeState = TradeState.NewTrade,
            TradeAction = TradeAction.Sell,
            PrimaryTrade = true,
            BaseContractSymbol = "ES",
            BaseContractId = "ESZ26",
        };

        await viewModel.AddManualTradeAsync(order, trade);

        viewModel.SelectedFundOrder.Should().NotBeNull();
        viewModel.SelectedFundOrder!.OrderId.Should().Be(16201);
        viewModel.FundOrderTrades.Should().HaveCount(2);
        viewModel.FundOrderTrades.Should().ContainSingle(value => value.TradeId == 17001);
        viewModel.SelectedFundOrderTrade.Should().NotBeNull();
        viewModel.SelectedFundOrderTrade!.TradeId.Should().Be(17001);
        await queries.Received(1).GetOrderTradesAsync(
            16201, 200, Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }
    [Fact]
    public async Task AddManualTradeFailure_PublishesCodedPresentationError()
    {
        var commands = Substitute.For<IPortfolioFundCommandApi>();
        commands.AddManualTradeAsync(
                Arg.Any<AddManualFundOrderTradeRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ServiceResult<FundCompositionReservationResult>>(
                new ServiceFailed<FundCompositionReservationResult>(919, "trade reference is required")));
        var services = Substitute.For<IUiServiceCatalog>();
        services.PortfolioFundCommands.Returns(commands);
        var appRoot = Substitute.For<IAppRoot>();
        appRoot.Services.Returns(services);
        var viewModel = new TradeOrderEditorViewModel(
            appRoot,
            new DateOnly(2026, 9, 18),
            [],
            Substitute.For<IReferenceDataService>());
        var order = new FundOrderProjectionReadModel
        {
            PortfolioId = 1201,
            FundId = 5401,
            OrderId = 16001,
            AggregateVersion = 1,
        };
        var trade = new PortfolioFundOrderTradeEditorModel
        {
            FundId = 5401,
            OrderId = 16001,
            TradeId = 17001,
            TradeType = TradeType.ShortIronCondor,
            RequestedTradeDate = new DateOnly(2026, 9, 18),
            RequestedMaturityDate = new DateOnly(2026, 9, 18),
            TradeState = TradeState.NewTrade,
            TradeAction = TradeAction.Sell,
            InstructionReference = string.Empty,
            PrimaryTrade = true,
            BaseContractSymbol = "ES",
            BaseContractId = "ESZ26",
        };

        var exception = await FluentActions.Awaiting(() => viewModel.AddManualTradeAsync(order, trade))
            .Should().ThrowAsync<UiServiceOperationException>();

        exception.Which.ErrorCode.Should().Be(919);
        await commands.Received(1).AddManualTradeAsync(
            Arg.Is<AddManualFundOrderTradeRequest>(request =>
                request.BaseContractId == "ESZ26" &&
                request.Reference == "ESZ26 @ 20260918 - 20260918"),
            Arg.Any<CancellationToken>());
        viewModel.LastError.Should().BeEquivalentTo(new
        {
            ErrorCode = 919,
            Message = "trade reference is required",
            Caption = "Add Trade Error",
        });
    }

    [Fact]
    public async Task DeleteManualOrderFailure_PublishesCodedPresentationError()
    {
        var commands = Substitute.For<IPortfolioFundCommandApi>();
        commands.DeleteManualOrderAsync(
                Arg.Any<ManualFundOrderMutationRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ServiceResult<Guid>>(
                new ServiceFailed<Guid>(920, "Only a draft order with an inactive trade can be deleted.")));
        var services = Substitute.For<IUiServiceCatalog>();
        services.PortfolioFundCommands.Returns(commands);
        var appRoot = Substitute.For<IAppRoot>();
        appRoot.Services.Returns(services);
        var viewModel = new TradeOrderEditorViewModel(
            appRoot,
            new DateOnly(2026, 9, 18),
            [],
            Substitute.For<IReferenceDataService>());
        var order = new FundOrderProjectionReadModel
        {
            PortfolioId = 1201,
            FundId = 5401,
            OrderId = 16001,
            AggregateVersion = 1,
        };

        var exception = await FluentActions.Awaiting(() =>
                viewModel.DeleteManualOrderAsync(order, "Deleted by operator."))
            .Should().ThrowAsync<UiServiceOperationException>();

        exception.Which.ErrorCode.Should().Be(920);
        await commands.Received(1).DeleteManualOrderAsync(
            Arg.Is<ManualFundOrderMutationRequest>(request =>
                request.PortfolioId == 1201 &&
                request.FundId == 5401 &&
                request.OrderId == 16001 &&
                request.ExpectedOrderVersion == 1 &&
                request.Reason == "Deleted by operator."),
            Arg.Any<CancellationToken>());
        viewModel.LastError.Should().BeEquivalentTo(new
        {
            ErrorCode = 920,
            Message = "Only a draft order with an inactive trade can be deleted.",
            Caption = "Remove Order Error",
        });
    }
}
