using System.Text.Json;
using TomasAI.IFM.Framework.TradeBroker.Contracts;
using TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine;
using Xunit;
namespace TomasAI.IFM.Application.TradeBroker.UnitTests;

public sealed class EmulatorCheckpointRecoveryTests
{
    sealed class Clock:IEmulatorClock {public DateTime UtcNow{get;set;}=new(2026,11,20,15,0,0,DateTimeKind.Utc);}
    sealed class Store:IEmulatorLedgerStore,IDisposable
    {
        public readonly FileEmulatorLedgerStore Inner;
        public bool Before,After,ReadFails;public int Attempts;
        public Store(string path)=>Inner=new(path);
        public EmulatorLedgerCheckpoint? Load()=>ReadFails?throw new IOException("Injected read failure."):Inner.Load();
        public void Save(EmulatorLedgerCheckpoint checkpoint)
        {Attempts++;if(Before)throw new IOException("Injected before-save failure.");Inner.Save(checkpoint);if(After)throw new IOException("Injected after-save failure.");}
        public void Dispose()=>Inner.Dispose();
    }
    static string PathForTest()=>Path.Combine(Path.GetTempPath(),"ifm-emulator-commit",Guid.NewGuid().ToString("N"),"ledger.json");
    static EmulatorScenario Scenario(int maximum=int.MaxValue)=>new("EMU","USD",1_000_000m,.65m,TimeSpan.FromMinutes(1),maximum);
    static FrameworkOrderRequest Order(Clock clock,int legs,int units=1,string id="order",decimal limit=20m)=>new("EMU",id,Guid.NewGuid(),Guid.NewGuid(),legs switch{1=>FrameworkOrderShape.FuturesOutright,2=>FrameworkOrderShape.VerticalSpread,_=>FrameworkOrderShape.IronCondor},false,
        Enumerable.Range(0,legs).Select(i=>new FrameworkOrderLeg(Guid.NewGuid(),"C"+i,(i%2==0?1:-1)*units,legs==1?null:100m+i,null,null,50m)).ToArray(),
        limit,limit,limit,.25m,clock.UtcNow.AddMinutes(5),"approved","ref",1000m,1000m);
    static EmulatorQuote[] Quotes(Clock clock,int legs,long offset=0)=>Enumerable.Range(0,legs).Select(i=>new EmulatorQuote("C"+i,9m,10m,10,10,clock.UtcNow,1,offset+i+1)).ToArray();
    static FrameworkBrokerObservation[] Executions(EmulatorLedger ledger,string id="order")=>ledger.Reconcile(id).Where(f=>f.Kind==FrameworkObservationKind.Execution).ToArray();
    [Fact]
    public async Task Account_wakeup_reconstructs_latest_state_without_blocking_or_consuming_money_facts()
    {
        var clock=new Clock();var store=new InMemoryEmulatorLedgerStore();
        var facts=Enumerable.Range(0,5000).Select(i=>new FrameworkBrokerObservation{Kind=FrameworkObservationKind.AccountSnapshot,
            ObservationId=new Guid(i+1,0,0,new byte[8]),AccountAlias="EMU",BrokerOrderId="account",OccurredAtUtc=clock.UtcNow,SourceSequence=i+1}).ToArray();
        store.Save(new(12345m,0,1,[],[],[],[],facts){CommitRevision=5001});
        var ledger=await Task.Run(()=>new EmulatorLedger(Scenario(),clock,store)).WaitAsync(TimeSpan.FromSeconds(5));
        using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using(var reader=ledger.ObserveAccount(stop.Token).GetAsyncEnumerator())
        {
            Assert.True(await reader.MoveNextAsync());Assert.Equal(facts[^1].ObservationId,reader.Current.ObservationId);
        }
        Assert.Equal(5000,store.Load()!.Observations.Length);Assert.Equal(5001,ledger.Snapshot().SourceSequence);Assert.Equal(12345m,ledger.Snapshot().CashBalance);
        var restart=new EmulatorLedger(Scenario(),clock,store);
        await using var replay=restart.ObserveAccount(stop.Token).GetAsyncEnumerator();
        Assert.True(await replay.MoveNextAsync());Assert.Equal(facts[^1].ObservationId,replay.Current.ObservationId);
        Assert.Equal(12345m,restart.Snapshot().CashBalance);
    }
    [Fact]
    public async Task Publication_cursor_rejects_gaps_and_duplicates_do_not_write_again()
    {
        using var store=new Store(PathForTest());var clock=new Clock();var scenario=Scenario();var ledger=new EmulatorLedger(scenario,clock,store);
        ledger.Place(Order(clock,1));ledger.PublishQuote(Quotes(clock,1)[0]);var facts=store.Load()!.Observations;
        Assert.Throws<InvalidOperationException>(()=>ledger.AcknowledgeOrderObservation(facts[1].ObservationId));
        Assert.Throws<InvalidOperationException>(()=>ledger.AcknowledgeOrderObservation(Guid.NewGuid()));
        var cash=ledger.Snapshot().CashBalance;
        foreach(var fact in facts.Where(f=>f.Kind!=FrameworkObservationKind.AccountSnapshot))ledger.AcknowledgeOrderObservation(fact.ObservationId);
        var writes=store.Attempts;ledger.AcknowledgeOrderObservation(facts[0].ObservationId);Assert.Equal(writes,store.Attempts);
        Assert.Throws<InvalidOperationException>(()=>ledger.AcknowledgeOrderObservation(facts.Single(f=>f.Kind==FrameworkObservationKind.AccountSnapshot).ObservationId));
        var restart=new EmulatorLedger(scenario,clock,store);Assert.Equal(cash,restart.Snapshot().CashBalance);
        using var stop=new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        await using var reader=restart.ObserveOrders(stop.Token).GetAsyncEnumerator();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async()=>await reader.MoveNextAsync());
    }
    [Fact]
    public async Task Restored_pending_journal_exceeds_former_queue_capacity_without_blocking_or_loss()
    {
        var facts=Enumerable.Range(0,5000).Select(i=>new FrameworkBrokerObservation{Kind=FrameworkObservationKind.Acknowledged,
            ObservationId=new Guid(i+1,0,0,new byte[8]),AccountAlias="EMU",BrokerOrderId="order",OccurredAtUtc=new Clock().UtcNow}).ToArray();
        var store=new InMemoryEmulatorLedgerStore();store.Save(new(1_000_000m,0,1,[],[],[],[],facts));
        var ledger=new EmulatorLedger(Scenario(),new Clock(),store);
        using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using(var reader=ledger.ObserveOrders(stop.Token).GetAsyncEnumerator())
            foreach(var fact in facts){Assert.True(await reader.MoveNextAsync());Assert.Equal(fact,reader.Current);}
        // Reading alone never acknowledges durable work; reconstruction resumes at the original fact.
        var restart=new EmulatorLedger(Scenario(),new Clock(),store);
        await using var replay=restart.ObserveOrders(stop.Token).GetAsyncEnumerator();Assert.True(await replay.MoveNextAsync());Assert.Equal(facts[0],replay.Current);
    }
    [Theory][InlineData(1,false)][InlineData(2,false)][InlineData(4,false)][InlineData(1,true)][InlineData(2,true)][InlineData(4,true)]
    public void Failed_save_hides_staged_finances_and_retry_keeps_exact_facts_across_restart(int legs,bool afterWrite)
    {
        using var store=new Store(PathForTest());var clock=new Clock();var scenario=Scenario();var ledger=new EmulatorLedger(scenario,clock,store);
        ledger.Place(Order(clock,legs));var quotes=Quotes(clock,legs);foreach(var q in quotes[..^1])ledger.PublishQuote(q);
        store.Before=!afterWrite;store.After=afterWrite;
        Assert.Throws<IOException>(()=>ledger.PublishQuote(quotes[^1]));
        var incomplete=ledger.Snapshot();Assert.False(incomplete.Complete);Assert.False(incomplete.NewRiskAllowed);Assert.Equal(scenario.StartingCash,incomplete.CashBalance);Assert.Empty(incomplete.Positions);
        Assert.Throws<IOException>(()=>ledger.Reconcile("order"));Assert.Throws<IOException>(()=>ledger.RemainingStrategyUnits("order"));
        var durableAfterFailure=store.Inner.Load()!;
        Assert.Equal(afterWrite?legs:0,durableAfterFailure.Observations.Count(f=>f.Kind==FrameworkObservationKind.Execution));
        var originalTime=clock.UtcNow;clock.UtcNow=clock.UtcNow.AddDays(1);
        store.Before=store.After=false;var attempts=store.Attempts;
        Assert.Equal(0,ledger.PublishQuote(quotes[^1]));Assert.Equal(attempts+1,store.Attempts);Assert.True(ledger.Snapshot().Complete);
        var fills=Executions(ledger);Assert.Equal(legs,fills.Length);Assert.All(fills,f=>Assert.Equal(originalTime,f.OccurredAtUtc));
        Assert.All(fills,f=>Assert.Equal(1,Math.Abs(f.SignedQuantity)));
        var cash=ledger.Snapshot().CashBalance;var ids=fills.Select(f=>f.ObservationId).ToArray();
        var restarted=new EmulatorLedger(scenario,clock,store);Assert.Equal(0,restarted.PublishQuote(quotes[^1]));
        Assert.Equal(cash,restarted.Snapshot().CashBalance);Assert.Equal(ids,Executions(restarted).Select(f=>f.ObservationId));
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void Unreadable_outcome_blocks_new_mutation_until_the_same_candidate_is_confirmed(bool afterWrite)
    {
        using var store=new Store(PathForTest());var clock=new Clock();var ledger=new EmulatorLedger(Scenario(),clock,store);ledger.Place(Order(clock,1));var quote=Quotes(clock,1)[0];
        store.Before=!afterWrite;store.After=afterWrite;Assert.Throws<IOException>(()=>ledger.PublishQuote(quote));
        store.Before=store.After=false;store.ReadFails=true;var attempts=store.Attempts;
        Assert.Throws<IOException>(()=>ledger.PublishQuote(quote with{SourceSequence=50}));Assert.Equal(attempts,store.Attempts);Assert.False(ledger.Snapshot().Complete);
        store.ReadFails=false;Assert.Equal(0,ledger.PublishQuote(quote));Assert.Single(Executions(ledger));Assert.True(ledger.Snapshot().Complete);
    }
    [Fact]
    public void Foreign_checkpoint_is_not_overwritten_by_pending_recovery()
    {
        using var store=new Store(PathForTest());var clock=new Clock();var ledger=new EmulatorLedger(Scenario(),clock,store);ledger.Place(Order(clock,1));var quote=Quotes(clock,1)[0];
        store.Before=true;Assert.Throws<IOException>(()=>ledger.PublishQuote(quote));store.Before=false;
        var foreign=store.Inner.Load()! with{Cash=12345m,CommitRevision=99};store.Inner.Save(foreign);
        Assert.Throws<InvalidDataException>(()=>ledger.PublishQuote(quote));Assert.False(ledger.Snapshot().Complete);Assert.Equal(12345m,store.Inner.Load()!.Cash);
    }
    [Theory][InlineData(1)][InlineData(2)][InlineData(4)]
    public void Partial_fill_receipts_prevent_duplicate_liquidity_after_lost_reply_and_restart(int legs)
    {
        using var store=new Store(PathForTest());var clock=new Clock();var scenario=Scenario(1);var ledger=new EmulatorLedger(scenario,clock,store);ledger.Place(Order(clock,legs,2));
        var first=Quotes(clock,legs);foreach(var q in first[..^1])ledger.PublishQuote(q);
        store.After=true;Assert.Throws<IOException>(()=>ledger.PublishQuote(first[^1]));store.After=false;
        var restarted=new EmulatorLedger(scenario,clock,store);Assert.Equal(0,restarted.PublishQuote(first[^1]));Assert.Equal(1,restarted.RemainingStrategyUnits("order"));
        var firstFills=Executions(restarted);Assert.Equal(legs,firstFills.Length);
        Assert.False(restarted.TryMatch("order",first));Assert.Equal(legs,Executions(restarted).Length);
        foreach(var q in Quotes(clock,legs,100))restarted.PublishQuote(q);
        // Each new input is an eligible market observation, but total approved size remains two units.
        Assert.Equal(0,restarted.RemainingStrategyUnits("order"));Assert.Equal(legs*2,Executions(restarted).Length);
        Assert.Single(restarted.Reconcile("order"),f=>f.Kind==FrameworkObservationKind.OrderCompleted);
    }
    [Fact]
    public void Multi_order_fanout_is_one_financial_save_and_rolls_back_together()
    {
        using var store=new Store(PathForTest());var clock=new Clock();var ledger=new EmulatorLedger(Scenario(),clock,store);
        ledger.Place(Order(clock,1,id:"A"));ledger.Place(Order(clock,1,id:"B"));var quote=Quotes(clock,1)[0];var attempts=store.Attempts;
        store.Before=true;Assert.Throws<IOException>(()=>ledger.PublishQuote(quote));Assert.Equal(attempts+1,store.Attempts);Assert.Equal(1_000_000m,ledger.Snapshot().CashBalance);
        store.Before=false;Assert.Equal(0,ledger.PublishQuote(quote));Assert.Single(Executions(ledger,"A"));Assert.Single(Executions(ledger,"B"));Assert.Equal(attempts+2,store.Attempts);
    }
    [Fact]
    public void Explicit_offline_match_retry_confirms_pending_commit_without_filling_again()
    {
        using var store=new Store(PathForTest());var clock=new Clock();var ledger=new EmulatorLedger(Scenario(1),clock,store);
        var quote=Quotes(clock,1)[0];ledger.PublishQuote(quote);ledger.Place(Order(clock,1,2));
        store.Before=true;Assert.Throws<IOException>(()=>ledger.SimulateOfflineFill("order",1));Assert.False(ledger.Snapshot().Complete);
        store.Before=false;Assert.False(ledger.SimulateOfflineFill("order",1));
        Assert.Single(Executions(ledger));Assert.Equal(1,ledger.RemainingStrategyUnits("order"));Assert.True(ledger.Snapshot().Complete);
    }
    [Fact]
    public void Offline_size_cap_does_not_reexecute_or_conflict_with_an_already_matched_native_book()
    {
        using var store=new Store(PathForTest());var clock=new Clock();var ledger=new EmulatorLedger(Scenario(1),clock,store);
        ledger.Place(Order(clock,1,2));var quote=Quotes(clock,1)[0];Assert.Equal(1,ledger.PublishQuote(quote));
        Assert.False(ledger.SimulateOfflineFill("order",1));Assert.Single(Executions(ledger));Assert.Equal(1,ledger.RemainingStrategyUnits("order"));
    }
    [Fact]
    public void Zero_match_book_is_restored_without_replaying_it_into_a_later_order()
    {
        using var store=new Store(PathForTest());var clock=new Clock();var ledger=new EmulatorLedger(Scenario(),clock,store);var quote=Quotes(clock,1)[0];
        Assert.Equal(0,ledger.PublishQuote(quote));var restarted=new EmulatorLedger(Scenario(),clock,store);restarted.Place(Order(clock,1));
        Assert.Equal(0,restarted.PublishQuote(quote));Assert.Empty(Executions(restarted));Assert.True(restarted.HasFreshMarketQuotes("order"));
        Assert.Equal(1,restarted.PublishQuote(quote with{SourceSequence=2}));Assert.Single(Executions(restarted));
    }
    [Fact]
    public void Legacy_checkpoint_upgrades_without_changing_cash_and_corruption_or_second_writer_blocks()
    {
        var path=PathForTest();Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var legacy=new EmulatorLedgerCheckpoint(123456m,0,1,[],[],[],[],[]);File.WriteAllBytes(path,JsonSerializer.SerializeToUtf8Bytes(legacy));
        using(var store=new FileEmulatorLedgerStore(path))
        {
            Assert.Throws<IOException>(()=>new FileEmulatorLedgerStore(path));var clock=new Clock();var ledger=new EmulatorLedger(Scenario(),clock,store);
            Assert.Equal(123456m,ledger.Snapshot().CashBalance);ledger.Place(Order(clock,1));
            Assert.True(store.Load()!.CommitRevision>0);Assert.Equal(123456m,store.Load()!.Cash);
        }
        File.WriteAllText(path,"{truncated");using var bad=new FileEmulatorLedgerStore(path);Assert.Throws<JsonException>(()=>bad.Load());
    }
    [Theory][InlineData("Place")][InlineData("Modify")][InlineData("Cancel")]
    public void Non_quote_mutations_use_the_same_failed_save_boundary(string kind)
    {
        using var store=new Store(PathForTest());var clock=new Clock();var ledger=new EmulatorLedger(Scenario(),clock,store);var order=Order(clock,1) with{MaximumLimit=22m};
        if(kind!="Place")ledger.Place(order);
        store.Before=true;
        if(kind=="Place")Assert.Throws<IOException>(()=>ledger.Place(order));
        else if(kind=="Modify")Assert.Throws<IOException>(()=>ledger.Modify(new("EMU","order",Guid.NewGuid(),21m,1)));
        else Assert.Throws<IOException>(()=>ledger.Cancel(new("EMU","order",Guid.NewGuid(),1)));
        Assert.False(ledger.Snapshot().Complete);store.Before=false;
        // A new rejected operation first confirms the exact pending mutation; no financial state is skipped.
        _=ledger.Place(order);Assert.True(ledger.Snapshot().Complete);Assert.NotNull(store.Load());
        if(kind=="Modify")Assert.Equal(2,store.Load()!.Orders.Single().Revision);
        if(kind=="Cancel")Assert.True(store.Load()!.Orders.Single().Cancelled);
    }
}
