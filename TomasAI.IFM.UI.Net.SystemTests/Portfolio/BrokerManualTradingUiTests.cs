using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Domain.BrokerAccount.Contracts;
using TomasAI.IFM.Application.MarketData.Pricing;
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
using AppBrokerAlgorithm = TomasAI.IFM.Application.TradeBroker.Contracts.BrokerAlgorithm;
using AppBrokerEnvironment = TomasAI.IFM.Application.TradeBroker.Contracts.BrokerEnvironment;
using AppBrokerOrderShape = TomasAI.IFM.Application.TradeBroker.Contracts.BrokerOrderShape;
using AppBrokerOrderType = TomasAI.IFM.Application.TradeBroker.Contracts.BrokerOrderType;

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
    [InlineData(TradeType.FuturesOutright, "ESZ6", "FuturesView")]
    [InlineData(TradeType.PutCreditSpread, "P4500:4490", "VerticalSpreadView")]
    [InlineData(TradeType.ShortIronCondor, "P4500:4490:C4550:4560", "IronCondorView")]
    public void Blotter_factory_routes_every_strategy_to_the_unified_three_tab_shell(
        TradeType tradeType, string reference, string expectedName)
    {
        var fund = new FundReadModel(2, "Fund", "Test", 100_000m, false, DateTime.UtcNow, "test");
        var order = Order();
        using var host = new Panel();
        using var view = TradeBlotterFactory.Create(host, TestAppRoot(), fund, order,
            Trade(tradeType, reference),
            order.TradeDate, [Contract()], portfolioId: 1);
        view.Should().BeAssignableTo<EsTradeBlotterControl>();
        view!.Name.Should().Be(expectedName);
        view.BackColor.Should().Be(Color.Black);
        view.Dock.Should().Be(DockStyle.Fill);
        view.Controls.Find("tradeBlotterTabs", true).Should().ContainSingle();
    }

    [Fact]
    public void Stage4_blotter_extends_the_stage3_shell_with_read_only_volatility_context()
    {
        var fund = new FundReadModel(2, "Fund", "Test", 100_000m, false, DateTime.UtcNow, "test");
        using var view = new BrokerTradeBlotterView(Substitute.For<IAppRoot>(), fund, Order(),
            Trade(TradeType.CallCreditSpread, "C4500:4510"), 1, historicalReadOnly: true);

        var tabs = view.Controls.Find("tradeBlotterTabs", true).Single().Should().BeOfType<TabControl>().Subject;
        tabs.TabPages.Cast<TabPage>().Select(tab => tab.Text).Should().Equal(
            "Market Selection", "Leg Staging", "Orders and Fills", "Volatility Context");
        var market = view.Controls.Find("marketSelectionGrid", true).Single().Should().BeOfType<DataGridView>().Subject;
        market.VirtualMode.Should().BeTrue();
        market.ReadOnly.Should().BeTrue();
        EsTradeBlotterControl.VisibleChainRowCapacity.Should().BeInRange(10, 20);
        var staging = view.Controls.Find("legStagingGrid", true).Single().Should().BeOfType<DataGridView>().Subject;
        staging.Columns[0].HeaderText.Should().Be("Leg");
        staging.Columns[1].HeaderText.Should().Be("Delta");
        new[] { "strategySelector", "directionSelector", "brokerModeSelector", "algorithmSelector", "orderTypeSelector" }
            .Select(name => view.Controls.Find(name, true).Single())
            .Should().OnlyContain(control => !control.Enabled);
        ((ComboBox)view.Controls.Find("algorithmSelector", true).Single()).Items.Cast<string>()
            .Should().Equal("None", "Adaptive");
        ((ComboBox)view.Controls.Find("orderTypeSelector", true).Single()).Items.Cast<string>()
            .Should().Equal("Market", "Limit");
    }

    [Fact]
    public async Task In_tab_submit_invokes_the_hosted_workflow_exactly_once_and_forwards_execution_selection()
    {
        var workflow = new RecordingTradeOrderControl();
        using var host = new Form
        {
            ClientSize = new Size(1200, 700),
            ShowInTaskbar = false,
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-3000, -3000)
        };
        using var view = new EsTradeBlotterControl(Substitute.For<IAppRoot>(),
            new FundReadModel(2, "Fund", "Test", 100_000m, false, DateTime.UtcNow, "test"),
            Order(), Trade(TradeType.FuturesOutright, "ESZ6"), 1, false,
            workflowControl: workflow);
        host.Controls.Add(view);
        host.Show();
        ((ComboBox)view.Controls.Find("orderTypeSelector", true).Single()).SelectedItem = "Market";
        ((ComboBox)view.Controls.Find("algorithmSelector", true).Single()).SelectedItem = "Adaptive";
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        view.SubmitOpeningRequested += async (_, _) =>
        {
            await ((ITradeOrderControl)view).SubmitOrderAsync(Order().TradeDate,
                OrderActionType.Open, Substitute.For<ITradeOrderConfirmationService>());
            completed.SetResult();
        };

        var tabs = (TabControl)view.Controls.Find("tradeBlotterTabs", true).Single();
        var submit = (Button)view.Controls.Find("submitOpening", true).Single();
        tabs.SelectedTab = submit.Parent?.Parent as TabPage
            ?? throw new InvalidOperationException("Submit button is not hosted on a blotter tab.");
        System.Windows.Forms.Application.DoEvents();
        typeof(Button).GetMethod("OnClick", System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic)!.Invoke(submit, [EventArgs.Empty]);
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(2));

        workflow.SubmitCount.Should().Be(1);
        workflow.OrderType.Should().Be(BrokerOrderType.Market);
        workflow.Algorithm.Should().Be(BrokerAlgorithm.Adaptive);
    }

    [Fact]
    public async Task Read_only_and_unsupported_execution_modes_fail_before_workflow_dispatch()
    {
        var workflow = new RecordingTradeOrderControl();
        var fund = new FundReadModel(2, "Fund", "Test", 100_000m, false, DateTime.UtcNow, "test");
        using var readOnly = new EsTradeBlotterControl(Substitute.For<IAppRoot>(), fund,
            Order(), Trade(TradeType.FuturesOutright, "ESZ6"), 1, true, workflowControl: workflow);
        await FluentActions.Awaiting(() => ((ITradeOrderControl)readOnly).SubmitOrderAsync(
                Order().TradeDate, OrderActionType.Open, Substitute.For<ITradeOrderConfirmationService>()))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*read-only*");

        var capabilities = new TomasAI.IFM.Application.TradeBroker.Contracts.BrokerCapabilities(
            "test", "IFM-EMULATOR-PAPER", AppBrokerEnvironment.Emulator,
            [AppBrokerOrderShape.FuturesOutright], [AppBrokerOrderType.Limit], [AppBrokerAlgorithm.None]);
        using var limited = new EsTradeBlotterControl(Substitute.For<IAppRoot>(), fund,
            Order(), Trade(TradeType.FuturesOutright, "ESZ6"), 1, false, capabilities, workflow);
        var algorithms = (ComboBox)limited.Controls.Find("algorithmSelector", true).Single();
        algorithms.Items.Add("Adaptive"); algorithms.SelectedItem = "Adaptive";
        await FluentActions.Awaiting(() => ((ITradeOrderControl)limited).SubmitOrderAsync(
                Order().TradeDate, OrderActionType.Open, Substitute.For<ITradeOrderConfirmationService>()))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*not supported*");
        workflow.SubmitCount.Should().Be(0);
    }

    [Fact]
    public void Manual_selectors_lock_after_staging_and_committed_trade_selectors_start_locked()
    {
        var fund = new FundReadModel(2, "Fund", "Test", 100_000m, false, DateTime.UtcNow, "test");
        using var mutable = new EsTradeBlotterControl(Substitute.For<IAppRoot>(), fund,
            Order(), Trade(TradeType.FuturesOutright, "ESZ6"), 1, false,
            workflowControl: new RecordingTradeOrderControl());
        var selectors = new[] { "strategySelector", "directionSelector", "brokerModeSelector", "algorithmSelector", "orderTypeSelector" }
            .Select(name => mutable.Controls.Find(name, true).Single()).ToArray();
        selectors.Should().OnlyContain(control => control.Enabled);
        mutable.BindStaging(new TradeBlotterStagingResult([], false, "staged", Guid.NewGuid(),
            Guid.NewGuid(), DateTimeOffset.UtcNow, new string('a', 64)));
        selectors.Should().OnlyContain(control => !control.Enabled);

        using var committed = new EsTradeBlotterControl(Substitute.For<IAppRoot>(), fund,
            Order(), Trade(TradeType.FuturesOutright, "ESZ6") with { TradeState = TradeState.OrderFilled },
            1, false, workflowControl: new RecordingTradeOrderControl());
        new[] { "strategySelector", "directionSelector", "brokerModeSelector", "algorithmSelector", "orderTypeSelector" }
            .Select(name => committed.Controls.Find(name, true).Single())
            .Should().OnlyContain(control => !control.Enabled);
    }

    [Fact]
    public void Iron_condor_blotter_includes_the_shared_durable_broker_evidence_tab()
    {
        var root = TestAppRoot();
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
        model.SetExecutionSelection(BrokerOrderType.Market, BrokerAlgorithm.Adaptive);
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
        submitted.Body.BrokerOrderType.Should().Be(BrokerOrderType.Market);
        submitted.Body.BrokerAlgorithm.Should().Be(BrokerAlgorithm.Adaptive);
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

    static IAppRoot TestAppRoot()
    {
        var root = Substitute.For<IAppRoot>();
        var services = Substitute.For<IUiServiceCatalog>();
        root.Services.Returns(services);
        var commandResponses = new TomasAI.IFM.UI.Net.Services.Application.CommandResponseEventService(
            Substitute.For<TomasAI.IFM.UI.EventConsumer.ICommandResponseUIEventConsumer>());
        commandResponses.SetSiteId(Guid.NewGuid());
        services.CommandResponses.Returns(commandResponses);
        return root;
    }

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
        BrokerOrderType = submitted.Body.BrokerOrderType,
        BrokerAlgorithm = submitted.Body.BrokerAlgorithm,
        MicroExecutionProfileId = submitted.Body.MicroExecutionProfileId,
        MicroExecutionProfileVersion = submitted.Body.MicroExecutionProfileVersion,
        MicroExecutionProfileHash = submitted.Body.MicroExecutionProfileHash,
        PortfolioApprovalId = Guid.NewGuid()
    };

    sealed class RecordingTradeOrderControl : Control, ITradeOrderControl, ITradeExecutionSelectionControl
    {
        public int SubmitCount { get; private set; }
        public BrokerOrderType OrderType { get; private set; } = BrokerOrderType.Limit;
        public BrokerAlgorithm Algorithm { get; private set; } = BrokerAlgorithm.None;
        public DateOnly MaturityDate => new(2026, 10, 16);
        public Task RemoveTradeAsync(int fundId, int orderId, int tradeId) => Task.CompletedTask;
        public Task<Guid> SubmitOrderAsync(DateOnly tradeDate, OrderActionType orderAction,
            ITradeOrderConfirmationService tradeOrderConfirmation)
        {
            SubmitCount++;
            return Task.FromResult(Guid.NewGuid());
        }
        public Task SetLiveFeedAsync(bool enabled) => Task.CompletedTask;
        public void SetNearestStrikePrices() { }
        public Task OrderActionTypeChangedAsync(OrderActionType orderActionType) => Task.CompletedTask;
        public void SetExecutionSelection(BrokerOrderType orderType, BrokerAlgorithm algorithm)
        {
            OrderType = orderType;
            Algorithm = algorithm;
        }
    }
}
