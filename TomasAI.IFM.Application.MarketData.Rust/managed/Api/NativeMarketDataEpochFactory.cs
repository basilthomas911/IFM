using System.Diagnostics;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.MarketData.Rust.Delivery;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Framework.MarketData.DataBento.LastPrice;
using TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation.Contracts;
using TomasAI.IFM.Shared.MarketData.Contracts.LastPrice;
using TomasAI.IFM.Shared.MarketData.Contracts.Ticker;

namespace TomasAI.IFM.Application.MarketData.Rust.Api;

/// <summary>Native-only epoch factory. The infrastructure bootstrap closes over
/// reviewed configuration and application publishers/reference policy and must
/// return the sealed native resource owner. Create performs no I/O. Actors and
/// startup workflows continue to use IMarketDataApi, not this infrastructure seam.</summary>
public sealed class NativeMarketDataEpochFactory : IRustMarketDataEpochFactory
{
    readonly Func<DateOnly,TimeSpan,CancellationToken,Task<NativeEpochResources>> bootstrap;
    readonly object registry=new();
    long latest;
    NativeMarketDataEpoch? current;
    /// <summary>The last successfully started native epoch. An epoch being created,
    /// stopped or superseded cannot supply Host services with stale resources.</summary>
    public NativeMarketDataEpoch Current {get{lock(registry)return current is{IsAvailable:true} value?value:throw new MarketDataApiNotRunningException();}}
    /// <summary>Observes the active native epoch without manufacturing a session
    /// during cold reference work or after shutdown.</summary>
    public bool TryGetCurrent(out NativeMarketDataEpoch? epoch)
    {lock(registry){epoch=current is{IsAvailable:true}?current:null;return epoch is not null;}}
    /// <inheritdoc/>
    public TimeProvider Clock {get;}
    /// <summary>Requires the same authoritative business clock used by bootstrap.
    /// No current-provider factory or process launcher can be returned here.</summary>
    public NativeMarketDataEpochFactory(TimeProvider clock,Func<DateOnly,TimeSpan,CancellationToken,Task<NativeEpochResources>> bootstrap)
    {Clock=clock??throw new ArgumentNullException(nameof(clock));this.bootstrap=bootstrap??throw new ArgumentNullException(nameof(bootstrap));}
    /// <inheritdoc/>
    public IRustMarketDataEpoch Create(DateOnly valueDate)
    {
        lock(registry){var candidate=checked(++latest);return new NativeMarketDataEpoch(valueDate,Clock,bootstrap,epoch=>{
            lock(registry){if(candidate!=latest)throw new OperationCanceledException("A newer native epoch superseded this bootstrap.");current=epoch;}
        });}
    }
}

