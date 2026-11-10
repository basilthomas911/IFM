using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Order.Broker;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Execution;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Execution.Projection;
using TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.IntegratedTests.Strategy.Workflow.IntrinsicTime;

public sealed partial class TradeSelectionRuntimeTests
{
    async Task<StrategyExecutionHandoffDefinition> AssertLostOpeningReplyAsync(IServiceProvider services,
        IntrinsicTimeStrategyWorkflowView view, CancellationToken token)
    {
        var store = new StrategyExecutionHandoffReadStore(FinancialBoundaryTransactions());
        var disk = services.GetRequiredService<SimpleInjector.Container>().GetInstance<IEmulatorLedgerStore>();
        var timer = System.Diagnostics.Stopwatch.StartNew();
        StrategyExecutionHandoffDefinition? current = null;
        var quoted = false;
        while (timer.Elapsed < TimeSpan.FromSeconds(25))
        {
            current = (await store.ReadWorkflowAsync(view.WorkflowId.Value, token)).SingleOrDefault();
            if (current?.Phase == StrategyExecutionHandoffPhase.ExecutionLinked && current.Execution is not null)
            {
                var orders = disk.Load()?.Orders.Where(order => order.Request.BrokerOrderId.StartsWith(current.Id.Format() + ".", StringComparison.Ordinal)).ToArray();
                if (orders is { Length: > 0 })
                {
                    if (!quoted) { await PublishOpeningFixtureQuotesAsync(services, current, token); quoted = true; }
                    if (disk.Load()!.Orders.Where(order => orders.Any(prior => prior.Request.BrokerOrderId == order.Request.BrokerOrderId)).All(order => order.Filled)) break;
                }
            }
            await Task.Delay(100, token);
        }
        quoted.Should().BeTrue($"the real emulator must receive a controlled eligible quote set; phase={current?.Phase}; " +
            $"reason={current?.ReconciliationReason}; execution={current?.Execution?.Status}; " +
            $"validUntil={current?.Order.ValidUntilUtc:O}; ledgerOrders={disk.Load()?.Orders.Length}");
        current.Should().NotBeNull();
        current!.Phase.Should().Be(StrategyExecutionHandoffPhase.ExecutionLinked);
        current.Execution!.CumulativeFilledQuantity.Should().Be(0, "broker fill delivery is deliberately unavailable before restart");
        current.Execution.Fills.Should().BeEmpty();
        disk.Load()!.Orders.Should().Contain(order => order.Filled && order.Request.BrokerOrderId.StartsWith(current.Id.Format() + ".", StringComparison.Ordinal));
        output.WriteLine($"Lost broker reply retained original pending execution: {current.Id.Format()}; emulator fill persisted; transport recovery requires reconciliation.");
        return current;
    }

