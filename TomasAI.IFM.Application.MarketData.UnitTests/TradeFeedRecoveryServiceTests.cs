using TomasAI.IFM.Application.MarketData.Recovery;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Recovery;
using Xunit;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class TradeFeedRecoveryServiceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Loading_during_worker_recovery_preserves_four_contracts_and_waits_for_success_or_caller_cancellation(bool cancel)
    {
        var state = new TomasAI.IFM.Application.MarketData.Contracts.MarketDataDatasetStatus("Options","GLBX.MDP3",Guid.NewGuid(),Guid.NewGuid(),123,false,false,"Recovering",null,Date);
        var store=new Store();var runtime=new Runtime();
        runtime.BeforeStart=token=>OptionWorkerReadiness.EnsureAsync(_=>Task.FromResult<TomasAI.IFM.Application.MarketData.Contracts.MarketDataDatasetStatus?>(Volatile.Read(ref state)),
            _=>throw new InvalidOperationException("Must join recovery without starting another worker."),token);
        using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var pending=new TradeFeedRecoveryService(store,runtime).LoadAsync(Trade.Format(),Date,true,deadline.Token);
        Assert.False(pending.IsCompleted);Assert.Empty(runtime.Active);
        Assert.Equal(4,Assert.Single(await store.ReadAsync(Date,default)).OptionContracts.Length);
        if(cancel){await deadline.CancelAsync();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>pending);Assert.Empty(runtime.Active);}
        else{Volatile.Write(ref state,state with{Lifecycle="Healthy",Running=true,Healthy=true});Assert.True(await pending);Assert.Equal(4,runtime.Active.Count);}
        Assert.Equal(4,Assert.Single(await store.ReadAsync(Date,default)).OptionContracts.Length);
    }
    static readonly DateOnly Date = new(2026,10,22);
    static readonly TradeEntityId Trade = new(101,701,1901,1501);
    [Fact]
    public async Task Enable_persists_contracts_and_restart_loads_trade_before_restoring_same_owner()
    {
        var store = new Store(); var runtime = new Runtime();
        Assert.True(await new TradeFeedRecoveryService(store,runtime).LoadAsync(Trade.Format(),Date,true,default));
        Assert.Equal(4, Assert.Single(await store.ReadAsync(Date,default)).OptionContracts.Length);
        Assert.Equal(4,runtime.Active.Count);
        runtime.Calls.Clear(); runtime.Active.Clear();
        Assert.True(await new TradeFeedRecoveryService(store,runtime).LoadAsync(Trade.Format(),Date,false,default));
        Assert.Equal("load",runtime.Calls[0]); Assert.Equal(4,runtime.Active.Count);
        Assert.All(runtime.Active, item => Assert.StartsWith(TradeFeedRecoveryService.Owner(Trade.Format(),Date)+"|",item));
    }
    [Fact]
    public async Task Disabled_and_different_value_dates_never_restore()
    {
        var store = new Store(); var runtime = new Runtime(); var service = new TradeFeedRecoveryService(store,runtime);
        Assert.False(await service.LoadAsync(Trade.Format(),Date,false,default)); Assert.Empty(runtime.Calls);
        await service.LoadAsync(Trade.Format(),Date,true,default);
        await service.DisableAsync(Trade.Format(),Date,default);
        Assert.Empty(await store.ReadAsync(Date,default)); Assert.Empty(runtime.Active);
        Assert.False(await service.LoadAsync(Trade.Format(),Date,false,default));
        Assert.False(await service.LoadAsync(Trade.Format(),Date.AddDays(-1),true,default));
    }
    [Fact]
    public async Task Failed_acquisition_preserves_intent_for_another_startup_but_failed_persistence_starts_nothing()
    {
        var store = new Store(); var runtime = new Runtime { FailStart = true }; var service = new TradeFeedRecoveryService(store,runtime);
        var failure = await Assert.ThrowsAsync<AggregateException>(()=> service.LoadAsync(Trade.Format(),Date,true,default));
        Assert.Equal(4, failure.InnerExceptions.Count);
        Assert.Single(await store.ReadAsync(Date,default));
        runtime.FailStart=false; Assert.True(await service.LoadAsync(Trade.Format(),Date,false,default)); Assert.Equal(4,runtime.Active.Count);
        var broken = new Store { FailSave=true }; runtime.Active.Clear();
        await Assert.ThrowsAsync<IOException>(()=>new TradeFeedRecoveryService(broken,runtime).LoadAsync(Trade.Format(),Date,true,default));
        Assert.Empty(runtime.Active);
    }
    [Fact]
    public async Task Explicit_off_deletes_intent_even_if_lease_release_fails()
    {
        var store=new Store();var runtime=new Runtime();var service=new TradeFeedRecoveryService(store,runtime);
        await service.LoadAsync(Trade.Format(),Date,true,default); runtime.FailStop=true;
        var failure = await Assert.ThrowsAsync<AggregateException>(()=>service.DisableAsync(Trade.Format(),Date,default));
        Assert.Equal(4, failure.InnerExceptions.Count);
        Assert.Empty(await store.ReadAsync(Date,default));
        Assert.False(await service.LoadAsync(Trade.Format(),Date,false,default));
    }
    [Fact]
    public async Task Closed_trade_removes_recovery_and_market_close_removes_session_rows()
    {
        var store=new Store();var runtime=new Runtime();var service=new TradeFeedRecoveryService(store,runtime);
        await service.LoadAsync(Trade.Format(),Date,true,default); runtime.Closed=true;
        Assert.False(await service.LoadAsync(Trade.Format(),Date,false,default)); Assert.Empty(runtime.Active); Assert.Empty(await store.ReadAsync(Date,default));
        runtime.Closed=false;await service.LoadAsync(Trade.Format(),Date,true,default);
        await service.CloseAsync(Date,default); Assert.Empty(await store.ReadAsync(Date,default)); Assert.Empty(runtime.Active);
    }
    sealed class Store : ITradeFeedRecoveryStore
    {
        readonly Dictionary<(string,DateOnly),TradeFeedRecoveryRegistration> rows=[];
        public bool FailSave;
        public Task<TradeFeedRecoveryRegistration[]> ReadAsync(DateOnly date,CancellationToken token)=>Task.FromResult(rows.Values.Where(row=>row.ValueDate==date).ToArray());
        public Task SaveAsync(TradeFeedRecoveryRegistration row,CancellationToken token) { if(FailSave)throw new IOException("database down"); rows[(row.TradeId,row.ValueDate)]=row;return Task.CompletedTask; }
        public Task RemoveAsync(string trade,DateOnly date,CancellationToken token){rows.Remove((trade,date));return Task.CompletedTask;}
        public Task CloseAsync(DateOnly date,CancellationToken token){foreach(var key in rows.Keys.Where(key=>key.Item2<=date).ToArray())rows.Remove(key);return Task.CompletedTask;}
    }
    sealed class Runtime : ITradeFeedRecoveryRuntime
    {
        public DateOnly? ActiveDate=>Date;
        public readonly List<string> Calls=[];
        public readonly HashSet<string> Active=[];
        public bool FailStart,FailStop,Closed;
        public Func<CancellationToken,Task>? BeforeStart;
        public Task<EstablishedTradeDefinition?> LoadAsync(TradeEntityId trade,CancellationToken token)
        {Calls.Add("load");return Task.FromResult<EstablishedTradeDefinition?>(new(){Id=trade,StrategyKind=TradeStrategyKind.IronCondor,Status=Closed?EstablishedTradeStatus.Closed:EstablishedTradeStatus.Open,Legs=Enumerable.Range(1,4).Select(i=>new TradeLegDefinition{TradeLegId=Guid.NewGuid(),ContractId="ES-option-"+i}).ToArray()});}
        public async Task StartAsync(string owner,string contract,DateOnly date,CancellationToken token){if(BeforeStart is not null)await BeforeStart(token);if(FailStart)throw new IOException("provider down"); Calls.Add("start");Active.Add(owner+"|"+contract);}
        public Task StopAsync(string owner,string contract,CancellationToken token){if(FailStop)throw new IOException("cleanup failed");Calls.Add("stop");Active.Remove(owner+"|"+contract);return Task.CompletedTask;}
    }
}
