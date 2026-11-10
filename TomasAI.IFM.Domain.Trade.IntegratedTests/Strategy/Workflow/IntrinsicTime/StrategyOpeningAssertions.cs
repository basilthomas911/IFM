using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Execution;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Execution.Projection;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Execution.State;
using TomasAI.IFM.Domain.Trade.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;

namespace TomasAI.IFM.Domain.Trade.IntegratedTests.Strategy.Workflow.IntrinsicTime;

public sealed partial class TradeSelectionRuntimeTests
{
    async Task<StrategyExecutionHandoffDefinition> AssertExpiredOpeningAsync(IServiceProvider services, IntrinsicTimeStrategyWorkflowView view, CancellationToken token)
    {
        var store = new StrategyExecutionHandoffReadStore(FinancialBoundaryTransactions());
        var timer = System.Diagnostics.Stopwatch.StartNew();
        StrategyExecutionHandoffDefinition? current = null;
        while (timer.Elapsed < TimeSpan.FromSeconds(20))
        {
            current = (await store.ReadWorkflowAsync(view.WorkflowId.Value, token)).SingleOrDefault();
            if (current?.Phase == StrategyExecutionHandoffPhase.Expired) break;
            await Task.Delay(100, token);
        }
        current.Should().NotBeNull();
        current!.Phase.Should().Be(StrategyExecutionHandoffPhase.Expired, current.ReconciliationReason);
        current.Execution.Should().BeNull();
        current.FundEvidence!.Status.Should().Be("Cancelled");
        var capacity = current.FinancialReceipt!.ProjectionFacts!.ExecutionCapacity!.Outcome;
        capacity.UnsubmittedExpiry.Should().BeTrue();
        capacity.FilledUnits.Should().Be(0);
        capacity.CancelledUnits.Should().Be(capacity.ApprovedUnits);
        current.FinancialReceipt.Receipt.Items.Should().BeEmpty();
        var actual = await services.GetRequiredService<IDbContextFactory>().TradeDb.GetTradeOrderAsync(current.Order.Id, token);
        actual!.Status.Should().Be(TomasAI.IFM.Domain.Trade.Shared.TradeOrderStatus.Expired);
        actual.BoundExecutionAttemptId.Should().BeNull();
        var executionRows = await FinancialBoundaryTransactions().ExecuteOnceAsync(async (db, ct) =>
            await db.ScalarAsync("SELECT count(*) FROM trade.order_execution WHERE portfolioid=$1 AND fundid=$2 AND orderid=$3;",
                [current.Order.Id.PortfolioId, current.Order.Id.FundId, current.Order.Id.OrderId], ct), token);
        executionRows.Should().Be(0L);
        await VerifyOpeningReplayAsync(services, view, current, token);
        output.WriteLine($"Expired opening released without execution: {current.Id.Format()}; release ms={timer.Elapsed.TotalMilliseconds:F3}");
        return current;
    }

