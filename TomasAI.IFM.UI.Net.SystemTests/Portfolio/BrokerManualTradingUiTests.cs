using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Domain.BrokerAccount.Contracts;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;
using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.ServiceApi;
using TomasAI.IFM.Domain.Trade.Shared.Order.Broker;
using TomasAI.IFM.Domain.Trade.Shared.Order.Execution;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Services;
using TomasAI.IFM.UI.Net.Services.Trade;
using TomasAI.IFM.UI.Net.ViewModels.Trade;
using TomasAI.IFM.UI.Net.Views.Trade;

namespace TomasAI.IFM.UI.Net.SystemTests.Portfolio;

public sealed class BrokerManualTradingUiTests
{
    [Theory]
    [InlineData(TradeType.PutCreditSpread, "P4500:4490", "ES20261016P4500", "ES20261016P4490")]
    [InlineData(TradeType.CallDebitSpread, "C4500:4510", "ES20261016C4500", "ES20261016C4510")]
    public void Vertical_spread_reference_resolves_two_broker_neutral_contracts(
        TradeType tradeType, string reference, string first, string second)
    {
        var trade = Trade(tradeType, reference);
        trade.GetContractIds().Should().Equal(first, second);
    }

    [Theory]
    [InlineData(TradeType.FuturesOutright, "FuturesView")]
    [InlineData(TradeType.PutCreditSpread, "VerticalSpreadView")]
    public void Blotter_factory_routes_futures_and_vertical_spread_to_dark_strategy_view(
        TradeType tradeType, string expectedName)
    {
        var fund = new FundReadModel(2, "Fund", "Test", 100_000m, false, DateTime.UtcNow, "test");
        var order = Order();
        using var host = new Panel();
        using var view = TradeBlotterFactory.Create(host, Substitute.For<IAppRoot>(), fund, order,
            Trade(tradeType, tradeType == TradeType.FuturesOutright ? "ESZ6" : "P4500:4490"),
            order.TradeDate, [Contract()], portfolioId: 1);
        view.Should().BeOfType<BrokerTradeBlotterView>();
        view!.Name.Should().Be(expectedName);
        view.BackColor.Should().Be(Color.Black);
        view.Dock.Should().Be(DockStyle.Fill);
    }

    [Fact]
    public void Iron_condor_blotter_includes_the_shared_durable_broker_evidence_tab()
    {
        var root = Substitute.For<IAppRoot>();
        var services = Substitute.For<IUiServiceCatalog>();
        root.Services.Returns(services);
        var commandResponses = new TomasAI.IFM.UI.Net.Services.Application.CommandResponseEventService(
            Substitute.For<TomasAI.IFM.UI.EventConsumer.ICommandResponseUIEventConsumer>());
        commandResponses.SetSiteId(Guid.NewGuid());
        services.CommandResponses.Returns(commandResponses);
        var fund = new FundReadModel(2, "Fund", "Test", 100_000m, false, DateTime.UtcNow, "test");
        var order = Order();
        using var host = new Panel();
        using var view = TradeBlotterFactory.Create(host, root, fund, order,
            Trade(TradeType.ShortIronCondor, "P4500:4490:C4550:4560"),
            order.TradeDate, [Contract()], portfolioId: 1);

        view.Should().NotBeNull();
        view!.Controls.Find("tabBrokerEvidence", true).Should().ContainSingle();
        view.Controls.Find("brokerExecutionEvidence", true).Should().ContainSingle();
    }

