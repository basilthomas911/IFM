using TomasAI.IFM.Application.Api.Server.Core.Trading.Emulation;
using NSubstitute;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.TradeBroker.Contracts;
using TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine;
using TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.OrderExecution;
using Xunit;
namespace TomasAI.IFM.LivePipeline.IntegrationTests;
public sealed class OfflineFillSimulationTests
{
    static FrameworkOrderRequest Request(int count = 4, int units = 3) => new("OFFLINE", Guid.NewGuid().ToString("N"),
        Guid.NewGuid(), Guid.NewGuid(), count == 4 ? FrameworkOrderShape.IronCondor : count == 2 ? FrameworkOrderShape.VerticalSpread : FrameworkOrderShape.FuturesOutright,
        false, Enumerable.Range(0,count).Select(i => new FrameworkOrderLeg(Guid.NewGuid(), $"TEST-{i}", i % 2 == 0 ? units : -units,
            count == 1 ? null : 100m+i, null, 1, 1m)).ToArray(), count == 1 ? 10m : -2m,
        -100m, 100m, 0.05m, DateTime.UtcNow.AddMinutes(5), "approval", "reference", 1000m, 1000m);
    static EmulatorLedger Ledger() => new(EmulatorScenario.Development("OFFLINE"), new SystemEmulatorClock());
    static OfflineFillSimulation Simulation(EmulatorLedger ledger, bool closed = true, double seconds = 0.5) =>
        new(ledger, new() { CompletionTime = TimeSpan.FromSeconds(seconds), MaximumUnitsPerFill = 1, RandomSeed = 12 }, () => closed);
    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(4)]
    public async Task Offline_order_emits_balanced_partial_executions_commissions_and_completion(int legs)
    {
        var ledger = Ledger(); var request = Request(legs);
        using var simulation = Simulation(ledger);
        var sessions = Substitute.For<IFuturesMarketSessionAuthority>();
        sessions.Current.Returns(new MarketSessionReadModel { State = FuturesMarketState.Closed });
        var broker = new FrozenEmulatorOrderExecutionBroker(new EmulatedOrderExecutionBroker(ledger), sessions, new SystemEmulatorClock(), simulation);
        Assert.Equal(FrameworkDispatchOutcome.AcceptedForDispatch, (await broker.PlaceAsync(request)).Outcome);
        await broker.PlaceAsync(request); // duplicate acceptance must not schedule twice
        await simulation.CompletionAsync(request.BrokerOrderId);
        var facts = await broker.ReconcileAsync(request.BrokerOrderId);
        var fills = facts.Where(x => x.Kind == FrameworkObservationKind.Execution).ToArray();
        Assert.Equal(legs * 3, fills.Length);
        Assert.All(fills, x => Assert.Equal(1, Math.Abs(x.SignedQuantity)));
        Assert.Equal(legs * 3, facts.Count(x => x.Kind == FrameworkObservationKind.Commission));
        Assert.Single(facts,x => x.Kind == FrameworkObservationKind.OrderCompleted);
        Assert.Equal(0, ledger.RemainingStrategyUnits(request.BrokerOrderId));
    }
    [Fact]
    public async Task Update_after_partial_fill_applies_new_price_and_revision_without_restarting_deadline()
    {
        var ledger = Ledger(); var request = Request(1); ledger.Place(request);
        using var simulation = Simulation(ledger,seconds:1);
        simulation.Start(request.BrokerOrderId);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(4));
        while (ledger.RemainingStrategyUnits(request.BrokerOrderId) == 3) await Task.Delay(5,deadline.Token);
        var operation = Guid.NewGuid();
        Assert.Equal(FrameworkDispatchOutcome.AcceptedForDispatch,
            ledger.Modify(new(request.AccountAlias,request.BrokerOrderId,operation,9m,1)).Outcome);
        await simulation.CompletionAsync(request.BrokerOrderId);
        var fills = ledger.Reconcile(request.BrokerOrderId).Where(x => x.Kind == FrameworkObservationKind.Execution && x.OrderRevision == 2).ToArray();
        Assert.NotEmpty(fills);
        Assert.All(fills,x=>Assert.Equal(operation,x.OperationId));
        foreach(var group in fills.Chunk(request.Legs.Length))
            Assert.Equal(9m,group.Sum(x=>x.Price*x.SignedQuantity));
    }
    [Fact]
    public async Task Cancellation_preserves_partial_fills_and_stops_remaining_fills()
    {
        var ledger = Ledger(); var request = Request(1); ledger.Place(request);
        using var simulation = Simulation(ledger,seconds:1); simulation.Start(request.BrokerOrderId);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(4));
        while (ledger.RemainingStrategyUnits(request.BrokerOrderId) == 3) await Task.Delay(5,deadline.Token);
        ledger.Cancel(new(request.AccountAlias,request.BrokerOrderId,Guid.NewGuid(),1));
        var before = ledger.Reconcile(request.BrokerOrderId).Count(x=>x.Kind==FrameworkObservationKind.Execution);
        await simulation.CompletionAsync(request.BrokerOrderId);
        var facts=ledger.Reconcile(request.BrokerOrderId);
        Assert.Equal(before,facts.Count(x=>x.Kind==FrameworkObservationKind.Execution));
        Assert.DoesNotContain(facts,x=>x.Kind==FrameworkObservationKind.OrderCompleted);
        Assert.Contains(facts,x=>x.Kind==FrameworkObservationKind.Cancelled);
    }
    [Fact]
    public async Task Fresh_live_quotes_prevent_synthetic_fills_but_missing_market_data_activates_them()
    {
        var ledger=Ledger(); var request=Request(); ledger.Place(request);
        foreach(var leg in request.Legs) ledger.PublishQuote(new(leg.ContractId,1m,2m,10,10,DateTime.UtcNow,1,1));
        using var live=Simulation(ledger,closed:false); live.Start(request.BrokerOrderId); await live.CompletionAsync(request.BrokerOrderId);
        Assert.DoesNotContain(ledger.Reconcile(request.BrokerOrderId),x=>x.Kind==FrameworkObservationKind.Execution);
        var empty=Ledger(); var offline=Request(); empty.Place(offline);
        using var missing=Simulation(empty,closed:false); missing.Start(offline.BrokerOrderId); await missing.CompletionAsync(offline.BrokerOrderId);
        Assert.Contains(empty.Reconcile(offline.BrokerOrderId),x=>x.Kind==FrameworkObservationKind.OrderCompleted);
    }
    [Fact]
    public async Task Market_order_with_zero_limit_still_fills_and_shutdown_stops_pending_simulation()
    {
        var ledger=Ledger(); var request=Request(1,1) with { OrderType=FrameworkOrderType.Market, SignedNetDebitLimit=0m };
        ledger.Place(request); using var simulation=Simulation(ledger);simulation.Start(request.BrokerOrderId);
        await simulation.CompletionAsync(request.BrokerOrderId);
        Assert.Contains(ledger.Reconcile(request.BrokerOrderId),x=>x.Kind==FrameworkObservationKind.OrderCompleted);
        var pending=Request();ledger.Place(pending);var stopped=Simulation(ledger);stopped.Start(pending.BrokerOrderId);stopped.Dispose();
        await stopped.CompletionAsync(pending.BrokerOrderId);
        Assert.DoesNotContain(ledger.Reconcile(pending.BrokerOrderId),x=>x.Kind==FrameworkObservationKind.Execution);
    }
    [Theory]
    [InlineData(2)] [InlineData(4)]
    public async Task Ten_strategy_units_fill_in_random_balanced_batches_without_partial_legs(int legs)
    {
        var ledger=Ledger();var request=Request(legs,10);ledger.Place(request);
        using var simulation=new OfflineFillSimulation(ledger,new() { CompletionTime=TimeSpan.FromSeconds(0.5),MaximumUnitsPerFill=3,RandomSeed=42 },()=>true);
        simulation.Start(request.BrokerOrderId);await simulation.CompletionAsync(request.BrokerOrderId);
        var fills=ledger.Reconcile(request.BrokerOrderId).Where(x=>x.Kind==FrameworkObservationKind.Execution).ToArray();
        var batches=fills.Chunk(legs).ToArray();
        Assert.InRange(batches.Length,4,10);
        foreach(var batch in batches) {
            Assert.Equal(legs,batch.Length);
            Assert.Equal(legs,batch.Select(x=>x.ContractId).Distinct().Count());
            Assert.Single(batch.Select(x=>Math.Abs(x.SignedQuantity)).Distinct());
            Assert.InRange(Math.Abs(batch[0].SignedQuantity),1,3);
        }
        foreach(var leg in request.Legs) Assert.Equal(leg.SignedQuantity,fills.Where(x=>x.ContractId==leg.ContractId).Sum(x=>x.SignedQuantity));
        Assert.Single(ledger.Reconcile(request.BrokerOrderId),x=>x.Kind==FrameworkObservationKind.OrderCompleted);
    }
    [Fact]
    public async Task Default_simulation_completes_at_thirty_seconds()
    {
        var ledger=Ledger();var request=Request(1);ledger.Place(request);
        using var simulation=new OfflineFillSimulation(ledger,new(),()=>true);
        var elapsed=System.Diagnostics.Stopwatch.StartNew();simulation.Start(request.BrokerOrderId);
        await simulation.CompletionAsync(request.BrokerOrderId);
        Assert.InRange(elapsed.Elapsed.TotalSeconds,29.5,32);
        Assert.Contains(ledger.Reconcile(request.BrokerOrderId),x=>x.Kind==FrameworkObservationKind.OrderCompleted);
    }
}