    async Task<StrategyExecutionHandoffDefinition> AssertOpeningAsync(IServiceProvider services, IntrinsicTimeStrategyWorkflowView view, CancellationToken token, bool publishQuotes = true)
    {
        var store = new StrategyExecutionHandoffReadStore(FinancialBoundaryTransactions());
        var timer = System.Diagnostics.Stopwatch.StartNew();
        StrategyExecutionHandoffDefinition? latest = null;
        var quoted = !publishQuotes;
        while (timer.Elapsed < TimeSpan.FromSeconds(40) && !token.IsCancellationRequested)
        {
            var rows = await store.ReadWorkflowAsync(view.WorkflowId.Value, token);
            latest = rows.SingleOrDefault();
            if (!quoted && latest?.Execution?.Status == TomasAI.IFM.Domain.Trade.Shared.OrderExecutionStatus.Submitted)
            {
                await PublishOpeningFixtureQuotesAsync(services, latest, token);
                quoted = true;
            }
            if (latest?.Phase == StrategyExecutionHandoffPhase.Opened) break;
            if (latest?.Phase is StrategyExecutionHandoffPhase.Rejected or StrategyExecutionHandoffPhase.Cancelled or StrategyExecutionHandoffPhase.Expired) break;
            await Task.Delay(100, token);
        }
        latest.Should().NotBeNull("the committed decision must create a durable opening handoff");
        if (latest!.Phase != StrategyExecutionHandoffPhase.Opened)
        {
            var diagnostic = await FinancialBoundaryTransactions().ExecuteOnceAsync(async (db, ct) =>
                await db.ScalarAsync("SELECT jsonb_build_object('intents',(SELECT count(*) FROM portfolio_financial.broker_accounting_intent WHERE portfolio_id=$1),'receipts',(SELECT count(*) FROM portfolio_financial.financial_operation_receipt WHERE portfolio_id=$1),'capacity',(SELECT count(*) FROM portfolio_financial.accepted_execution_capacity WHERE portfolio_id=$1))::text;", [latest.Order.Id.PortfolioId], ct), token);
            output.WriteLine($"Opening settlement diagnostic: workflow lineage={latest.Execution?.Order.StrategyWorkflowId}; {diagnostic}");
        }
        latest!.Phase.Should().Be(StrategyExecutionHandoffPhase.Opened, latest.ReconciliationReason + "; execution=" + latest.Execution?.Status + "; broker reason=" + latest.Execution?.RejectionDetail);
        latest.Execution!.CumulativeFilledQuantity.Should().Be(latest.Execution.OrderQuantity);
        latest.FundEvidence!.Status.Should().Be("Opened");
        latest.FinancialReceipt!.ProjectionFacts!.ExecutionCapacity!.Outcome.FilledUnits.Should().Be(latest.Execution.OrderQuantity);
        var legs = latest.Order.Components.SelectMany(component => component.Legs).ToDictionary(leg => leg.TradeLegId);
        var paid = latest.Execution.Fills.Sum(fill => decimal.Round(fill.Price * fill.SignedQuantity * legs[fill.TradeLegId].CashMultiplier, 2)
            + fill.Commission);
        var container = services.GetRequiredService<SimpleInjector.Container>();
        var account = await services.GetRequiredService<IContainerInstance>().Resolve<TomasAI.IFM.Application.TradeBroker.Contracts.ITradeBroker>()
            .GetAccountSnapshotAsync(token);
        account.CashBalance.Should().Be(container.GetInstance<TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine.EmulatorScenario>().StartingCash - paid);
        var scope = new FinancialReadScope { PortfolioId = latest.Order.Id.PortfolioId, FundId = latest.Order.Id.FundId,
            Access = new("OpeningQualification", ["LedgerRead"], [latest.Order.Id.PortfolioId]) };
        var balance = await services.GetRequiredService<TomasAI.IFM.Domain.Portfolio.Shared.Financial.IPortfolioFinancialApi>()
            .GetAccountBalancesAsync(scope, new(), token);
        balance.Success.Should().BeTrue(balance.ErrorMessage);
        balance.Value!.Value!.Accounts.Single(row => row.AccountId == 101).Balance.Should().Be(1000000000m - paid);
        var database = services.GetRequiredService<IDbContextFactory>().TradeDb;
        foreach (var component in latest.Order.Components)
        {
            var trade = await database.GetEstablishedTradeAsync(new(latest.Order.Id.PortfolioId, latest.Order.Id.FundId,
                latest.Order.Id.OrderId, component.ReservedTradeId), token);
            trade.Should().NotBeNull();
        }
        var positions = await FinancialBoundaryTransactions().ExecuteOnceAsync(async (db, ct) =>
            await db.ScalarAsync("SELECT count(*) FROM trade.strategy_position_current WHERE portfolioid=$1 AND fundid=$2 AND orderid=$3 AND isopen;",
                [latest.Order.Id.PortfolioId, latest.Order.Id.FundId, latest.Order.Id.OrderId], ct), token);
        positions.Should().Be(1L);
        await VerifyOpeningReplayAsync(services, view, latest, token);
        output.WriteLine($"Strategy opening: {view.EntityId.ItiSignalEntityId.TimePeriod}; workflow={view.WorkflowId}; order={latest.Order.Id.Format()}; execution={latest.Id.Format()}; phase={latest.Phase}; post-authorization ms={timer.Elapsed.TotalMilliseconds:F3}");
        return latest;
    }