    [Theory]
    [InlineData(TradeType.FuturesOutright, "FuturesTradeOrderView")]
    [InlineData(TradeType.CallCreditSpread, "VerticalSpreadTradeOrderView")]
    public void Manual_editor_exposes_emulator_account_gate_and_exact_strategy_shape(
        TradeType tradeType, string expectedName)
    {
        var root = Substitute.For<IAppRoot>();
        var services = Substitute.For<TomasAI.IFM.UI.Net.Services.IUiServiceCatalog>();
        root.Services.Returns(services);
        var model = new BrokerManualTradeOrderViewModel(root, 1, Order(),
            Trade(tradeType, tradeType == TradeType.FuturesOutright ? "ESZ6" : "C4500:4510"), Contract());
        using var view = new BrokerManualTradeOrderView(model);
        view.Name.Should().Be(expectedName);
        view.BackColor.Should().Be(Color.Black);
        view.Controls.Cast<Control>().Should().ContainSingle();
        model.ContractIds.Should().HaveCount(tradeType == TradeType.FuturesOutright ? 1 : 2);
    }

    [Theory]
    [InlineData(TradeType.FuturesOutright, "ESZ6", 1, TradeStrategyKind.FuturesOutright)]
    [InlineData(TradeType.PutCreditSpread, "P4500:4490", 2, TradeStrategyKind.VerticalSpread)]
    public async Task Manual_editor_submits_operator_input_through_portfolio_and_trade_order_lifecycle(
        TradeType tradeType, string reference, int expectedLegCount, TradeStrategyKind expectedStrategy)
    {
        var portfolio = Substitute.For<IPortfolioOrderCompositionApi>();
        var lifecycle = Substitute.For<ITradeOrderLifecycleApi>();
        var portfolioQueries = Substitute.For<IPortfolioQueryApi>();
        var brokerAccounts = Substitute.For<IBrokerAccountQueryApi>();
        var services = Substitute.For<IUiServiceCatalog>();
        var root = Substitute.For<IAppRoot>();
        var approvalId = Guid.NewGuid();
        EvaluatePortfolioOrderCompositionCommand? submitted = null;

        root.Services.Returns(services);
        services.PortfolioQueries.Returns(portfolioQueries);
        services.BrokerAccounts.Returns(brokerAccounts);
        services.PortfolioTradeOrders.Returns(new PortfolioTradeOrderService(portfolio, lifecycle));
        portfolioQueries.GetFundAsync(1, 2, null, Arg.Any<CancellationToken>())
            .Returns(new ServiceOk<FundMandateReadModel>(FundMandate()));
        portfolioQueries.GetAssignmentsAsync(1, 2, 7, Arg.Any<CancellationToken>())
            .Returns(new ServiceOk<FundTradeTemplateAssignmentReadModel[]>(
                [Assignment(expectedStrategy)]));
        brokerAccounts.GetAsync(new BrokerAccountId("IFM-EMULATOR-PAPER"), Arg.Any<CancellationToken>())
            .Returns(new ServiceOk<BrokerAccountDefinition>(AcceptedAccount(approvalId)));
        portfolio.EvaluateAsync(Arg.Any<EvaluatePortfolioOrderCompositionCommand>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                submitted = call.Arg<EvaluatePortfolioOrderCompositionCommand>();
                var order = AcceptedOrder(submitted, expectedStrategy);
                var completed = new PortfolioOrderCompositionCompletedEvent
                {
                    Id = Guid.NewGuid(),
                    OperationId = submitted.OperationId,
                    PortfolioId = submitted.PortfolioId,
                    Receipt = new PortfolioOrderCompositionReceipt
                    {
                        PortfolioId = submitted.PortfolioId,
                        CompositionId = submitted.Body.CompositionId,
                        Status = PortfolioOrderCompositionStatus.ExecuteTradeOrders,
                        TradeOrders = [order]
                    }
                };
                return new ServiceOk<FunctionResult<PortfolioOrderCompositionCompletedEvent,
                    PortfolioOrderCompositionFailedEvent>>(
                    FunctionResult<PortfolioOrderCompositionCompletedEvent,
                        PortfolioOrderCompositionFailedEvent>.Complete(completed));
            });
        lifecycle.SubmitAcceptedAsync(Arg.Any<TradeOrderDefinition>(), Arg.Any<Guid>(),
                ExecutionChannel.Broker, Arg.Any<CancellationToken>())
            .Returns(new ServiceOk<Guid>(Guid.NewGuid()));
        var confirmation = Substitute.For<ITradeOrderConfirmationService>();
        confirmation.ConfirmAsync(Arg.Any<TomasAI.IFM.Domain.Trade.Shared.TradeOrder.ViewModels.TradeOrderReadModel>(),
                Arg.Any<CancellationToken>())
            .Returns(new TradeOrderConfirmationResult(true, TradeFillType.Broker));