/// <summary>Application epoch interface over a fully owned native resource set.
/// Logical option scope identities remain distinct from physical futures source
/// identities. Native readiness is reported separately from acknowledged event
/// publication; it does not claim that a trade actor or UI has processed an event.</summary>
public sealed class NativeMarketDataEpoch : IRustMarketDataEpoch
{
    readonly Func<DateOnly,TimeSpan,CancellationToken,Task<NativeEpochResources>> bootstrap;
    readonly TimeProvider clock;
    readonly Action<NativeMarketDataEpoch> publish;
    readonly object lifecycle=new(),routeGate=new();
    readonly CancellationTokenSource stopping=new();
    readonly SemaphoreSlim resets=new(1);
    readonly Dictionary<string,Route> routes=new(StringComparer.Ordinal);
    sealed class Route(DateTimeOffset activated){internal readonly HashSet<TickerStreamOwner> Owners=[];internal readonly DateTimeOffset Activated=activated;}
    NativeEpochResources? resources;
    Task? starting,disposal;
    internal NativeMarketDataEpoch(DateOnly valueDate,TimeProvider clock,Func<DateOnly,TimeSpan,CancellationToken,Task<NativeEpochResources>> bootstrap,Action<NativeMarketDataEpoch> publish)
    {this.publish=publish;if(valueDate==default)throw new ArgumentOutOfRangeException(nameof(valueDate));ValueDate=valueDate;this.clock=clock;this.bootstrap=bootstrap;}
    internal bool IsAvailable=>!stopping.IsCancellationRequested&&Volatile.Read(ref resources)is not null;
    internal NativeEpochResources Resources=>Active;
    /// <summary>Logical option owner identity, independent of physical reconnects.</summary>
    public Guid OptionGeneration=>Active.OptionGeneration;
    /// <summary>True while replacement has not restored all desired option scopes.</summary>
    public bool IsOptionOwnerReplacementPending=>Active.OptionScopes.IsReplacing;
    /// <inheritdoc/>
    public DateOnly ValueDate {get;}
    NativeEpochResources Active
    {
        get{if(stopping.IsCancellationRequested||Volatile.Read(ref resources)is not{} owned)throw new MarketDataApiNotRunningException();return owned;}
    }
    /// <inheritdoc/>
    public NativeMarketDataRuntime Runtime=>Active.Runtime;
    /// <summary>Restarts owned option scopes under the caller's remaining episode
    /// budget. It preserves leases while native physical generations fence old data.</summary>
    public async Task ResetOptionsAsync(TimeSpan remainingBudget,CancellationToken cancellationToken=default)
    {
        if(remainingBudget<TimeSpan.Zero)throw new ArgumentOutOfRangeException(nameof(remainingBudget));
        if(remainingBudget>TimeSpan.FromMinutes(2))remainingBudget=TimeSpan.FromMinutes(2);
        using var bounded=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,stopping.Token);
        bounded.CancelAfter(remainingBudget);var owned=Active;
        await owned.OptionScopes.ResetAsync(remainingBudget,bounded.Token).ConfigureAwait(false);
        await owned.QualifyOptionRecoveryAsync(bounded.Token).ConfigureAwait(false);
    }
    /// <summary>Restores a terminated managed receive/delivery owner under the
    /// existing recovery budget, joining all predecessor callbacks first.</summary>
    public Task<bool> EnsureDeliveryOperationalAsync(TimeSpan remainingBudget,CancellationToken token=default)
        =>Active.EnsureDeliveryOperationalAsync(remainingBudget,token);
    /// <summary>Reads a qualified additional underlying owned by options only.</summary>
    public Task<decimal?> ReadAdditionalUnderlyingPriceAsync(string contract,TimeSpan budget,CancellationToken token)
        =>Active.ReadAdditionalUnderlyingPriceAsync(contract,budget,token);
    /// <summary>Replaces only the complete native option owner, proving managed
    /// callbacks joined before native cleanup and preserving futures ownership.</summary>
    public async Task<IReadOnlyList<WorkerOptionChainRequest>> ReplaceOptionOwnerAsync(
        Func<System.Collections.Immutable.ImmutableArray<WorkerOptionDefinition>,Guid,CancellationToken,Task<System.Collections.Immutable.ImmutableArray<WorkerOptionDefinition>>> requalify,
        TimeSpan remainingBudget,CancellationToken cancellationToken=default)
    {
        if(remainingBudget<TimeSpan.Zero)throw new ArgumentOutOfRangeException(nameof(remainingBudget));
        if(remainingBudget>TimeSpan.FromMinutes(2))remainingBudget=TimeSpan.FromMinutes(2);
        var start=Stopwatch.GetTimestamp();
        TimeSpan Remaining()=>TimeSpan.FromTicks(Math.Max(0,remainingBudget.Ticks-Stopwatch.GetElapsedTime(start).Ticks));
        using var bounded=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,stopping.Token);bounded.CancelAfter(remainingBudget);
        var owned=Active;IReadOnlyList<WorkerOptionChainRequest>? restored=null;
        await owned.ReplaceOptionOwnerAsync(async()=>restored=await owned.OptionScopes.ReplaceOwnerAsync(requalify,Remaining(),bounded.Token).ConfigureAwait(false),bounded.Token).ConfigureAwait(false);
        await owned.QualifyOptionRecoveryAsync(bounded.Token).ConfigureAwait(false);
        return restored!;
    }
    /// <summary>Deadline escalation: revoke option reads and signal only their
    /// native owner without waiting on a busy managed gate or allocating a new
    /// recovery budget. The caller must not label this as cleanup confirmation.</summary>
    public void SignalOptionOwnerStop()
    {var owned=Active;owned.OptionScopes.SuspendMonitoring();owned.Runtime.SignalOptionOwnerStop(owned.Runtime.ReadOptionOwnerGeneration());}
    /// <summary>Repairs exact monitored contracts while preserving other scopes
    /// and all futures lanes. Existing callbacks must join before qualification.</summary>
    public async Task<string[]> ResetOptionContractsAsync(IReadOnlyCollection<string> contracts,TimeSpan remainingBudget,CancellationToken cancellationToken=default)
    {
        if(remainingBudget<TimeSpan.Zero)throw new ArgumentOutOfRangeException(nameof(remainingBudget));
        if(remainingBudget>TimeSpan.FromMinutes(2))remainingBudget=TimeSpan.FromMinutes(2);
        using var bounded=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,stopping.Token);bounded.CancelAfter(remainingBudget);
        var owned=Active;var repaired=await owned.OptionScopes.ResetContractsAsync(contracts,remainingBudget,bounded.Token).ConfigureAwait(false);
        await owned.QualifyOptionRecoveryAsync(bounded.Token).ConfigureAwait(false);return repaired;
    }
    /// <summary>Qualifies exact underlying definitions for a committed option
    /// manifest without changing primary futures ownership.</summary>
    public Task PrepareOptionUnderlyingsAsync(IReadOnlyList<TomasAI.IFM.Application.MarketData.Databento.Workers.DatasetSubscriptionContract> contracts,TimeSpan remainingBudget,CancellationToken token=default)
        =>Active.PrepareOptionUnderlyingsAsync(contracts,remainingBudget,token);
    /// <summary>Copied scope states for Host health; no provider call or acquisition.</summary>
    public IReadOnlyDictionary<string,LiveLaneStatus> GetOptionScopeStates()
    {var owned=Active;return owned.OptionScopes.ActiveScopes.ToDictionary(x=>x,x=>owned.Runtime.ReadOptionScopeStatus(x),StringComparer.Ordinal);}
    /// <summary>Copies actual scope ownership, including underlying dependencies.
    /// A suspended/replacing owner cannot masquerade as an empty idle workload.</summary>
    public NativeOptionWorkloadEvidence CaptureOptionWorkload()=>Active.OptionScopes.CaptureWorkloadEvidence();
    /// <summary>Current owned monitoring contract identities, without acquisition.</summary>
    public IReadOnlyCollection<string> MonitoredOptionContracts=>Active.OptionScopes.MonitoredQuotes.Select(x=>x.Prepared.Context.Contract.ContractId).ToArray();
    /// <inheritdoc/>
    public IDatabentoMarketDataCatalog Catalog=>Active.Catalog;
    /// <inheritdoc/>
    public IDatabentoLastPriceReaderProvider LastPrices=>Active.LastPrices;
    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();lock(lifecycle)
        {ObjectDisposedException.ThrowIf(disposal is not null,this);return starting??=StartCoreAsync(cancellationToken);}
    }
    async Task StartCoreAsync(CancellationToken token)
    {
        var started=Stopwatch.GetTimestamp();using var bounded=CancellationTokenSource.CreateLinkedTokenSource(token,stopping.Token);bounded.CancelAfter(TimeSpan.FromMinutes(2));
        NativeEpochResources? owned=null;
        try
        {
            owned=await bootstrap(ValueDate,TimeSpan.FromMinutes(2),bounded.Token).ConfigureAwait(false);
            if(owned.Runtime.LiveValueDate!=ValueDate)throw new InvalidOperationException("Native bootstrap returned another API ValueDate.");
            bounded.Token.ThrowIfCancellationRequested();Volatile.Write(ref resources,owned);publish(this);
        }
        catch(Exception original)
        {
            Volatile.Write(ref resources,null);
            if(owned is not null)
                try{await owned.StopAndDisposeAsync(TimeSpan.FromTicks(Math.Max(0,TimeSpan.FromMinutes(2).Ticks-Stopwatch.GetElapsedTime(started).Ticks))).ConfigureAwait(false);}
                catch(Exception cleanup){throw new AggregateException("Native epoch startup and cleanup failed.",original,cleanup);}
            throw;
        }
    }
    /// <inheritdoc/>
    public Task StopAsync()=>StopAndDisposeAsync(TimeSpan.FromMinutes(2)).AsTask();
    /// <summary>Cancels admission before waiting for bootstrap/owned cleanup.
    /// All waits consume one original budget; repeated stops share its result.</summary>
    public ValueTask StopAndDisposeAsync(TimeSpan remainingBudget)
    {
        if(remainingBudget<TimeSpan.Zero)throw new ArgumentOutOfRangeException(nameof(remainingBudget));
        if(remainingBudget>TimeSpan.FromMinutes(2))remainingBudget=TimeSpan.FromMinutes(2);
        lock(lifecycle)return new(disposal??=StopCoreAsync(remainingBudget));
    }
    async Task StopCoreAsync(TimeSpan total)
    {
        var started=Stopwatch.GetTimestamp();TimeSpan Remaining()=>TimeSpan.FromTicks(Math.Max(0,total.Ticks-Stopwatch.GetElapsedTime(started).Ticks));
        var failures=new List<Exception>();
        var signal=stopping.CancelAsync();
        try{await signal.WaitAsync(Remaining()).ConfigureAwait(false);}catch(Exception error){failures.Add(error);}
        if(starting is{} pending)
            try{await pending.WaitAsync(Remaining()).ConfigureAwait(false);}catch(Exception error){if(pending.IsCompleted)_=pending.Exception;else failures.Add(error);}
        var owned=Volatile.Read(ref resources);
        if(owned is null){if(failures.Count!=0)throw new AggregateException("Native epoch bootstrap/cancellation did not join.",failures);return;}
        owned.LastPrices.Dispose();
        lock(routeGate){foreach(var id in routes.Keys)try{owned.LiveRoutes.Deactivate(id);}catch(Exception e){failures.Add(e);}routes.Clear();}
        try{await owned.StopAndDisposeAsync(Remaining()).ConfigureAwait(false);}catch(Exception e){failures.Add(e);}
        if(failures.Count!=0)throw new AggregateException("Native epoch stop failed.",failures);
    }
    /// <inheritdoc/>
    public ValueTask DisposeAsync()=>StopAndDisposeAsync(TimeSpan.FromMinutes(2));
    /// <inheritdoc/>
    public bool TryGetMarketInstrumentId(string contractId,out uint marketInstrumentId)
    {
        var owned=Active;if(owned.Catalog.TryGetMarketInstrumentId(contractId,out marketInstrumentId))return true;
        if(owned.OptionScopes.FindMonitoredQuote(contractId)is{} option&&option.IsCurrent()){marketInstrumentId=option.Prepared.Context.Contract.InstrumentId;return true;}
        marketInstrumentId=0;return false;
    }
    /// <inheritdoc/>
    public bool TryGetLastTickPrice(string contractId,out FuturesMarketPriceSnapshot snapshot)
    {
        var owned=Active;snapshot=default;
        return owned.Futures.TryGetValue(contractId,out var binding)&&owned.Runtime.ReadLiveState(binding.Lane,binding.Mapping.InstrumentId,binding.Mapping.PublisherId)
            .TryGetMarketPrice(contractId,AssetTypeId.Futures,out snapshot);
    }
    /// <inheritdoc/>
    public bool TryGetLastOptionTickPrice(string contractId,out OptionTickerPriceSnapshot snapshot)
    {
        var owned=Active;snapshot=default;var binding=owned.OptionScopes.FindMonitoredQuote(contractId);if(binding is null||!binding.IsCurrent())return false;
        var c=binding.Prepared.Context.Contract;var state=owned.Runtime.ReadOptionScopeState(binding.ScopeId,c.InstrumentId,c.PublisherId);
        if(!state.TryGetMarketPrice(contractId,AssetTypeId.FuturesOption,out var price)||!binding.IsCurrent())return false;
        OptionGreeksSnapshot? greeks=null;
        if(binding.Reader.TryGetLastQuoteWithGreeks(out var quote)&&price.Quote?.SourceSequence==quote.Tick.SourceSequence)greeks=quote.Greeks;
        else if(binding.Reader.TryGetLastTradeWithGreeks(out var trade)&&price.Trade?.SourceSequence==trade.Tick.SourceSequence)greeks=trade.Greeks;
        snapshot=new(new(price.ContractId,price.InstrumentId,price.PublisherId,price.AssetTypeId,price.ValueDate,
            price.Quote is{} q?new TickerQuoteSnapshot(q.BidPrice,q.BidSize,q.AskPrice,q.AskSize,q.BidCount,q.AskCount,q.SourceSequence,q.EventTimestamp,q.ReceiveTimestamp):null,
            price.Trade is{} t?new TickerTradeSnapshot(t.LastPrice,t.LastSize,t.SourceSequence,t.EventTimestamp,t.ReceiveTimestamp):null),greeks);return true;
    }
    /// <inheritdoc/>
    public bool TryGetFuturesSessionStatistics(string contractId,out FuturesSessionStatisticsSnapshot snapshot)
    {
        var owned=Active;snapshot=default;
        return owned.Futures.TryGetValue(contractId,out var binding)&&owned.Runtime.ReadLiveState(binding.Lane,binding.Mapping.InstrumentId,binding.Mapping.PublisherId).TryGetSessionStatistics(contractId,out snapshot);
    }
    /// <inheritdoc/>
    public bool IsTickDataStreamActive(string contractId){var owned=Active;lock(routeGate)return routes.TryGetValue(contractId,out var route)&&route.Owners.Count!=0
        &&(owned.Futures.ContainsKey(contractId)||owned.OptionScopes.IsMonitoredContract(contractId));}
    bool ChangeRoute(TickerStreamOwner owner,string id,bool option,bool acquire)
    {
        owner.Validate();ArgumentException.ThrowIfNullOrWhiteSpace(id);var owned=Active;
        if(option?!owned.OptionScopes.IsMonitoredContract(id):!owned.Futures.ContainsKey(id))
        {if(!acquire)return false;throw new KeyNotFoundException("No qualified native contract owns this route.");}
        lock(routeGate)
        {
            if(stopping.IsCancellationRequested)throw new MarketDataApiNotRunningException();
            if(!routes.TryGetValue(id,out var route)){if(!acquire)return false;if(routes.Count>=4096)throw new InvalidOperationException("Native route capacity reached.");routes.Add(id,route=new(clock.GetUtcNow()));}
            if(acquire)
            {
                if(route.Owners.Contains(owner))return false;if(route.Owners.Count>=128)throw new InvalidOperationException("Native route owner capacity reached.");
                route.Owners.Add(owner);try{if(route.Owners.Count==1)owned.LiveRoutes.Activate(id);}catch{route.Owners.Remove(owner);if(route.Owners.Count==0)routes.Remove(id);throw;}return true;
            }
            if(!route.Owners.Remove(owner))return false;
            try{if(route.Owners.Count==0)owned.LiveRoutes.Deactivate(id);}catch{route.Owners.Add(owner);throw;}
            if(route.Owners.Count==0)routes.Remove(id);return true;
        }
    }
    /// <inheritdoc/>
    public bool StartFuturesRoute(TickerStreamOwner owner,string id)=>ChangeRoute(owner,id,false,true);
    /// <inheritdoc/>
    public bool StopFuturesRoute(TickerStreamOwner owner,string id)=>ChangeRoute(owner,id,false,false);
    /// <inheritdoc/>
    public bool StartIndividualOptionRoute(TickerStreamOwner owner,string id)=>ChangeRoute(owner,id,true,true);
    /// <inheritdoc/>
    public bool StopIndividualOptionRoute(TickerStreamOwner owner,string id)=>ChangeRoute(owner,id,true,false);
    /// <inheritdoc/>
    public Task<bool> StartOptionChainAsync(string futuresContractId,DateOnly maturityDate,string[] optionContractIds)
    {_=Active;throw new MarketDataPricingInputUnavailableException("Treasury curve session rate");}
    /// <inheritdoc/>
    public Task<bool> StopOptionChainAsync(string futuresContractId,DateOnly maturityDate)
    {_=Active;return Task.FromResult(false);} // Unqualified legacy starts never acquire a lease, matching current policy.
    /// <inheritdoc/>
    public Task<WorkerOptionChainResult> AcquireOptionChainAsync(WorkerOptionChainRequest request,CancellationToken token)=>Active.OptionScopes.AcquireAsync(request,TimeSpan.FromMinutes(2),token);
    /// <inheritdoc/>
    public Task<WorkerOptionChainResult> ReleaseOptionChainAsync(WorkerOptionChainRelease request,CancellationToken token)=>Active.OptionScopes.ReleaseAsync(request,TimeSpan.FromMinutes(2),token);
    /// <inheritdoc/>
    public async Task<CompositionSnapshotResult> CaptureCompositionSnapshotAsync(CompositionSnapshotRequest request,CancellationToken token)
    {
        var owned=Active;
        ICompositionMarketSource source=request.IncludeOptions?owned.OptionScopes:new WorkerFuturesCompositionSource(owned.LastPrices,ValueDate,request.GenerationId,
            id=>owned.Futures.TryGetValue(id,out var future)&&request.GenerationId==owned.DatasetGeneration(future.Lane));
        if(request.EvaluatedAtUtc==default)
        {
            try{var page=await source.ReadAsync(request,null,token).ConfigureAwait(false);source=new FrozenSource(page);request=request with{EvaluatedAtUtc=clock.GetUtcNow()};}
            catch(CompositionMarketSourceException error){return new(null,new(error.Code,"Snapshot","","Native snapshot source is unavailable."));}
        }
        return await new NativeMarketCompositionSnapshotProvider(source,owned.Runtime,clock).CaptureAsync(request,token).ConfigureAwait(false);
    }
    sealed class FrozenSource(CompositionMarketPage page):ICompositionMarketSource
    {public Task<CompositionMarketPage> ReadAsync(CompositionSnapshotRequest request,string? continuation,CancellationToken token){token.ThrowIfCancellationRequested();if(continuation is not null)throw new CompositionMarketSourceException("IncompleteChain");return Task.FromResult(page);}}
    /// <inheritdoc/>
    public async Task<DatabentoDatasetResetResult> ResetDatasetAsync(DatabentoDatasetResetRequest request,CancellationToken token)
    {
        var owned=Active;if(request.ValueDate!=ValueDate)throw new InvalidOperationException("Reset ValueDate differs from its native epoch.");
        var members=owned.Futures.Values.Where(x=>x.Mapping.Dataset==request.Dataset).ToArray();if(members.Length==0)throw new KeyNotFoundException("Dataset is not configured in the native futures epoch.");
        if(request.TeardownTimeout<=TimeSpan.Zero||request.QualificationTimeout<=TimeSpan.Zero)throw new ArgumentOutOfRangeException(nameof(request));
        var lane=members[0].Lane;var started=Stopwatch.GetTimestamp();using var deadline=CancellationTokenSource.CreateLinkedTokenSource(token,stopping.Token);deadline.CancelAfter(TimeSpan.FromMinutes(2));
        try
        {
            await resets.WaitAsync(deadline.Token).ConfigureAwait(false);
            try
            {
                var current=owned.DatasetGeneration(lane);
                if(current!=request.ExpectedGenerationId)return new(request.Dataset,request.ExpectedGenerationId,current,true,"A newer logical dataset already owns the route; stale reset was ignored.");
                var remaining=TimeSpan.FromMinutes(2)-Stopwatch.GetElapsedTime(started);if(remaining<=TimeSpan.Zero)throw new TimeoutException("Original native epoch reset deadline expired.");
                var status=await owned.FuturesDelivery.ResetAsync(lane,owned.Runtime.ReadLiveStatus(lane).Generation,remaining,deadline.Token,()=>owned.ReplaceDatasetGeneration(lane)).ConfigureAwait(false);
                return new(request.Dataset,current,owned.DatasetGeneration(lane),status.Phase==3,status.Error??"Native dataset replacement qualified under the original deadline.");
            }
            finally{resets.Release();}
        }
        catch(Exception error)when(error is not OperationCanceledException||!token.IsCancellationRequested)
        {return new(request.Dataset,request.ExpectedGenerationId,owned.DatasetGeneration(lane),false,error.Message);}
    }
    /// <inheritdoc/>
    public bool IsFeedUp(TimeSpan timeout)=>timeout>TimeSpan.Zero&&GetHealth()is{Running:true,AggregationRunning:true,LastPriceStoreActive:true};
    /// <inheritdoc/>
    public TickAggregationContractStatus GetAggregationStatus(string contractId)
    {
        var owned=Active;bool active;DateTimeOffset? activated;lock(routeGate){active=routes.TryGetValue(contractId,out var route);activated=active?route!.Activated:null;}
        if(owned.Futures.TryGetValue(contractId,out var future))return owned.FuturesPublication.GetStatus(contractId,owned.Runtime.ReadLiveStatus(future.Lane).Phase==3,active,activated);
        var option=owned.OptionScopes.FindMonitoredQuote(contractId);var configured=option?.IsCurrent()==true;
        var running=configured&&owned.Runtime.ReadOptionScopeStatus(option!.ScopeId).Phase==3;
        return new(contractId,AssetTypeId.FuturesOption,!owned.Completion.IsCompleted,configured,running,active&&configured,StreamActivatedAtUtc:activated);
    }
    /// <inheritdoc/>
    public DatabentoMarketDataEpochHealth GetHealth()
    {
        var owned=Volatile.Read(ref resources);if(owned is null||stopping.IsCancellationRequested)return new(ValueDate,false,false,0,0,false,0,0,0);
        var running=owned.IsDeliveryOperational;var statuses=owned.Futures.Keys.Select(GetAggregationStatus).Concat(owned.OptionScopes.MonitoredQuotes.Select(x=>GetAggregationStatus(x.Prepared.Context.Contract.ContractId))).ToArray();
        var metrics=owned.FuturesPublication.GetMetrics();
        var datasets=owned.Futures.Values.GroupBy(x=>x.Mapping.Dataset,StringComparer.Ordinal).Select(group=>
        {
            var status=owned.Runtime.ReadLiveStatus(group.First().Lane);var up=status.Phase==3;
            var health=new FeedHealthSnapshot(up?FeedState.Running:status.Phase==5?FeedState.Faulted:FeedState.Starting,
                status.Phase==5?DatabentoFeedStatus.DatabentoError:DatabentoFeedStatus.Ok,0,0,0,0,0,0,0,0,0,
                status.Error??"Tokio backend: legacy ring/thread counters are not applicable."){TransportReady=up,TradingReady=up&&group.All(x=>owned.FuturesPublication.GetStatus(x.Mapping.ContractId,true,false,null).LastDurableTickPublishedAtUtc is not null),InstrumentCount=group.Count(),BaselineReadyInstrumentCount=up?group.Count():0};
            return new DatabentoDatasetFeedHealth(group.Key,owned.DatasetGeneration(group.First().Lane),health,owned.FuturesPublication.GetMetrics(group.First().Lane));
        }).ToArray();
        return new(ValueDate,running,running&&statuses.All(x=>x.ContractRunning),statuses.Length,owned.LastPrices.Count,owned.LastPrices.IsActive,
            metrics.SourceQuoteRecords,metrics.SourceTradeRecords,metrics.PublicationFailures,metrics.ProcessingFailures,statuses,datasets);
    }
}
