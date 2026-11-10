using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;
using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Order;
using TomasAI.IFM.Domain.Trade.Shared.Order.Broker;
using TomasAI.IFM.Domain.Trade.Shared.Portfolio;
using TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.TradeSelection;
using TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.IntegratedTests.Strategy.Workflow.IntrinsicTime;

public sealed partial class TradeSelectionRuntimeTests
{
    // This is the manual application command path, not RegisterStrategyFundOrder
    // and not injected executions/fills. Only initial authority and market quotes
    // are labelled fixtures. Every lifecycle transition uses the real actors.
    [Theory, Trait("Category", "ManualTradeRoundTrip")]
    [InlineData(TimeFrameType.Monthly, "ShortBalancedIronCondor", "ShortIronCondor", "LongIronCondor", 4)]
    [InlineData(TimeFrameType.Weekly, "BullCallDebit", "CallDebitSpread", "CallCreditSpread", 2)]
    [InlineData(TimeFrameType.Daily, "LongFuture", "FuturesOutright", "FuturesOutright", 1)]
    public async Task Manual_create_add_open_close_settle_restart_and_reconcile_has_no_duplicates(
        TimeFrameType horizon, string variant, string openingType, string closingType, int legCount)
    {
        await using var isolation = await SharedOpeningActorIsolation.EnterAsync();
        var url = Environment.GetEnvironmentVariable("IFM_CONFIGURED_TEST_NATS_URL")!;
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "IFM-ManualRoundTrip", Guid.NewGuid().ToString("N"), "ledger.json");
        var alias = $"IFM-MANUAL-{Guid.NewGuid():N}";
        var owner = Random.Shared.Next(10_000_000, 800_000_000);
        var selection = await TradeSelectionFixture.Command(variant, horizon, DateTime.UtcNow,
            scopeId: owner, compositionReady: true, compositionIntegrationTiming: true, policyId: owner);
        var source = selection.SelectionBinding.PortfolioSnapshot;
        var fund = source.Fund.FundId;
        var setupTrade = owner + 1;
        var closingSetup = owner + 2;
        var now = DateTime.UtcNow;
        var maturity = legCount == 1 ? (DateOnly?)null : DateOnly.FromDateTime(now.AddDays(horizon == TimeFrameType.Monthly ? 30 : 7));
        var kind = legCount == 4 ? PortfolioExecutionStrategyKind.IronCondor : legCount == 2
            ? PortfolioExecutionStrategyKind.VerticalSpread : PortfolioExecutionStrategyKind.FuturesOutright;
        var signs = legCount == 4 ? new[] { 1, -1, -1, 1 } : legCount == 2 ? new[] { 1, -1 } : new[] { 1 };
        var limit = legCount == 4 ? -2m : legCount == 2 ? 2m : 100m;
        var component = new PortfolioExecutionComponent
        {
            ComponentId = Guid.NewGuid(), StrategyKind = kind, SignedNetDebitLimit = limit,
            MinimumSignedNetDebitLimit = limit, MaximumSignedNetDebitLimit = limit, TickIncrement = .25m,
            Legs = signs.Select((sign, index) => new PortfolioExecutionLeg
            {
                TradeLegId = Guid.NewGuid(), AssetFamily = legCount == 1 ? PortfolioExecutionAssetFamily.Futures : PortfolioExecutionAssetFamily.FuturesOption,
                SignedQuantity = sign, ContractId = legCount == 1 ? "ES-MANUAL-FIXTURE" : $"ES-MANUAL-{owner}-{index}",
                ContractKey = "ES-MANUAL-FIXTURE", CashMultiplier = 50,
                Expiry = maturity, Strike = legCount == 1 ? null : legCount == 4 ? new[] { 5020m, 5015m, 5005m, 5000m }[index] : 5000 + index * 5,
                PutCall = legCount == 1 ? null : (byte)(legCount == 4 && index >= 2 ? 2 : 1)
            }).ToArray()
        };
        TomasAI.IFM.IntegrationTesting.KestrelWebApplicationFactory<TomasAI.IFM.Application.Api.Server.ApiServerEntryPoint> NewHost() => Host(services =>
        {
            var container = (SimpleInjector.Container)services.Single(value => value.ServiceType == typeof(SimpleInjector.Container)).ImplementationInstance!;
            // Match the trusted Development emulator policy; the generic
            // integration factory otherwise uses the Testing environment.
            services.RemoveAll<FinancialDevelopmentPolicy>();
            services.AddSingleton(new FinancialDevelopmentPolicy(true));
            container.RegisterInstance(new FinancialDevelopmentPolicy(true));
            container.RegisterInstance(EmulatorScenario.Development(alias));
            container.RegisterInstance<IEmulatorLedgerStore>(new FileEmulatorLedgerStore(path));
        }, url, actualPortfolio: true, isolatedTransport: true);
        var executed = new List<(TradeOrderDefinition Order, Guid Attempt)>();
        var executionHashes = new Dictionary<Guid, string>();
        int setupOrder = 0;
        StrategyPositionId positionId = default;
        string? before = null;
        var timer = System.Diagnostics.Stopwatch.StartNew();
        await using (var host = NewHost())
        {
            _ = host.CreateClient();
            await host.Services.GetRequiredService<IActorProducer>().StartAsync(new(ActorType.Realtime, $"ManualLifecycle{Guid.NewGuid():N}"));
            try
            {
                await InitializeWorkflowPortfolioAsync(host.Services, selection.SelectionBinding);
                var commands = host.Services.GetRequiredService<IPortfolioFundCommandApi>();
                var query = host.Services.GetRequiredService<IPortfolioQueryApi>();
                var api = host.Services.GetRequiredService<IPortfolioOrderCompositionApi>();
                var create = new CreateManualFundOrderRequest { PortfolioId = owner, PortfolioVersion = source.Portfolio.PortfolioVersion,
                    FundId = fund, FundMandateVersion = source.Fund.FundMandateVersion, IdempotencyKey = Guid.NewGuid(),
                    Reference = $"Manual {horizon} round trip", RequestedAtUtc = DateTime.UtcNow, ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5) };
                var draft = await commands.CreateManualOrderAsync(create);
                draft.Success.Should().BeTrue(draft.ErrorMessage);
                setupOrder = draft.Value!.Order.OrderId;
                draft.Value.Trades.Should().BeEmpty();
                (await commands.CreateManualOrderAsync(create)).Value!.Order.OrderId.Should().Be(setupOrder);
                AddManualFundOrderTradeRequest Add(int trade, bool primary, string type, long version) => new()
                {
                    PortfolioId = owner, FundId = fund, OrderId = setupOrder, ExpectedOrderVersion = version,
                    TradeId = trade, PrimaryTrade = primary, TradeType = type, TradeState = "NewTrade",
                    TradeAction = type == "ShortIronCondor" || type == "CallCreditSpread" || (!primary && legCount == 1) ? "Sell" : "Buy",
                    BaseContractId = "ES-MANUAL-FIXTURE", BaseContractSymbol = "ES", EffectiveDate = DateOnly.FromDateTime(DateTime.UtcNow),
                    MaturityDate = maturity, Reference = $"{(primary ? "Open" : "Close")} {horizon} manual lifecycle", RequestedAtUtc = DateTime.UtcNow
                };
                var add = Add(setupTrade, true, openingType, draft.Value.Order.AggregateVersion);
                var added = await commands.AddManualTradeAsync(add);
                added.Success.Should().BeTrue(added.ErrorMessage);
                added.Value!.Trades.Should().ContainSingle().Which.TradeState.Should().Be("NewTrade");
                var bad = await commands.AddManualTradeAsync(Add(owner + 3, true, openingType, added.Value.Order.AggregateVersion));
                bad.Success.Should().BeFalse("a hidden existing draft must not permit a second opening trade");
                var operation = Guid.NewGuid(); now = DateTime.UtcNow;
                var request = new EvaluatePortfolioOrderCompositionCommand
                {
                    CommandId = operation, OperationId = operation, PortfolioId = owner, EntityId = new(owner, operation),
                    Subject = new(ActorType.Function, EvaluatePortfolioOrderCompositionCommand.Actor, EvaluatePortfolioOrderCompositionCommand.Verb, new FinancialExecutionId(owner, operation).Format()),
                    RequestedAtUtc = now, ExpiresAtUtc = now.AddMinutes(2), CorrelationId = operation, CausationId = operation,
                    Access = new("ManualLifecycleQualification", ["OrderCompositionEvaluate"], [owner]),
                    Body = new() { CompositionId = Guid.NewGuid(), WorkflowId = Guid.NewGuid(), DecisionHorizon = horizon.ToString(), StrategyKind = kind,
                        ValueDate = DateOnly.FromDateTime(now), ValidUntilUtc = now.AddMinutes(2), Origin = "DesktopTradeOrder", Components = [component],
                        DeploymentKey = selection.SelectionBinding.Candidates[0].DeploymentKey, ProductSymbol = "ES", ProductExchange = "CME", ProductCurrency = "USD",
                        RequiredCapital = 100, MaximumLoss = 100, StressLoss = 100, Notional = 1000, EvidenceHash = new('a', 64),
                        PositionType = PortfolioExecutionPositionType.Opening, BrokerEnvironment = PortfolioBrokerEnvironment.Emulator, BrokerAccountAlias = alias,
                        MicroExecutionProfileId = "ManualLifecycleFixture", MicroExecutionProfileVersion = 1, MicroExecutionProfileHash = new('c', 64),
                        SetupTrade = new() { FundId = fund, OrderId = setupOrder, TradeId = setupTrade } }
                };
                request = request with { InputSha256 = FinancialCanonicalHash.Request(request) };
                var accepted = await api.EvaluateAsync(request);
                accepted.Success.Should().BeTrue(accepted.ErrorMessage);
                accepted.Value!.IsCompleted.Should().BeTrue(accepted.Value.Failed?.ErrorMessage);
                var instruction = accepted.Value.Completed!.Receipt.TradeOrders.Should().ContainSingle().Subject;
                (await api.EvaluateAsync(request)).Value!.Completed!.Id.Should().Be(accepted.Value.Completed.Id);
                var opening = instruction.ToTradeOrder();
                var openAttempt = await ExecuteManualAsync(host.Services, opening);
                executed.Add((opening, openAttempt));
                positionId = StrategyPositionId.Create(new(owner, fund, opening.Id.OrderId, opening.Components[0].ReservedTradeId), (TradeStrategyKind)kind);
                var position = await WaitManualPositionAsync(host.Services, positionId, true);
                position.Legs.Should().HaveCount(legCount);
                var opened = await WaitManualOrderAsync(query, setupOrder, "Opened");
                var closing = await commands.AddManualTradeAsync(Add(closingSetup, false, closingType, opened.AggregateVersion));
                closing.Success.Should().BeTrue(closing.ErrorMessage);
                closing.Value!.Trades.Should().HaveCount(2);
                operation = Guid.NewGuid(); now = DateTime.UtcNow;
                var closeRequest = new EvaluatePortfolioCloseOrderCompositionCommand
                {
                    CommandId = operation, OperationId = operation, PortfolioId = owner, EntityId = new(owner, operation),
                    Subject = new(ActorType.Function, EvaluatePortfolioCloseOrderCompositionCommand.Actor, EvaluatePortfolioCloseOrderCompositionCommand.Verb, new FinancialExecutionId(owner, operation).Format()),
                    CorrelationId = operation, CausationId = operation, RequestedAtUtc = now, ExpiresAtUtc = now.AddMinutes(2),
                    Access = new("ManualLifecycleQualification", ["OrderCompositionClose"], [owner]),
                    Body = new() { CompositionId = Guid.NewGuid(), WorkflowId = new(positionId.ToPortfolioPositionReference(), DateOnly.FromDateTime(now), Guid.NewGuid()),
                        Position = position.ToPortfolioPosition(), StrategyKind = kind, ValueDate = DateOnly.FromDateTime(now), ValidUntilUtc = now.AddMinutes(2),
                        Origin = "DesktopTradeOrder", EvidenceHash = new('b', 64), PositionType = PortfolioExecutionPositionType.Closing,
                        SetupTrade = new() { FundId = fund, OrderId = setupOrder, TradeId = closingSetup },
                        Component = instruction.Components[0] with { SignedNetDebitLimit = -limit, MinimumSignedNetDebitLimit = -limit, MaximumSignedNetDebitLimit = -limit,
                            Legs = instruction.Components[0].Legs.Select(leg => leg with { SignedQuantity = -leg.SignedQuantity }).ToArray() } }
                };
                closeRequest = closeRequest with { InputSha256 = FinancialCanonicalHash.Request(closeRequest) };
                var close = await api.EvaluateCloseAsync(closeRequest);
                close.Success.Should().BeTrue(close.ErrorMessage);
                close.Value!.IsCompleted.Should().BeTrue(close.Value.Failed?.ErrorMessage);
                var closeOrder = close.Value.Completed!.Receipt.TradeOrder!.ToTradeOrder();
                (await api.EvaluateCloseAsync(closeRequest)).Value!.Completed!.Id.Should().Be(close.Value.Completed.Id);
                executed.Add((closeOrder, await ExecuteManualAsync(host.Services, closeOrder)));
                var closed = await WaitManualPositionAsync(host.Services, positionId, false);
                closed.ClosingFills.Should().HaveCount(legCount);
                (await WaitManualOrderAsync(query, setupOrder, "Closed")).Status.Should().Be("Closed");
                await AssertManualSettlementAsync(host.Services, owner, fund, executed);
                foreach (var (order, attempt) in executed)
                    executionHashes[attempt] = FinancialCanonicalHash.Compute(await host.Services.GetRequiredService<IDbContextFactory>().TradeDb.GetOrderExecutionAsync(order.Id, attempt));
                before = await ManualCountsAsync(owner);
                using (var counts = System.Text.Json.JsonDocument.Parse(before))
                {
                    counts.RootElement.GetProperty("orders").GetInt32().Should().Be(2);
                    counts.RootElement.GetProperty("fills").GetInt32().Should().Be(legCount * 2);
                    counts.RootElement.GetProperty("positions").GetInt32().Should().Be(1);
                    counts.RootElement.GetProperty("setups").GetInt32().Should().Be(2);
                    counts.RootElement.GetProperty("executions").GetInt32().Should().Be(2);
                }
                output.WriteLine($"Manual {horizon} create/add/open/add close/settle: {timer.Elapsed.TotalMilliseconds:F3} ms; setup={setupOrder}; legs={legCount}");
            }
            finally { await global::SupervisorTestLifecycle.ShutdownAsync(host.Services); await host.Services.GetRequiredService<IActorProducer>().StopAsync(); }
        }
        timer.Restart();
        var ledgerBefore = new FileEmulatorLedgerStore(path).Load()!;
        ledgerBefore.Orders.Should().HaveCount(2);
        ledgerBefore.Journal.Count(row => row.Kind == "Place").Should().Be(2);
        await using (var restarted = NewHost())
        {
            _ = restarted.CreateClient();
            await restarted.Services.GetRequiredService<IActorProducer>().StartAsync(new(ActorType.Realtime, $"ManualRestart{Guid.NewGuid():N}"));
            try
            {
                var actors = restarted.Services.GetRequiredService<IActorService>();
                foreach (var (order, attempt) in executed)
                    foreach (var item in order.Components)
                    {
                        var id = new BrokerOrderId(new(order.Id, attempt), item.ComponentId);
                        for (var repeat = 0; repeat < 2; repeat++)
                        {
                            var result = await actors.RequestAsync<ReconcileBrokerOrderCommand, BrokerOrderId>(new()
                            { CommandId = Guid.NewGuid(), EntityId = id, Subject = new(ActorType.Command, ReconcileBrokerOrderCommand.Actor, ReconcileBrokerOrderCommand.Verb, id.Format()) });
                            result.Success.Should().BeTrue(result.ErrorMessage);
                        }
                        var execution = await restarted.Services.GetRequiredService<IDbContextFactory>().TradeDb.GetOrderExecutionAsync(order.Id, attempt);
                        execution!.Status.Should().Be(OrderExecutionStatus.Filled);
                        FinancialCanonicalHash.Compute(execution).Should().Be(executionHashes[attempt], "restart and Reconcile preserve every original fill identity, price, quantity and commission");
                        execution.Fills.Should().HaveCount(legCount);
                        execution.Fills.Select(fill => fill.ExternalExecutionId).Should().OnlyHaveUniqueItems();
                    }
                (await WaitManualPositionAsync(restarted.Services, positionId, false)).ClosingFills.Should().HaveCount(legCount);
                await WaitManualOrderAsync(restarted.Services.GetRequiredService<IPortfolioQueryApi>(), setupOrder, "Closed");
                await AssertManualSettlementAsync(restarted.Services, owner, fund, executed);
                (await ManualCountsAsync(owner)).Should().Be(before);
                var ledgerAfter = new FileEmulatorLedgerStore(path).Load()!;
                ledgerAfter.Orders.Should().BeEquivalentTo(ledgerBefore.Orders);
                ledgerAfter.Journal.Where(row => row.Kind is "Place" or "Execution" or "Commission").Should()
                    .BeEquivalentTo(ledgerBefore.Journal.Where(row => row.Kind is "Place" or "Execution" or "Commission"));
                output.WriteLine($"Manual {horizon} restart + repeated Reconcile: {timer.Elapsed.TotalMilliseconds:F3} ms; no duplicate orders, fills, positions or ledger postings; {before}");
            }
            finally { await global::SupervisorTestLifecycle.ShutdownAsync(restarted.Services); await restarted.Services.GetRequiredService<IActorProducer>().StopAsync(); }
        }
    }

    static async Task<Guid> ExecuteManualAsync(IServiceProvider services, TradeOrderDefinition order)
    {
        var actors = services.GetRequiredService<IActorService>();
        var attempt = Guid.NewGuid();
        var created = await actors.SendAsync<CreateTradeOrderCommand, TradeOrderId>(new() { CommandId = Guid.NewGuid(), PostEvents = true, EntityId = order.Id, Order = order,
            Subject = new(ActorType.Command, "TradeOrderCommand", CreateTradeOrderCommand.Verb, order.Id.Format()) }, order.Id);
        created.Success.Should().BeTrue(created.ErrorMessage);
        var approved = await actors.SendAsync<ApproveTradeOrderCommand, TradeOrderId>(new() { CommandId = Guid.NewGuid(), PostEvents = true, EntityId = order.Id,
            Subject = new(ActorType.Command, "TradeOrderCommand", ApproveTradeOrderCommand.Verb, order.Id.Format()) }, order.Id);
        approved.Success.Should().BeTrue(approved.ErrorMessage);
        var ready = await actors.SendAsync<ReadyTradeOrderCommand, TradeOrderId>(new() { CommandId = Guid.NewGuid(), PostEvents = true, EntityId = order.Id,
            Subject = new(ActorType.Command, "TradeOrderCommand", ReadyTradeOrderCommand.Verb, order.Id.Format()) }, order.Id);
        ready.Success.Should().BeTrue(ready.ErrorMessage);
        var bind = new BindTradeOrderExecutionCommand { CommandId = Guid.NewGuid(), PostEvents = true, EntityId = order.Id, ExecutionAttemptId = attempt,
            ExecutionChannel = ExecutionChannel.Broker, EffectiveAtUtc = DateTime.UtcNow,
            Subject = new(ActorType.Command, "TradeOrderCommand", BindTradeOrderExecutionCommand.Verb, order.Id.Format()) };
        var bound = await actors.SendAsync<BindTradeOrderExecutionCommand, TradeOrderId>(bind, order.Id);
        bound.Success.Should().BeTrue(bound.ErrorMessage);
        var db = services.GetRequiredService<IDbContextFactory>().TradeDb;
        var stop = DateTime.UtcNow.AddSeconds(30); var quoted = false;
        while (DateTime.UtcNow < stop)
        {
            var execution = await db.GetOrderExecutionAsync(order.Id, attempt);
            if (!quoted && execution?.Status == OrderExecutionStatus.Submitted)
            {
                await PublishOpeningFixtureQuotesAsync(services, new() { Order = order }, CancellationToken.None,
                    requireNewMatch: order.PositionType == TradeOrderPositionType.Opening);
                quoted = true;
            }
            if (execution?.Status == OrderExecutionStatus.Filled)
            {
                (await actors.SendAsync<BindTradeOrderExecutionCommand, TradeOrderId>(bind, order.Id)).Success.Should().BeTrue();
                return attempt;
            }
            execution?.Status.Should().NotBe(OrderExecutionStatus.Rejected, execution.RejectionDetail);
            await Task.Delay(50);
        }
        var pending = await db.GetOrderExecutionAsync(order.Id, attempt);
        var errors = await FinancialBoundaryTransactions().ExecuteOnceAsync(async (tx, token) =>
            await tx.ScalarAsync("SELECT string_agg(projectorname || ':' || status || ':' || lasterror, E'\\n') FROM event_projector_outbox WHERE coalesce(lasterror,'')<>'';", [], token));
        throw new TimeoutException($"Manual execution did not fill: {order.Id.Format()}; quoted={quoted}; status={pending?.Status}; channel={pending?.Channel}; components={pending?.Components.Length}; outbox={errors}");
    }

    static async Task<StrategyPositionSnapshot> WaitManualPositionAsync(IServiceProvider services, StrategyPositionId id, bool open)
    {
        var db = services.GetRequiredService<IDbContextFactory>().TradeDb;
        for (var poll = 0; poll < 300; poll++)
        {
            var position = await db.GetStrategyPositionAsync(id);
            if (position is not null && position.IsOpen == open) return position;
            await Task.Delay(50);
        }
        throw new TimeoutException($"Position {id.Format()} did not reach open={open}");
    }

    static async Task<TomasAI.IFM.Domain.Portfolio.Shared.ViewModels.FundOrderProjectionReadModel> WaitManualOrderAsync(IPortfolioQueryApi query, int id, string status)
    {
        for (var poll = 0; poll < 300; poll++)
        {
            var result = await query.GetOrderAsync(id);
            if (result.Success && result.Value?.Status == status) return result.Value;
            await Task.Delay(50);
        }
        throw new TimeoutException($"Manual setup order {id} did not reach {status}");
    }

    static async Task AssertManualSettlementAsync(IServiceProvider services, int portfolio, int fund, List<(TradeOrderDefinition Order, Guid Attempt)> orders)
    {
        var db = services.GetRequiredService<IDbContextFactory>().TradeDb;
        decimal paid = 0;
        foreach (var (order, attempt) in orders)
        {
            TradeOrderDefinition? terminal = null;
            for (var poll = 0; poll < 300; poll++)
            {
                terminal = await db.GetTradeOrderAsync(order.Id);
                if (terminal?.Status == TradeOrderStatus.Completed) break;
                await Task.Delay(50);
            }
            terminal!.Status.Should().Be(TradeOrderStatus.Completed, "settlement must finish the execution order as well as the Fund setup");
            var execution = (await db.GetOrderExecutionAsync(order.Id, attempt))!;
            var legs = order.Components.SelectMany(component => component.Legs).ToDictionary(leg => leg.TradeLegId);
            paid += execution.Fills.Sum(fill => decimal.Round(fill.Price * fill.SignedQuantity * legs[fill.TradeLegId].CashMultiplier, 2) + fill.Commission);
        }
        var broker = services.GetRequiredService<IContainerInstance>().Resolve<TomasAI.IFM.Application.TradeBroker.Contracts.ITradeBroker>();
        var scenario = services.GetRequiredService<SimpleInjector.Container>().GetInstance<EmulatorScenario>();
        var account = await broker.GetAccountSnapshotAsync();
        account.CashBalance.Should().Be(scenario.StartingCash - paid);
        account.Positions.Should().OnlyContain(position => position.SignedQuantity == 0);
        account.AvailableFunds.Should().Be(account.CashBalance, "the closed orders leave no broker reservation");
        var balance = await services.GetRequiredService<IPortfolioFinancialApi>().GetAccountBalancesAsync(new()
        { PortfolioId = portfolio, FundId = fund, Access = new("ManualLifecycleQualification", ["LedgerRead"], [portfolio]) }, new());
        balance.Success.Should().BeTrue(balance.ErrorMessage);
        balance.Value!.Value!.Accounts.Single(row => row.AccountId == 101).Balance.Should().Be(1_000_000_000m - paid);
    }

    static Task<string> ManualCountsAsync(int portfolio) => FinancialBoundaryTransactions().ExecuteOnceAsync(async (db, token) =>
        (string)(await db.ScalarAsync("""
        SELECT jsonb_build_object('orders',(SELECT count(DISTINCT orderid) FROM trade.trade_order WHERE portfolioid=$1),
        'fills',(SELECT count(*) FROM trade.order_execution_fill f JOIN trade.order_execution e USING(executionattemptid) WHERE e.portfolioid=$1),
        'positions',(SELECT count(*) FROM trade.strategy_position_current WHERE portfolioid=$1),
        'setups',(SELECT count(*) FROM portfolio.fund_order_trade WHERE portfolio_id=$1),
        'executions',(SELECT count(*) FROM trade.order_execution WHERE portfolioid=$1),
        'postings',(SELECT count(*) FROM portfolio_financial.ledger_transaction WHERE portfolio_id=$1))::text;
        """, [portfolio], token))!);
}