        var model = new BrokerManualTradeOrderViewModel(root, 1, Order(),
            Trade(tradeType, reference), Contract());
        using var view = new BrokerManualTradeOrderView(model);
        ((NumericUpDown)view.Controls.Find("quantity", true).Single()).Value = 3;
        ((NumericUpDown)view.Controls.Find("signedNetDebitLimit", true).Single()).Value = -1.25m;

        var portfolioEventId = await ((ITradeOrderControl)view).SubmitOrderAsync(
            Order().TradeDate, OrderActionType.Open, confirmation);

        portfolioEventId.Should().NotBeEmpty();
        submitted.Should().NotBeNull();
        submitted!.Body.StrategyKind.Should().Be(expectedStrategy);
        submitted.Body.PositionType.Should().Be(TradeOrderPositionType.Opening);
        submitted.Body.BrokerEnvironment.Should().Be(BrokerEnvironment.Emulator);
        submitted.Body.BrokerAccountAlias.Should().Be("IFM-EMULATOR-PAPER");
        submitted.Body.AccountPromotionApprovalReference.Should().Be(approvalId.ToString("N"));
        submitted.Body.Components.Should().ContainSingle();
        submitted.Body.Components[0].Legs.Should().HaveCount(expectedLegCount);
        submitted.Body.Components[0].Legs.Should().OnlyContain(leg =>
            leg.SignedQuantity != 0 && !string.IsNullOrWhiteSpace(leg.ContractId));
        submitted.Body.Components[0].SignedNetDebitLimit.Should().Be(-1.25m);
        await lifecycle.Received(1).SubmitAcceptedAsync(
            Arg.Is<TradeOrderDefinition>(order =>
                order.PositionType == TradeOrderPositionType.Opening &&
                order.BrokerEnvironment == BrokerEnvironment.Emulator &&
                order.AccountPromotionApprovalReference == approvalId.ToString("N")),
            portfolioEventId, ExecutionChannel.Broker, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Current_blotter_renders_account_broker_order_and_exact_fill_evidence()
    {
        var services = Substitute.For<IUiServiceCatalog>();
        var root = Substitute.For<IAppRoot>();
        var accountApi = Substitute.For<IBrokerAccountQueryApi>();
        var brokerApi = Substitute.For<IBrokerOrderQueryApi>();
        var executionApi = Substitute.For<IOrderExecutionQueryApi>();
        var attemptId = Guid.NewGuid();
        var componentId = Guid.NewGuid();
        var tradeOrderId = new TradeOrderId(1, 2, 3);
        var brokerOrderId = new BrokerOrderId(new(tradeOrderId, attemptId), componentId);
        root.Services.Returns(services);
        services.BrokerAccounts.Returns(accountApi);
        services.BrokerOrders.Returns(brokerApi);
        services.OrderExecutions.Returns(executionApi);
        accountApi.GetAsync(new BrokerAccountId("IFM-EMULATOR-PAPER"), Arg.Any<CancellationToken>())
            .Returns(new ServiceOk<BrokerAccountDefinition>(AcceptedAccount(Guid.NewGuid()) with
            {
                Snapshot = new BrokerAccountSnapshotEvidence
                {
                    AccountAlias = "IFM-EMULATOR-PAPER",
                    Currency = "USD",
                    CashBalance = 99_000m,
                    AvailableFunds = 98_000m,
                    Complete = true,
                    Generation = 2,
                    AsOfUtc = DateTime.UtcNow
                }
            }));
        brokerApi.ListAsync(tradeOrderId, Arg.Any<CancellationToken>())
            .Returns(new ServiceOk<BrokerOrderDefinition[]>(
            [
                new()
                {
                    Id = brokerOrderId,
                    Status = BrokerOrderStatus.Filled,
                    BrokerRevision = 1,
                    CurrentSignedNetDebitLimit = 6000m,
                    DispatchCategory = "EM.DISPATCHED",
                    LastObservation = new()
                    {
                        Kind = BrokerOrderObservationKind.OrderCompleted,
                        OccurredAtUtc = DateTime.UtcNow
                    }
                }
            ]));
        executionApi.GetAsync(tradeOrderId, attemptId, Arg.Any<CancellationToken>())
            .Returns(new ServiceOk<OrderExecutionDefinition>(new()
            {
                TradeOrderId = tradeOrderId,
                ExecutionAttemptId = attemptId,
                Status = OrderExecutionStatus.Filled,
                Fills =
                [
                    new()
                    {
                        ExecutionFillId = Guid.NewGuid(),
                        ExecutionAttemptId = attemptId,
                        ComponentId = componentId,
                        TradeLegId = Guid.NewGuid(),
                        ContractId = "ESZ6",
                        ExternalExecutionId = "EM-EXEC-1",
                        SignedQuantity = 1,
                        Price = 6000m,
                        Commission = 0.65m,
                        FilledAtUtc = DateTime.UtcNow
                    }
                ]
            }));
        var fund = new FundReadModel(2, "Fund", "Test", 100_000m, false, DateTime.UtcNow, "test");
        using var view = new BrokerTradeBlotterView(root, fund, Order(),
            Trade(TradeType.FuturesOutright, "ESZ6"), 1, false);

        await view.RefreshAsync();

        var text = string.Join(Environment.NewLine,
            view.Controls.Find("brokerExecutionEvidenceText", true).Select(control => control.Text));
        text.Should().Contain("Gate=Open");
        text.Should().Contain("Broker order").And.Contain("Filled");
        text.Should().Contain("ESZ6").And.Contain("EM-EXEC-1").And.Contain("Commission=0.65");
    }

    [Fact]
    public async Task Qualification_dialog_requires_review_pending_evidence_before_human_acceptance()
    {
        var services = Substitute.For<IUiServiceCatalog>();
        var root = Substitute.For<IAppRoot>();
        var accountApi = Substitute.For<IBrokerAccountQueryApi>();
        root.Services.Returns(services);
        services.BrokerAccounts.Returns(accountApi);
        services.BrokerAccountCommands.Returns(Substitute.For<IBrokerAccountCommandApi>());
        accountApi.GetAsync(new BrokerAccountId("IFM-EMULATOR-PAPER"), Arg.Any<CancellationToken>())
            .Returns(new ServiceOk<BrokerAccountDefinition>(new BrokerAccountDefinition
            {
                Id = new("IFM-EMULATOR-PAPER"),
                Environment = TomasAI.IFM.Application.TradeBroker.Contracts.BrokerEnvironment.Emulator,
                QualificationStatus = BrokerAccountQualificationStatus.ReviewPending,
                Gate = BrokerAccountOperationalGate.Closed,
                ManifestHash = "sha256:reviewed",
                EvidenceReference = "artifact://emulator/e10",
                Revision = 3
            }));
        var model = new BrokerManualTradeOrderViewModel(root, 1, Order(),
            Trade(TradeType.FuturesOutright, "ESZ6"), Contract());
        using var dialog = new BrokerAccountQualificationDialog(model);

        await dialog.RefreshAsync();

        dialog.BackColor.Should().Be(Color.Black);
        dialog.Controls.Find("qualificationManifestHash", true).Single().Text
            .Should().Be("sha256:reviewed");
        dialog.Controls.Find("acceptQualification", true).Single().Enabled.Should().BeTrue();
        dialog.Controls.Find("revokeQualification", true).Single().Enabled.Should().BeFalse();
    }

    static FundOrderReadModel Order() => new(2, 3, DateTime.UtcNow,
        TomasAI.IFM.Domain.Fund.Shared.OrderStatus.Open, "ES",
        new DateOnly(2026, 9, 16), new DateOnly(2026, 10, 16), "manual", DateTime.UtcNow,
        "test", null, string.Empty);

    static FundOrderTradeReadModel Trade(TradeType type, string reference) => new(
        2, 3, 4, type, new DateOnly(2026, 9, 16), new DateOnly(2026, 10, 16),
        TradeState.NewTrade,
        type is TradeType.PutCreditSpread or TradeType.CallCreditSpread ? TradeAction.Sell : TradeAction.Buy,
        reference, true, "ES", DateTime.UtcNow, "test", null, string.Empty);

    static FuturesContractV3ReadModel Contract() => new("ESZ6", "ES December", "ES", "ESZ6",
        "FUT", "USD", "CME", "50", new DateOnly(2026, 12, 18), true);

    static FundMandateReadModel FundMandate() => new()
    {
        PortfolioId = 1,
        FundId = 2,
        FundMandateVersion = 7
    };

    static FundTradeTemplateAssignmentReadModel Assignment(TradeStrategyKind strategy)
    {
        var deployment = new CatalogKey(StrategyCatalogKind.Deployment, Guid.NewGuid(), 1);
        return new()
        {
            PortfolioId = 1,
            PortfolioVersion = 1,
            FundId = 2,
            FundMandateVersion = 7,
            AssignmentVersion = 1,
            Enabled = true,
            DecisionHorizon = "Daily",
            UnderlyingUniverse = ["ES"],
            TradeFamily = strategy == TradeStrategyKind.FuturesOutright ? "Futures" : "VerticalSpread",
            Priority = 1,
            EffectiveFromUtc = DateTime.UtcNow.AddDays(-1),
            TradeStrategyFamily = new TradeStrategyFamilyReference(0, 0)
            {
                CatalogDeployment = deployment
            }
        };
    }

    static BrokerAccountDefinition AcceptedAccount(Guid approvalId) => new()
    {
        Id = new BrokerAccountId("IFM-EMULATOR-PAPER"),
        Environment = TomasAI.IFM.Application.TradeBroker.Contracts.BrokerEnvironment.Emulator,
        QualificationStatus = BrokerAccountQualificationStatus.Accepted,
        Gate = BrokerAccountOperationalGate.Open,
        ApprovalId = approvalId,
        Reason = "Emulator qualification accepted"
    };

    static TradeOrderDefinition AcceptedOrder(
        EvaluatePortfolioOrderCompositionCommand submitted,
        TradeStrategyKind strategy) => new()
    {
        Id = new TradeOrderId(1, 2, 3),
        Status = TradeOrderStatus.Ready,
        ValueDate = submitted.Body.ValueDate,
        ValidUntilUtc = submitted.Body.ValidUntilUtc,
        Origin = submitted.Body.Origin,
        Components = submitted.Body.Components,
        PositionType = TradeOrderPositionType.Opening,
        BrokerAccountAlias = submitted.Body.BrokerAccountAlias,
        BrokerEnvironment = submitted.Body.BrokerEnvironment,
        AccountPromotionApprovalReference = submitted.Body.AccountPromotionApprovalReference,
        MicroExecutionProfileId = submitted.Body.MicroExecutionProfileId,
        MicroExecutionProfileVersion = submitted.Body.MicroExecutionProfileVersion,
        MicroExecutionProfileHash = submitted.Body.MicroExecutionProfileHash,
        PortfolioApprovalId = Guid.NewGuid()
    };
}
