using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Diagnostics;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.MarketData.Rust.Delivery;
using TomasAI.IFM.Application.MarketData.Rust.OptionChains;
using TomasAI.IFM.Application.MarketData.Rust.Publication;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation;
using TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation.Contracts;
using TomasAI.IFM.Framework.MarketData.TickAggregation;
using TomasAI.IFM.Shared.MarketData.Contracts.TickAggregation;
using TomasAI.IFM.Shared.MarketData.Contracts.Pricing;
using TomasAI.IFM.Shared.MarketData.Contracts.Ticker;

namespace TomasAI.IFM.Application.MarketData.Rust.Api;

/// <summary>Already-qualified endpoint for a futures dataset. Secrets are supplied
/// separately and are never included in a generated configuration ToString.</summary>
public sealed record NativeDatasetEndpoint(MarketDataLane Lane,string Host,ushort Port);

/// <summary>Required, nonblocking Host observers. No logging or health observer
/// has a silent default; the Host must route Fault into its recovery supervisor.</summary>
public sealed record NativeEpochObservers(Action<LiveDiagnostic> Diagnostic,Action<HistoricalWarning> Warning,
    Action<string,LiveLaneStatus> ScopeStatus,Action<string?,Exception> Fault);

/// <summary>Owns the complete prepared native runtime data plane for one API
/// ValueDate. Bootstrap performs native catalog I/O, prepares options ownership,
/// starts the sole event receiver and then starts futures lanes. No worker process
/// or current-provider driver is constructed. Host policy owns reference
/// qualification and publication/persistence dependencies passed here.</summary>
public sealed class NativeEpochResources : IAsyncDisposable
{
    NativeMarketDataDelivery receiver;
    readonly Func<NativeMarketDataDelivery> createReceiver;
    readonly SemaphoreSlim deliveryReplacement=new(1);
    readonly NativeFuturesPublication futures;
    readonly TickQuoteBufferPool pool;
    readonly NativeOptionTradeSourceReader trades;
    readonly NativeEpochObservers observers;
    readonly ConcurrentDictionary<string,NativeRegistration> optionUnderlyings;
    readonly TimeProvider clock;
    internal sealed class SourceIdentity(Guid initial)
    {
        sealed record Identity(Guid Value);
        Identity current=new(initial);
        internal Guid Current=>Volatile.Read(ref current).Value;
        internal void Replace()=>Volatile.Write(ref current,new(Guid.NewGuid()));
    }
    readonly FrozenDictionary<MarketDataLane,SourceIdentity> identities;
    internal Guid DatasetGeneration(MarketDataLane lane)=>identities[lane].Current;
    internal void ReplaceDatasetGeneration(MarketDataLane lane)=>identities[lane].Replace();
    internal ITickLiveRouter LiveRoutes {get;}
    internal NativeFuturesDelivery FuturesDelivery {get;}
    internal NativeFuturesPublication FuturesPublication=>futures;
    readonly object gate=new();
    Task? disposal;
    /// <summary>Native provider, numerical and aggregate owner.</summary>
    public NativeMarketDataRuntime Runtime {get;}
    /// <summary>Resolved immutable startup catalog, with native query operations.</summary>
    public NativeContractCatalog Catalog {get;}
    /// <summary>Stable bounded, date-scoped last-price handles.</summary>
    public NativeLastPriceDirectory LastPrices {get;}
    /// <summary>Application-qualified lease and reference authority.</summary>
    public NativeOptionChainRuntime OptionScopes {get;}
    /// <summary>Logical generation used by qualified option requests.</summary>
    public Guid OptionGeneration=>OptionScopes.Generation;
    /// <summary>Logical generation of this futures publication epoch.</summary>
    public Guid FuturesGeneration {get;}
    /// <summary>Acquires an exact option-only futures quote dependency. Native
    /// definitions qualify the canonical ID; primary futures remain unchanged.</summary>
    public async Task<decimal?> ReadAdditionalUnderlyingPriceAsync(string contract,TimeSpan budget,CancellationToken token)
    {
        if(!contract.StartsWith("ES",StringComparison.Ordinal))throw new ArgumentException("Only qualified ES option underlyings are supported.",nameof(contract));
        ArgumentOutOfRangeException.ThrowIfLessThan(budget,TimeSpan.Zero);if(budget>TimeSpan.FromMinutes(2))budget=TimeSpan.FromMinutes(2);
        var started=Stopwatch.GetTimestamp();TimeSpan Remaining()=>TimeSpan.FromTicks(Math.Max(0,budget.Ticks-Stopwatch.GetElapsedTime(started).Ticks));
        if(!optionUnderlyings.TryGetValue(contract,out var registration))
        {
            using var source=await Runtime.AcquireDefinitionSnapshotAsync("GLBX.MDP3",["ES.FUT"],Remaining(),token).ConfigureAwait(false);
            var matches=Enumerable.Range(0,source.Count).Select(source.ReadDefinition).Where(x=>x.CanonicalId==contract&&x.Kind==1&&!x.Deleted&&x.Asset=="ES"&&x.Publisher>0&&x.Instrument>0).ToArray();
            if(matches.Length!=1)throw new InvalidDataException("Exact option-underlying definition is missing or ambiguous: "+contract);
            var value=matches[0];registration=new(value.Instrument,contract,value.Instrument.ToString(System.Globalization.CultureInfo.InvariantCulture),false,value.Tick,false,true,value.Publisher);
            lock(gate){if(optionUnderlyings.Count>=64&&!optionUnderlyings.ContainsKey(contract))throw new InvalidOperationException("Option underlying definition capacity exceeded.");optionUnderlyings.TryAdd(contract,registration);}
        }
        return await OptionScopes.ReadUnderlyingPriceAsync(registration,Remaining(),token).ConfigureAwait(false);
    }
    /// <summary>Exact qualified startup futures mappings.</summary>
    public FrozenDictionary<string,(MarketDataLane Lane,TickContractMapping Mapping)> Futures {get;}
    /// <summary>Qualifies additional option-only underlying mappings from the
    /// provider's exact definition query. Primary futures subscriptions remain
    /// unchanged. The caller is application manifest authority, not a tick reader.</summary>
    public async Task PrepareOptionUnderlyingsAsync(IReadOnlyList<TomasAI.IFM.Application.MarketData.Databento.Workers.DatasetSubscriptionContract> contracts,TimeSpan budget,CancellationToken token)
    {
        if(contracts.Count is <1 or >16||contracts.Any(x=>x.Dataset!="GLBX.MDP3"))throw new ArgumentException("A bounded GLBX option-underlying manifest is required.",nameof(contracts));
        using var snapshot=await Runtime.AcquireExactDefinitionSnapshotAsync("GLBX.MDP3",contracts.Select(x=>x.ProviderContractName).ToArray(),budget,token).ConfigureAwait(false);
        var rows=Enumerable.Range(0,snapshot.Count).Select(snapshot.ReadDefinition).ToArray();
        var prepared=new List<NativeRegistration>(contracts.Count);
        foreach(var contract in contracts)
        {
            var matches=rows.Where(x=>x.Kind==1&&!x.Deleted&&x.Symbol==contract.ProviderContractName&&x.Asset==contract.RootSymbol).ToArray();
            if(matches.Length!=1||matches[0] is not{Instrument:>0,Publisher:>0} d)throw new InvalidDataException("Native option-underlying definition is missing or ambiguous.");
            prepared.Add(new(d.Instrument,contract.DomainContractId,d.Instrument.ToString(System.Globalization.CultureInfo.InvariantCulture),false,d.Tick,false,true,d.Publisher));
        }
        // One small cold control lock makes capacity/conflict rejection atomic;
        // hot acquisition reads the concurrent directory without this lock.
        lock(gate)
        {
            if(optionUnderlyings.Count+prepared.Count(x=>!optionUnderlyings.ContainsKey(x.Contract))>64)throw new InvalidOperationException("Native option-underlying capacity exceeded.");
            foreach(var item in prepared)if(optionUnderlyings.TryGetValue(item.Contract,out var previous)&&previous!=item)throw new InvalidOperationException("Qualified option underlying changed within its logical epoch.");
            foreach(var item in prepared)optionUnderlyings.TryAdd(item.Contract,item);
        }
    }
    internal Task ReplaceOptionOwnerAsync(Func<Task> replace,CancellationToken token)=>receiver.ReplaceOptionOwnerAsync(replace,token);
    internal Task QualifyOptionRecoveryAsync(CancellationToken token)=>receiver.QualifyOptionRecoveryAsync(token);
    /// <summary>Receiver completion exposes an unexpected terminal observer error.</summary>
    public Task Completion=>Volatile.Read(ref receiver).Completion;
    /// <summary>All managed delivery loops, not just native transport, remain active.</summary>
    public bool IsDeliveryOperational=>Volatile.Read(ref receiver).IsOperational;
    /// <summary>Replaces a terminated signal/delivery owner only after actual
    /// old callbacks/loops join. Native futures ownership is left intact and
    /// option reads stay suspended until explicit option-owner restoration.</summary>
    public async Task<bool> EnsureDeliveryOperationalAsync(TimeSpan budget,CancellationToken token)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(budget,TimeSpan.Zero);
        if(budget>TimeSpan.FromMinutes(2))budget=TimeSpan.FromMinutes(2);
        var started=Stopwatch.GetTimestamp();
        TimeSpan Remaining()=>TimeSpan.FromTicks(Math.Max(0,budget.Ticks-Stopwatch.GetElapsedTime(started).Ticks));
        using var bounded=CancellationTokenSource.CreateLinkedTokenSource(token);bounded.CancelAfter(budget);
        await deliveryReplacement.WaitAsync(bounded.Token).ConfigureAwait(false);
        try
        {
            lock(gate)ObjectDisposedException.ThrowIf(disposal is not null,this);
            var old=Volatile.Read(ref receiver);if(!old.NeedsReplacement)return false;
            OptionScopes.SuspendMonitoring();
            try{await old.StopAndDisposeAsync(Remaining()).ConfigureAwait(false);}
            catch when(old.IsJoined){_=old.Completion.Exception;}
            bounded.Token.ThrowIfCancellationRequested();
            lock(gate)
            {
                ObjectDisposedException.ThrowIf(disposal is not null,this);
                Volatile.Write(ref receiver,createReceiver());
            }
            return true;
        }
        finally{deliveryReplacement.Release();}
    }
    NativeEpochResources(NativeMarketDataRuntime runtime,NativeContractCatalog catalog,NativeLastPriceDirectory prices,
        NativeOptionChainRuntime scopes,Guid optionGeneration,Guid futuresGeneration,
        FrozenDictionary<string,(MarketDataLane Lane,TickContractMapping Mapping)> mappings,
        NativeMarketDataDelivery receiver,Func<NativeMarketDataDelivery> createReceiver,NativeFuturesPublication futures,TickQuoteBufferPool pool,
        NativeOptionTradeSourceReader trades,NativeEpochObservers observers,TimeProvider clock,NativeFuturesDelivery primary,ITickLiveRouter liveRoutes,FrozenDictionary<MarketDataLane,SourceIdentity> identities,ConcurrentDictionary<string,NativeRegistration> optionUnderlyings)
    {Runtime=runtime;Catalog=catalog;LastPrices=prices;OptionScopes=scopes;FuturesGeneration=futuresGeneration;
        Futures=mappings;this.receiver=receiver;this.createReceiver=createReceiver;this.futures=futures;this.pool=pool;this.trades=trades;this.observers=observers;this.clock=clock;FuturesDelivery=primary;LiveRoutes=liveRoutes;this.identities=identities;this.optionUnderlyings=optionUnderlyings;}

    /// <summary>Bootstraps using one original caller budget, at most 120 seconds.
    /// Replay starts and current time come only from API policy. Endpoints are
    /// explicit to support local qualification without production connections.
    /// This data-plane bootstrap accepts configured futures; options are acquired
    /// later through complete qualified contexts in OptionScopes.</summary>
    public static async Task<NativeEpochResources> StartAsync(string libraryPath,string apiKey,string historyEndpoint,
        string[] numericDnsServers,DateOnly valueDate,DatabentoMarketDataRuntimeOptions options,
        IReadOnlyCollection<NativeDatasetEndpoint> endpoints,NativeOptionConnection optionConnection,TimeProvider clock,
        bool advancingClock,bool production,ITickAggregationEventPublisher publisher,IOptionTradeEvidenceWriter evidence,
        ITickLiveRouter liveRoutes,Func<OptionPricingContext,DateTimeOffset,DateTimeOffset> qualifyUntil,
        Func<DateOnly,long?> replayStart,NativeEpochObservers observers,TimeSpan remainingBudget,bool requireFuturesObservations=true,CancellationToken token=default)
    {
        ArgumentNullException.ThrowIfNull(options);ArgumentNullException.ThrowIfNull(endpoints);ArgumentNullException.ThrowIfNull(optionConnection);
        ArgumentNullException.ThrowIfNull(clock);ArgumentNullException.ThrowIfNull(publisher);ArgumentNullException.ThrowIfNull(evidence);
        ArgumentNullException.ThrowIfNull(liveRoutes);ArgumentNullException.ThrowIfNull(qualifyUntil);ArgumentNullException.ThrowIfNull(replayStart);
        ArgumentNullException.ThrowIfNull(observers);ArgumentNullException.ThrowIfNull(observers.Diagnostic);ArgumentNullException.ThrowIfNull(observers.Warning);
        ArgumentNullException.ThrowIfNull(observers.ScopeStatus);ArgumentNullException.ThrowIfNull(observers.Fault);
        token.ThrowIfCancellationRequested();
        if(remainingBudget<TimeSpan.Zero)throw new ArgumentOutOfRangeException(nameof(remainingBudget));
        if(remainingBudget>TimeSpan.FromMinutes(2))remainingBudget=TimeSpan.FromMinutes(2);
        var started=Stopwatch.GetTimestamp();
        TimeSpan Remaining()=>TimeSpan.FromTicks(Math.Max(0,remainingBudget.Ticks-Stopwatch.GetElapsedTime(started).Ticks));
        using var bounded=CancellationTokenSource.CreateLinkedTokenSource(token);bounded.CancelAfter(Remaining());
        options=options with{Contracts=options.Contracts.ToArray()};
        if(valueDate==default||options.Contracts.Count is <1 or >2048||options.Contracts.Any(x=>x.AssetTypeId!=AssetTypeId.Futures)
            ||options.LastPriceCapacity is <1 or >4096||options.FuturesQuoteBatchCapacity is 0 or >4096)
            throw new ArgumentException("Bootstrap requires configured futures; options require complete qualified scope contexts.",nameof(options));
        var endpointsByLane=endpoints.ToFrozenDictionary(x=>x.Lane);
        foreach(var registration in options.Contracts)
        {
            var dataset=string.IsNullOrWhiteSpace(registration.Dataset)?options.FeedOptions.Dataset:registration.Dataset;
            var lane=dataset switch{"GLBX.MDP3"=>MarketDataLane.FuturesGlbx,"XCBF.PITCH"=>MarketDataLane.FuturesXcbf,
                _=>throw new ArgumentException("The native backend does not qualify this futures dataset.",nameof(options))};
            if(!endpointsByLane.TryGetValue(lane,out var endpoint)||string.IsNullOrWhiteSpace(endpoint.Host)||endpoint.Port==0)
                throw new ArgumentException("Every configured futures dataset requires a valid explicit native endpoint.",nameof(endpoints));
        }
        var runtime=new NativeMarketDataRuntime(libraryPath);
        NativeOptionChainRuntime? scopes=null;NativeLastPriceDirectory? prices=null;NativeMarketDataDelivery? receiver=null;
        NativeFuturesPublication? assembly=null;TickQuoteBufferPool? pool=null;NativeOptionTradeSourceReader? tradeSource=null;
        try
        {
            // Stable managed handles may outlive an inactive scope. Native hot
            // capacity is independently capped at 2048 options + 64 dependencies.
            runtime.ConfigureLive(valueDate,Guid.NewGuid(),clock.GetUtcNow(),advancingClock,checked((uint)Math.Min(2112,options.LastPriceCapacity)));
            runtime.ConfigureCatalog(16384,16*1024*1024);runtime.ConfigureHistory(historyEndpoint,apiKey,numericDnsServers);
            var catalog=await NativeContractCatalog.CreateAsync(runtime,options,clock,Remaining(),bounded.Token).ConfigureAwait(false);
            static MarketDataLane Lane(string dataset)=>dataset switch{"GLBX.MDP3"=>MarketDataLane.FuturesGlbx,"XCBF.PITCH"=>MarketDataLane.FuturesXcbf,
                _=>throw new ArgumentException("The native backend does not qualify this futures dataset.")};
            var mappings=catalog.Definitions.ToFrozenDictionary(pair=>pair.Key,pair=>
            {
                var d=pair.Value;var future=catalog.FindFutures(pair.Key)!;
                var mapping=new TickContractMapping(d.Dataset,valueDate,d.Publisher,d.Instrument,pair.Key,AssetTypeId.Futures,new TickerContractDetails{
                    ContractId=pair.Key,Dataset=d.Dataset,DefinitionDate=valueDate,PublisherId=d.Publisher,InstrumentId=d.Instrument,AssetTypeId=AssetTypeId.Futures,
                    ProviderContractId=d.Symbol,Ticker=d.Asset,LocalSymbol=d.Symbol,SecurityType=d.SecurityType,Currency=future.Currency,Exchange=d.Exchange,
                    ContractMultiplier=d.Multiplier??1,TickSize=d.Tick is >0 and <long.MaxValue?d.Tick/1_000_000_000m:null,
                    MaturityDate=future.LastTradeDate,IsOnTheRun=future.OnTheRun});
                return(Lane:Lane(d.Dataset),Mapping:mapping);
            },StringComparer.Ordinal);
            foreach(var lane in mappings.Values.Select(x=>x.Lane).Distinct())
                if(!endpointsByLane.ContainsKey(lane))throw new ArgumentException("Every configured futures dataset requires an explicit native endpoint.",nameof(endpoints));
            await runtime.PrepareOptionOwnerAsync(Guid.Empty,Remaining(),bounded.Token).ConfigureAwait(false);
            var optionGeneration=runtime.ReadOptionOwnerGeneration();var futuresGeneration=Guid.NewGuid();
            var identities=mappings.Values.Select(x=>x.Lane).Distinct().ToFrozenDictionary(x=>x,_=>new SourceIdentity(futuresGeneration));
            var optionUnderlyings=new ConcurrentDictionary<string,NativeRegistration>(catalog.Definitions.Select(x=>new KeyValuePair<string,NativeRegistration>(x.Key,new(x.Value.Instrument,x.Key,x.Value.Instrument.ToString(System.Globalization.CultureInfo.InvariantCulture),false,x.Value.Tick,false,true,x.Value.Publisher))),StringComparer.Ordinal);
            NativeRegistration Resolve(string id)=>optionUnderlyings.TryGetValue(id,out var value)?value:throw new KeyNotFoundException("Option underlying is not qualified in this epoch.");
            scopes=new(runtime,optionConnection,optionGeneration,valueDate,clock,options.OptionPricingRefresh,production,Resolve,qualifyUntil,replayStart,
                error=>observers.Fault(null,error));
            prices=new(runtime,scopes,valueDate,mappings.Select(x=>(x.Key,x.Value.Lane)),options.LastPriceCapacity);
            pool=new([(options.FuturesQuoteBatchCapacity,checked(mappings.Count+8))]);
            assembly=new(publisher,pool,valueDate,futuresGeneration,mappings.Values.ToArray(),options.FuturesQuoteBatchCapacity,
                (lane,physical)=>runtime.ReadLiveStatus(lane).Generation==physical,liveRoutes,lane=>identities[lane].Current);
            var primary=new NativeFuturesDelivery(runtime,assembly);
            tradeSource=new(runtime,optionGeneration,valueDate,()=>scopes.Generation);
            var quotes=new NativeOptionQuoteDelivery(scopes,new(optionGeneration,publisher,scopes.IsMonitoredContract,clock,()=>scopes.Generation));
            var trades=new NativeOptionTradeDelivery(tradeSource,new(runtime),scopes,evidence,publisher,liveRoutes);
            Func<NativeMarketDataDelivery> createReceiver=()=>new(runtime,scopes,quotes,trades,primary.OnChangesAsync,observers.Diagnostic,observers.Warning,observers.ScopeStatus,observers.Fault);
            receiver=createReceiver();
            foreach(var group in mappings.Values.GroupBy(x=>x.Lane).OrderBy(x=>x.Key==MarketDataLane.FuturesXcbf?0:1))
            {
                var endpoint=endpointsByLane[group.Key];var ids=group.Select(x=>x.Mapping.InstrumentId.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                var replay=replayStart(valueDate);
                await runtime.StartLiveLaneAsync(group.Key,apiKey,endpoint.Host,endpoint.Port,numericDnsServers,
                    [new(ids,"trades","instrument_id",replay),new(ids,"mbp-1","instrument_id"),new(ids,"statistics","instrument_id",replay)],
                    group.Select(x=>{var d=catalog.Definitions[x.Mapping.ContractId];return Resolve(x.Mapping.ContractId) with{Vwap=d.Asset=="ES"};}).ToArray(),
                    requireFuturesObservations,Remaining(),bounded.Token).ConfigureAwait(false);
            }
            bounded.Token.ThrowIfCancellationRequested();
            return new(runtime,catalog,prices,scopes,optionGeneration,futuresGeneration,mappings,receiver,createReceiver,assembly,pool,tradeSource,observers,clock,primary,liveRoutes,identities,optionUnderlyings);
        }
        catch(Exception original)
        {
            // Signal every native task even if managed callback cleanup fails.
            // Never unload memory still owned by an unjoined callback.
            prices?.Dispose();var failures=new List<Exception>{original};
            // Preserve the last classified lane reason before cancellation
            // retires its owner. Otherwise a two-minute bootstrap failure can
            // hide repeated processing faults behind TaskCanceledException.
            foreach(var endpoint in endpoints)
                try{if(runtime.ReadLiveStatus(endpoint.Lane) is{Error:not null} failed)
                    failures.Add(new InvalidOperationException($"Native {endpoint.Lane} bootstrap phase {failed.Phase}, attempt {failed.Attempt}: {failed.Error}"));}
                catch(Exception observation){failures.Add(observation);}
            if(receiver is not null)try{await receiver.StopAndDisposeAsync(Remaining()).ConfigureAwait(false);}catch(Exception e){failures.Add(e);}
            if(scopes is not null)try{await scopes.StopAndDisposeAsync(Remaining()).ConfigureAwait(false);}catch(Exception e){failures.Add(e);}
            try{await runtime.StopAndDisposeAsync(Remaining()).ConfigureAwait(false);}catch(Exception e){failures.Add(e);}
            if(receiver is null||receiver.IsJoined){assembly?.Dispose();pool?.Dispose();tradeSource?.Dispose();}
            if(failures.Count>1)throw new AggregateException("Native epoch bootstrap and cleanup failed.",failures);
            throw;
        }
    }
    /// <summary>Stops all ownership using one original budget and no renewal.</summary>
    public ValueTask StopAndDisposeAsync(TimeSpan remainingBudget)
    {
        if(remainingBudget<TimeSpan.Zero)throw new ArgumentOutOfRangeException(nameof(remainingBudget));
        if(remainingBudget>TimeSpan.FromMinutes(2))remainingBudget=TimeSpan.FromMinutes(2);
        lock(gate)return new(disposal??=StopAsync(remainingBudget));
    }
    /// <inheritdoc/>
    public ValueTask DisposeAsync()=>StopAndDisposeAsync(TimeSpan.FromMinutes(2));
    async Task StopAsync(TimeSpan total)
    {
        var started=Stopwatch.GetTimestamp();TimeSpan Remaining()=>TimeSpan.FromTicks(Math.Max(0,total.Ticks-Stopwatch.GetElapsedTime(started).Ticks));
        LastPrices.Dispose();var failures=new List<Exception>();
        try{await receiver.StopAndDisposeAsync(Remaining()).ConfigureAwait(false);}catch(Exception e){failures.Add(e);}
        Task? flush=null;
        if(receiver.IsJoined)
            try
            {
                using var deadline=new CancellationTokenSource(Remaining());
                flush=futures.FlushAsync(QuoteEmissionReason.FeedStopped,clock.GetUtcNow(),deadline.Token).AsTask();
                await flush.WaitAsync(deadline.Token).ConfigureAwait(false);
            }
            catch(Exception e){failures.Add(e);}
        try{await OptionScopes.StopAndDisposeAsync(Remaining()).ConfigureAwait(false);}catch(Exception e){failures.Add(e);}
        try{await Runtime.StopAndDisposeAsync(Remaining()).ConfigureAwait(false);}catch(Exception e){failures.Add(e);}
        if(receiver.IsJoined&&(flush is null||flush.IsCompleted)){_=flush?.Exception;futures.Dispose();pool.Dispose();trades.Dispose();}
        if(failures.Count!=0){var error=new AggregateException("Native epoch cleanup failed within its original deadline.",failures);try{observers.Fault(null,error);}catch(Exception observer){throw new AggregateException("Native cleanup and fault reporting failed.",error,observer);}throw error;}
    }
}
