using TomasAI.IFM.Framework.TradeBroker.Contracts;
using TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine;
using Xunit;
namespace TomasAI.IFM.LivePipeline.IntegrationTests;
public sealed class OfflineFillSimulationTests
{
    static FrameworkOrderRequest Request(int units=1) => new("STRICT",Guid.NewGuid().ToString("N"),Guid.NewGuid(),Guid.NewGuid(),FrameworkOrderShape.IronCondor,true,
        Enumerable.Range(0,4).Select(i=>new FrameworkOrderLeg(Guid.NewGuid(),"LEG"+i,i%2==0?units:-units,100+i,null,1,50)).ToArray(),
        0,-100,100,.05m,DateTime.UtcNow.AddMinutes(5),"approval","reference",1000,1000,FrameworkOrderType.Market);
    sealed class Clock:IEmulatorClock {public DateTime UtcNow{get;set;}=DateTime.UtcNow;}
    static EmulatorQuote[] Quotes(FrameworkOrderRequest r,DateTime at)=>r.Legs.Select((l,i)=>new EmulatorQuote(l.ContractId,2+i,2.25m+i,10,10,at,1,i+1)).ToArray();
    [Theory]
    [InlineData("missing")][InlineData("stale")][InlineData("future")][InlineData("crossed")][InlineData("zero")][InlineData("depth")][InlineData("epoch")][InlineData("sequence")]
    public void Every_leg_requires_original_fresh_executable_quotes(string invalid)
    {
        var clock=new Clock();var ledger=new EmulatorLedger(EmulatorScenario.Development("STRICT"),clock);var r=Request();var quotes=Quotes(r,clock.UtcNow);
        quotes[3]=invalid switch {"stale"=>quotes[3] with {MarketTimeUtc=clock.UtcNow.AddSeconds(-3)},"future"=>quotes[3] with {MarketTimeUtc=clock.UtcNow.AddSeconds(1)},
            "crossed"=>quotes[3] with {Bid=10,Ask=1},"zero"=>quotes[3] with {Bid=0},"depth"=>quotes[3] with {BidSize=0},"epoch"=>quotes[3] with {SourceEpoch=2},"sequence"=>quotes[3] with {SourceSequence=0},_=>quotes[3]};
        foreach(var q in quotes.Take(invalid=="missing"?3:4))ledger.PublishQuote(q);ledger.Place(r);var cash=ledger.Snapshot().CashBalance;
        Assert.False(ledger.SimulateOfflineFill(r.BrokerOrderId,1));Assert.DoesNotContain(ledger.Reconcile(r.BrokerOrderId),f=>f.Kind==FrameworkObservationKind.Execution);
        Assert.Equal(cash,ledger.Snapshot().CashBalance);Assert.Equal(1,ledger.RemainingStrategyUnits(r.BrokerOrderId));
    }
    [Fact]
    public void Market_close_uses_bid_ask_and_keeps_source_identity()
    {
        var clock=new Clock();var ledger=new EmulatorLedger(EmulatorScenario.Development("STRICT"),clock);var r=Request();var quotes=Quotes(r,clock.UtcNow);
        foreach(var q in quotes)ledger.PublishQuote(q);ledger.Place(r);Assert.True(ledger.SimulateOfflineFill(r.BrokerOrderId,1));
        var fills=ledger.Reconcile(r.BrokerOrderId).Where(f=>f.Kind==FrameworkObservationKind.Execution).ToArray();Assert.Equal(4,fills.Length);
        foreach(var fill in fills){var q=quotes.Single(q=>q.ContractId==fill.ContractId);Assert.Equal(fill.SignedQuantity>0?q.Ask:q.Bid,fill.Price);Assert.Equal(q.SourceEpoch,fill.SourceEpoch);Assert.Equal(q.SourceSequence,fill.SourceSequence);}
    }
    [Fact]
    public void Limit_price_is_not_forced_by_adjusting_one_leg()
    {
        var clock=new Clock();var ledger=new EmulatorLedger(EmulatorScenario.Development("STRICT"),clock);var r=Request() with {OrderType=FrameworkOrderType.Limit,SignedNetDebitLimit=-10};
        foreach(var q in Quotes(r,clock.UtcNow))ledger.PublishQuote(q);ledger.Place(r);Assert.False(ledger.SimulateOfflineFill(r.BrokerOrderId,1));
    }
    [Theory]
    [InlineData(true)][InlineData(false)]
    public async Task Timer_does_not_fill_missing_quotes_even_during_market_hours(bool closed)
    {
        var clock=new Clock();var ledger=new EmulatorLedger(EmulatorScenario.Development("STRICT"),clock);var r=Request();ledger.Place(r);
        using var simulation=new OfflineFillSimulation(ledger,new(){CompletionTime=TimeSpan.FromMilliseconds(50)},()=>closed);simulation.Start(r.BrokerOrderId);await simulation.CompletionAsync(r.BrokerOrderId);
        Assert.Equal(1,ledger.RemainingStrategyUnits(r.BrokerOrderId));Assert.DoesNotContain(ledger.Reconcile(r.BrokerOrderId),f=>f.Kind==FrameworkObservationKind.Execution);
    }
    [Fact]
    public void Fourth_quote_respects_partial_depth_and_stale_remaining_book()
    {
        var clock=new Clock();var ledger=new EmulatorLedger(EmulatorScenario.Development("STRICT"),clock);var r=Request(3);var quotes=Quotes(r,clock.UtcNow);
        ledger.Place(r);foreach(var q in quotes.Take(3))ledger.PublishQuote(q);Assert.Equal(3,ledger.RemainingStrategyUnits(r.BrokerOrderId));
        ledger.PublishQuote(quotes[3] with {BidSize=1,AskSize=1});Assert.Equal(2,ledger.RemainingStrategyUnits(r.BrokerOrderId));clock.UtcNow=clock.UtcNow.AddSeconds(3);
        Assert.False(ledger.SimulateOfflineFill(r.BrokerOrderId,2));Assert.Equal(4,ledger.Reconcile(r.BrokerOrderId).Count(f=>f.Kind==FrameworkObservationKind.Execution));
    }
}