    static async Task PublishOpeningFixtureQuotesAsync(IServiceProvider services, StrategyExecutionHandoffDefinition handoff, CancellationToken token, bool requireNewMatch = true)
    {
        var container = services.GetRequiredService<SimpleInjector.Container>();
        var context = (TomasAI.IFM.Domain.Trade.Order.Broker.Command.Actor.IBrokerOrderCommandContext)
            container.GetInstance<ICommandActorContext<TomasAI.IFM.Domain.Trade.Order.Broker.Command.Actor.BrokerOrderCommandActor>>();
        var broker = context.TradeBroker;
        broker.Environment.Should().Be(TomasAI.IFM.Application.TradeBroker.Contracts.BrokerEnvironment.Emulator);
        foreach (var component in handoff.Order.Components)
        {
            // Explicit, labelled synthetic market inputs reach the real emulator matcher.
            // No fill, execution-completion or position-success event is injected.
            var units = component.Legs.Max(leg => Math.Abs(leg.SignedQuantity));
            var prices = component.Legs.Select(_ => 100m).ToArray();
            var net = component.Legs.Select((leg, index) => prices[index] * leg.SignedQuantity / units).Sum();
            var difference = component.SignedNetDebitLimit!.Value - net;
            if (difference != 0)
            {
                var pivot = Array.FindIndex(component.Legs, leg => difference > 0 ? leg.SignedQuantity > 0 : leg.SignedQuantity < 0);
                if (pivot < 0) pivot = 0; // A single short futures leg has no positive leg.
                prices[pivot] += difference * units / component.Legs[pivot].SignedQuantity;
            }
            prices.Should().OnlyContain(price => price > 0);
            var now = context.Container.Resolve<TimeProvider>().GetUtcNow().UtcDateTime;
            var matched = 0;
            for (var index = 0; index < component.Legs.Length; index++)
                matched += await broker.PublishMarketQuoteAsync(new(component.Legs[index].ContractId, prices[index], prices[index],
                    units, units, now, broker.Generation, now.Ticks + index), token);
            // A manual close can already have filled against the still-fresh
            // opening quotes. Its caller independently waits for actual fills.
            if (requireNewMatch)
                matched.Should().BeGreaterThan(0, $"fixture quotes must match the isolated emulator order; now={now:O}; validUntil={handoff.Order.ValidUntilUtc:O}; units={units}; limit={component.SignedNetDebitLimit}; prices={string.Join(',', prices)}; contracts={string.Join(',', component.Legs.Select(leg => leg.ContractId))}");
        }
    }

    async Task VerifyOpeningReplayAsync(IServiceProvider services, IntrinsicTimeStrategyWorkflowView view, StrategyExecutionHandoffDefinition latest, CancellationToken token)
    {
        var container = services.GetRequiredService<SimpleInjector.Container>();
        var source = container.GetInstance<IEventSourceActorDbContext>();
        var repository = container.GetInstance<IEventSourceActorStateRepository<StrategyExecutionHandoffCommandState>>();
        var authorization = latest.Authorization;
        var committed = new TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events.WorkflowStrategyStateUpdatedEvent
        {
            Id = authorization.Id, CommandId = authorization.CommandId, EventId = authorization.EventId,
            EntityId = view.EntityId, State = view, WorkflowId = authorization.WorkflowId,
            WorkflowRevision = authorization.WorkflowRevision, UpdatedAtUtc = authorization.UpdatedAtUtc
        };
        var command = TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Execution.Model.StrategyOpeningIntentFactory
            .Create(committed, authorization.UpdatedAtUtc).Single(value => value.EntityId == latest.Id);
        var fullTimes = new List<double>(); var snapshotTimes = new List<double>();
        for (var iteration = 0; iteration < 5; iteration++)
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            var stream = await source.GetEventStreamIdAsync(command.StreamId, token);
            var replay = new StrategyExecutionHandoffCommandState { Id = command.Subject.ThreadId };
            await source.MapReduceActorEventStreamFromCreationAsync<StrategyExecutionHandoffCommandState>(stream, replay.ReplayEvents, token);
            fullTimes.Add(timer.Elapsed.TotalMilliseconds);
            timer.Restart();
            var snapshot = await repository.LoadStateAsync(command);
            snapshotTimes.Add(timer.Elapsed.TotalMilliseconds);
            FinancialCanonicalHash.Compute(snapshot.Definition).Should().Be(FinancialCanonicalHash.Compute(latest));
            FinancialCanonicalHash.Compute(replay.Definition).Should().Be(FinancialCanonicalHash.Compute(latest));
        }
        var actor = services.GetRequiredService<IActorService>();
        (await actor.RequestAsync<BeginStrategyExecutionHandoffCommand, TomasAI.IFM.Domain.Trade.Shared.OrderExecutionId>(command, token))
            .Success.Should().BeTrue();
        var count = await FinancialBoundaryTransactions().ExecuteOnceAsync(async (db, ct) =>
            await db.ScalarAsync("SELECT count(*) FROM trade.order_execution_fill WHERE executionattemptid=$1;", [latest.Id.ExecutionAttemptId], ct), token);
        count.Should().Be((long)(latest.Execution?.Fills.Length ?? 0));
        output.WriteLine($"Handoff cold reconstruction (stream resolution included): full replay ms=[{string.Join(',', fullTimes)}]; latest snapshot ms=[{string.Join(',', snapshotTimes)}]; preserved fill count={count}; duplicate fills added=0");
    }
}