    async Task VerifyOpeningRestartAsync(string broker, string ledgerPath, string accountAlias,
        IReadOnlyList<(IntrinsicTimeStrategyWorkflowView View, StrategyExecutionHandoffDefinition Handoff)> results)
    {
        var disk = new FileEmulatorLedgerStore(ledgerPath);
        var before = disk.Load();
        if (results.Any(value => value.Handoff.Execution is not null))
            before.Should().NotBeNull("the real emulator ledger must persist its orders and fills before shutdown");
        var counts = new Dictionary<string, string>();
        foreach (var (_, handoff) in results) counts[handoff.Id.Format()] = await OpeningCountsAsync(handoff);
        var timer = System.Diagnostics.Stopwatch.StartNew();
        await using var restarted = Host(services =>
        {
            var container = (SimpleInjector.Container)services.Single(value => value.ServiceType == typeof(SimpleInjector.Container)).ImplementationInstance!;
            container.RegisterInstance(EmulatorScenario.Development(accountAlias));
            container.RegisterInstance<IEmulatorLedgerStore>(new FileEmulatorLedgerStore(ledgerPath));
        }, broker, actualPortfolio: true, isolatedTransport: true).WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TradeBroker:Emulator:LedgerPath"] = ledgerPath,
                ["TradeBroker:Emulator:AccountAlias"] = accountAlias
            })));
        _ = restarted.CreateClient();
        try
        {
            var actors = restarted.Services.GetRequiredService<IActorService>();
            var tradeBroker = restarted.Services.GetRequiredService<IContainerInstance>()
                .Resolve<TomasAI.IFM.Application.TradeBroker.Contracts.ITradeBroker>();
            tradeBroker.AccountAlias.Should().Be(accountAlias);
            var store = new StrategyExecutionHandoffReadStore(FinancialBoundaryTransactions());
            foreach (var (view, handoff) in results)
            {
                var restored = await store.ReadAsync(handoff.Id);
                restored.Should().NotBeNull();
                restored!.IntentHash.Should().Be(handoff.IntentHash);
                restored.Revision.Should().BeGreaterThanOrEqualTo(handoff.Revision);
                var pending = handoff.Phase < StrategyExecutionHandoffPhase.Opened;
                var completed = handoff;
                if (handoff.Execution is not null)
                {
                    foreach (var component in handoff.Order.Components)
                    {
                        var id = new BrokerOrderId(handoff.Id, component.ComponentId);
                        var observations = await tradeBroker.ReconcileOrderAsync(id.Format());
                        var expectedFills = pending
                            ? before!.Observations.Where(value => value.BrokerOrderId == id.Format() && value.Kind == TomasAI.IFM.Framework.TradeBroker.Contracts.FrameworkObservationKind.Execution)
                                .Select(value => (value.ExternalExecutionId, value.Price, value.SignedQuantity))
                            : handoff.Execution.Fills.Where(fill => fill.ComponentId == component.ComponentId)
                                .Select(fill => (fill.ExternalExecutionId, fill.Price, fill.SignedQuantity));
                        observations.Where(value => value.Kind == TomasAI.IFM.Application.TradeBroker.Contracts.BrokerObservationKind.Execution)
                            .Select(value => (value.ExternalExecutionId, value.Price, value.SignedQuantity)).Should().BeEquivalentTo(expectedFills);
                        var reconcile = new ReconcileBrokerOrderCommand { CommandId = Guid.NewGuid(), EntityId = id,
                            Subject = new(ActorType.Command, ReconcileBrokerOrderCommand.Actor, ReconcileBrokerOrderCommand.Verb, id.Format()) };
                        var first = await actors.RequestAsync<ReconcileBrokerOrderCommand, BrokerOrderId>(reconcile);
                        first.Success.Should().BeTrue(first.ErrorMessage);
                        var repeated = await actors.RequestAsync<ReconcileBrokerOrderCommand, BrokerOrderId>(reconcile);
                        repeated.Success.Should().BeTrue(repeated.ErrorMessage);
                    }
                    if (pending)
                        completed = await AssertOpeningAsync(restarted.Services, view, CancellationToken.None, publishQuotes: false);
                    var execution = await restarted.Services.GetRequiredService<IDbContextFactory>().TradeDb
                        .GetOrderExecutionAsync(handoff.Order.Id, handoff.Id.ExecutionAttemptId);
                    FinancialCanonicalHash.Compute(execution).Should().Be(FinancialCanonicalHash.Compute(completed.Execution));
                }
                var fund = await restarted.Services.GetRequiredService<IPortfolioQueryApi>().GetOrderAsync(handoff.Order.Id.OrderId);
                fund.Success.Should().BeTrue(fund.ErrorMessage);
                fund.Value!.Status.Should().Be(completed.FundEvidence!.Status);
                if (!pending) (await OpeningCountsAsync(handoff)).Should().Be(counts[handoff.Id.Format()]);
                await VerifyOpeningReplayAsync(restarted.Services, view, completed, CancellationToken.None);
            }
            var after = disk.Load();
            after?.Orders.Should().BeEquivalentTo(before?.Orders);
            after?.Journal.Where(entry => entry.Kind is "Place" or "Execution" or "Commission").Should()
                .BeEquivalentTo(before?.Journal.Where(entry => entry.Kind is "Place" or "Execution" or "Commission"));
            output.WriteLine($"Host restart and repeated broker Reconcile verified: {timer.Elapsed.TotalMilliseconds:F3} ms; original orders, fills, ledger postings and Fund outcomes preserved.");
        }
        finally { await global::SupervisorTestLifecycle.ShutdownAsync(restarted.Services); }
    }

    static Task<string> OpeningCountsAsync(StrategyExecutionHandoffDefinition handoff) =>
        FinancialBoundaryTransactions().ExecuteOnceAsync(async (db, token) => (string)(await db.ScalarAsync("""
            SELECT jsonb_build_object(
              'fills',(SELECT count(*) FROM trade.order_execution_fill WHERE executionattemptid=$1),
              'postings',(SELECT count(*) FROM portfolio_financial.ledger_transaction WHERE portfolio_id=$2),
              'positions',(SELECT count(*) FROM trade.strategy_position_current WHERE portfolioid=$2 AND fundid=$3 AND orderid=$4))::text;
            """, [handoff.Id.ExecutionAttemptId, handoff.Order.Id.PortfolioId, handoff.Order.Id.FundId, handoff.Order.Id.OrderId], token))!);
}
