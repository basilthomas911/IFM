using System.Collections.Immutable;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Diagnostics;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.MarketData.Rust.Api;
using TomasAI.IFM.Application.MarketData.Rust.Delivery;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.MarketData.Contracts.Pricing;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;

namespace TomasAI.IFM.Application.MarketData.Rust.OptionChains;

/// <summary>Application lease authority over independently owned native scopes.
/// Preserves current worker request hashes, ownership revisions and temporary
/// lease semantics while provider tasks, statistics and pricing remain native.
/// All awaited control steps share the caller's original capped budget.</summary>
public sealed class NativeOptionChainRuntime : ICompositionMarketSource,IAsyncDisposable
{
    readonly NativeMarketDataRuntime runtime;
    readonly NativeOptionConnection connection;
    readonly TimeProvider clock;
    readonly OptionPricingRefreshPolicy policy;
    sealed record OwnerIdentity(Guid Value);
    OwnerIdentity identity;
    Guid generation=>Volatile.Read(ref identity).Value;
    /// <summary>Current joined/replaced native logical owner identity.</summary>
    public Guid Generation=>generation;
    readonly DateOnly valueDate;
    readonly bool production;
    readonly Func<string,NativeRegistration> resolveUnderlying;
    readonly Func<OptionPricingContext,DateTimeOffset,DateTimeOffset> qualifyUntil;
    readonly Func<DateOnly,long?> replayStart;
    readonly SemaphoreSlim gate=new(1);
    readonly Dictionary<string,Scope> scopes=new(StringComparer.Ordinal);
    FrozenDictionary<string,Scope> renewalScopes=FrozenDictionary<string,Scope>.Empty;
    readonly Dictionary<string,NativeRegistration> prerequisiteUnderlyings=new(StringComparer.Ordinal);
    static string UnderlyingScope(string contract)=>"underlying-"+PricingSemanticHash.Compute(new{Underlying=contract});
    readonly HashSet<Guid> ended=[];
    HashSet<string>? replacementScopes;
    /// <summary>Replacement is incomplete; an empty monitoring view is not readiness.</summary>
    public bool IsReplacing=>Volatile.Read(ref suspended)!=0||Volatile.Read(ref replacementScopes) is not null;
    /// <summary>Revokes local hot-read authority before immediate native stop.</summary>
    public void SuspendMonitoring(){Volatile.Write(ref suspended,1);Volatile.Write(ref monitoring,new([],Array.Empty<(string,Guid)>().ToFrozenSet(),Array.Empty<string>().ToFrozenSet(StringComparer.Ordinal),[],FrozenDictionary<string,NativeMonitoredOptionQuote>.Empty,FrozenDictionary<(string,uint),NativeMonitoredOptionQuote>.Empty));}
    readonly Dictionary<string,(string Authority,long Revision,string Digest)> watermarks=new(StringComparer.Ordinal);
    readonly CancellationTokenSource stopping=new();
    readonly Task expiry;
    readonly Action<Exception> fault;
    readonly object disposalGate=new();
    Task? disposal;
    int disposed,suspended;
    sealed record Monitoring(ImmutableArray<NativeMonitoredOptionQuote> Entries,FrozenSet<(string Scope,Guid Context)> Keys,FrozenSet<string> Contracts,ImmutableArray<string> Scopes,FrozenDictionary<string,NativeMonitoredOptionQuote> ByContract,FrozenDictionary<(string Scope,uint Instrument),NativeMonitoredOptionQuote> ByInstrument);
    Monitoring monitoring=new([],Array.Empty<(string,Guid)>().ToFrozenSet(),Array.Empty<string>().ToFrozenSet(StringComparer.Ordinal),[],FrozenDictionary<string,NativeMonitoredOptionQuote>.Empty,FrozenDictionary<(string,uint),NativeMonitoredOptionQuote>.Empty);
    /// <summary>Reads the immutable current monitoring directory without a gate,
    /// I/O or subscription acquisition. Rebuilds occur only on scope transitions.</summary>
    public ImmutableArray<NativeMonitoredOptionQuote> MonitoredQuotes=>Volatile.Read(ref monitoring).Entries;
    /// <summary>All currently owned native scopes, including discovery-only
    /// scopes whose trades still require durable source evidence.</summary>
    public ImmutableArray<string> ActiveScopes=>Volatile.Read(ref monitoring).Scopes;
    /// <summary>Captures coherent ownership without waiting on provider/control
    /// work. Busy acquisition is explicitly pending, never a false empty idle.</summary>
    public TomasAI.IFM.Application.MarketData.Contracts.NativeOptionWorkloadEvidence CaptureWorkloadEvidence()
    {
        if(!gate.Wait(0))return new(generation,clock.GetUtcNow(),true,[],[],[]);
        try
        {
            var active=ActiveScopes.ToArray();
            return new(generation,clock.GetUtcNow(),IsReplacing,active,
                active.Where(id=>runtime.ReadOptionScopeStatus(id).Phase==3).ToArray(),
                active.Where(id=>!id.StartsWith("underlying-",StringComparison.Ordinal)).ToArray());
        }
        finally{gate.Release();}
    }
    /// <summary>Checks current monitoring authority without waiting on control.</summary>
    public bool IsMonitoredContract(string contractId)=>Volatile.Read(ref monitoring).Contracts.Contains(contractId);
    /// <summary>Finds the current immutable monitoring binding without I/O or a
    /// control lock. Stable epoch readers resolve again on every read so an old
    /// context cannot survive a lease release or atomic reference replacement.</summary>
    public NativeMonitoredOptionQuote? FindMonitoredQuote(string contractId)=>Volatile.Read(ref monitoring).ByContract.GetValueOrDefault(contractId);
    /// <summary>Finds an authoritative current source route in constant time.</summary>
    public NativeMonitoredOptionQuote? FindMonitoredQuote(string scope,uint instrument)=>Volatile.Read(ref monitoring).ByInstrument.GetValueOrDefault((scope,instrument));
    void PublishMonitoring()
    {
        if(Volatile.Read(ref suspended)!=0)return;
        Volatile.Write(ref renewalScopes,scopes.Where(x=>!x.Value.Retiring).ToFrozenDictionary(StringComparer.Ordinal));
        var previous=Volatile.Read(ref monitoring);
        var candidates=scopes.OrderByDescending(pair=>pair.Value.Key.Exact.Length!=0).ThenBy(pair=>pair.Key,StringComparer.Ordinal)
            .Where(pair=>!pair.Value.Retiring).SelectMany(pair=>pair.Value.Binding.Prepared
                .Where(option=>pair.Value.Key.Exact.Length!=0||pair.Value.Owners.Any(owner=>owner.ContractIds.Contains(option.Context.Contract.ContractId,StringComparer.Ordinal)))
                .Select(option=>(Id:pair.Key,Scope:pair.Value,Prepared:option)))
            .GroupBy(x=>x.Prepared.Context.Contract.ContractId,StringComparer.Ordinal).Select(group=>group.First()).ToArray();
        var entries=candidates.Select(candidate=>{
            var scope=candidate.Scope;var binding=scope.Binding;var prepared=candidate.Prepared;var id=candidate.Id;
            // Ownership-only changes must not discard a durably acknowledged
            // trade held by this reader. A context/physical-scope replacement
            // still receives a fresh fenced binding.
            if(previous.ByContract.TryGetValue(prepared.Context.Contract.ContractId,out var old)
                &&old.ScopeId==id&&ReferenceEquals(old.Prepared,prepared)&&old.IsCurrent())return old;
            return new NativeMonitoredOptionQuote(id,scope.Key.Expiry,prepared,new(runtime,id,generation,valueDate,()=>prepared,clock),
                ()=>Volatile.Read(ref suspended)==0&&!scope.Retiring&&ReferenceEquals(scope.Binding,binding)&&Volatile.Read(ref monitoring).Keys.Contains((id,prepared.Identity)));
        }).ToImmutableArray();
        Volatile.Write(ref monitoring,new(entries,entries.Select(x=>(x.ScopeId,x.Prepared.Identity)).ToFrozenSet(),
            entries.Select(x=>x.Prepared.Context.Contract.ContractId).ToFrozenSet(StringComparer.Ordinal),
            scopes.Where(x=>!x.Value.Retiring).Select(x=>x.Key).Concat(prerequisiteUnderlyings.Keys.Select(UnderlyingScope)).Order(StringComparer.Ordinal).ToImmutableArray(),
            entries.ToFrozenDictionary(x=>x.Prepared.Context.Contract.ContractId,StringComparer.Ordinal),
            entries.ToFrozenDictionary(x=>(x.ScopeId,x.Prepared.Context.Contract.InstrumentId))));
    }
    sealed record PhysicalKey(string Underlying,DateOnly Expiry,string Exact,string Independent);
    sealed record Binding(ImmutableArray<WorkerOptionDefinition> Options,ImmutableArray<PreparedOptionPricingContext> Prepared,string Digest);
    sealed class Scope(PhysicalKey key,Binding binding,Guid lease,DateTimeOffset expiry)
    {
        internal readonly PhysicalKey Key=key;
        internal volatile Binding Binding=binding;
        internal TomasAI.IFM.Shared.MarketData.Contracts.OptionChainStatisticsSnapshot[] Statistics=[];
        internal readonly ConcurrentDictionary<Guid,DateTimeOffset> Leases=new(new[]{new KeyValuePair<Guid,DateTimeOffset>(lease,expiry)});
        internal ImmutableArray<WorkerOptionChainOwner> Owners=[];
        internal string? Authority,OwnershipDigest;
        internal long Revision=-1;
        internal volatile bool Retiring;
    }
    sealed class Budget
    {
        readonly long started=Stopwatch.GetTimestamp();
        readonly TimeSpan total;
        internal Budget(TimeSpan total){if(total<TimeSpan.Zero)throw new ArgumentOutOfRangeException(nameof(total));this.total=total>TimeSpan.FromMinutes(2)?TimeSpan.FromMinutes(2):total;}
        internal TimeSpan Remaining=>TimeSpan.FromTicks(Math.Max(0,total.Ticks-Stopwatch.GetElapsedTime(started).Ticks));
    }
    /// <summary>Creates lease bookkeeping only. Underlying resolution and the
    /// qualified-until boundary are application reference policy, must not open
    /// I/O, and must use the supplied API time. The boundary includes earlier
    /// calendar/tenor cutovers. Replay start is explicitly API ValueDate based.
    /// The required fault sink must enter application recovery on expiry failures.</summary>
    public NativeOptionChainRuntime(NativeMarketDataRuntime runtime,NativeOptionConnection connection,Guid generation,DateOnly valueDate,
        TimeProvider clock,OptionPricingRefreshPolicy policy,bool production,Func<string,NativeRegistration> resolveUnderlying,
        Func<OptionPricingContext,DateTimeOffset,DateTimeOffset> qualifyUntil,Func<DateOnly,long?> replayStart,Action<Exception> fault)
    {
        ArgumentNullException.ThrowIfNull(runtime);ArgumentNullException.ThrowIfNull(connection);ArgumentNullException.ThrowIfNull(clock);ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(resolveUnderlying);ArgumentNullException.ThrowIfNull(qualifyUntil);ArgumentNullException.ThrowIfNull(replayStart);ArgumentNullException.ThrowIfNull(fault);
        if(generation==Guid.Empty||valueDate==default||runtime.LiveValueDate!=valueDate||runtime.ReadOptionOwnerGeneration()!=generation)throw new ArgumentException("A prepared native API date/options owner is required.");
        policy.Validate();this.runtime=runtime;this.connection=connection;identity=new(generation);this.valueDate=valueDate;this.clock=clock;this.policy=policy;this.production=production;
        this.resolveUnderlying=resolveUnderlying;this.qualifyUntil=qualifyUntil;this.replayStart=replayStart;this.fault=fault;expiry=ExpireAsync();
    }
    /// <summary>Restarts every currently owned option connection without draining
    /// old records. Logical leases remain owned; physical source identities change
    /// and old enriched observations are fenced by their source generation. Gate
    /// acquisition and every restart consume the same original caller budget.</summary>
    public Task ResetAsync(TimeSpan remainingBudget,CancellationToken cancellationToken=default)=>ResetCoreAsync(null,remainingBudget,cancellationToken);
    /// <summary>Resets only scopes containing the requested monitored contracts.
    /// Unknown or duplicate inputs fail before any provider ownership changes.</summary>
    public Task<string[]> ResetContractsAsync(IReadOnlyCollection<string> contracts,TimeSpan remainingBudget,CancellationToken cancellationToken=default)
    {
        ArgumentNullException.ThrowIfNull(contracts);
        if(contracts.Count is <1 or >2048||contracts.Any(string.IsNullOrWhiteSpace)||contracts.Distinct(StringComparer.Ordinal).Count()!=contracts.Count)throw new ArgumentException("A bounded unique option contract set is required.",nameof(contracts));
        return ResetCoreAsync(contracts.ToHashSet(StringComparer.Ordinal),remainingBudget,cancellationToken);
    }
    async Task<string[]> ResetCoreAsync(HashSet<string>? contracts,TimeSpan remainingBudget,CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed)!=0,this);
        var budget=new Budget(remainingBudget);
        using var linked=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,stopping.Token);
        linked.CancelAfter(budget.Remaining);
        await EnterControlAsync(cancellationToken,linked.Token).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed)!=0,this);
            // Bounded scope count permits concurrent cancellation/reconnect; one
            // slow scope cannot delay signaling the others. No new deadline here.
            var selected=scopes.Where(x=>!x.Value.Retiring&&(contracts is null||x.Value.Binding.Options.Any(option=>contracts.Contains(option.Pricing.Contract.ContractId)))).ToArray();
            var found=selected.SelectMany(x=>x.Value.Binding.Options).Select(x=>x.Pricing.Contract.ContractId).Where(x=>contracts is null||contracts.Contains(x)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            if(contracts is not null&&found.Length!=contracts.Count)throw new KeyNotFoundException("A requested option repair contract is not currently owned.");
            await Task.WhenAll(selected.Select(x=>runtime.ResetOptionScopeAsync(x.Key,generation,budget.Remaining,linked.Token))).WaitAsync(linked.Token).ConfigureAwait(false);
            return found;
        }
        finally{gate.Release();}
    }
    /// <summary>Physically joins and replaces the whole options task subtree.
    /// Managed lease authority survives; pricing inputs must be requalified by
    /// application policy for the new generation. No prior queue is drained.
    /// Invoke while the epoch excludes all option delivery/read callbacks.</summary>
    public async Task<IReadOnlyList<WorkerOptionChainRequest>> ReplaceOwnerAsync(
        Func<ImmutableArray<WorkerOptionDefinition>,Guid,CancellationToken,Task<ImmutableArray<WorkerOptionDefinition>>> requalify,
        TimeSpan remainingBudget,CancellationToken cancellationToken=default)
    {
        ArgumentNullException.ThrowIfNull(requalify);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed)!=0,this);
        var budget=new Budget(remainingBudget);
        using var linked=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,stopping.Token);
        linked.CancelAfter(budget.Remaining);
        await EnterControlAsync(cancellationToken,linked.Token).ConfigureAwait(false);
        try
        {
            replacementScopes??=scopes.Where(x=>!x.Value.Retiring).Select(x=>x.Key).ToHashSet(StringComparer.Ordinal);
            // Expired temporary owners are not durable restore intent. The
            // whole native join below retires their physical resources too.
            var now=clock.GetUtcNow();
            foreach(var pair in scopes.ToArray())
            {
                foreach(var lease in pair.Value.Leases.Where(x=>x.Value<=now).ToArray())
                {if(((ICollection<KeyValuePair<Guid,DateTimeOffset>>)pair.Value.Leases).Remove(lease))ended.Add(lease.Key);}
                if(pair.Value.Leases.Count==0&&pair.Value.Owners.IsEmpty){scopes.Remove(pair.Key);replacementScopes.Remove(pair.Key);}
            }
            var retained=scopes.Where(x=>replacementScopes.Contains(x.Key)).ToArray();
            // Retire public bindings before destroying native storage. Stable
            // handles become unavailable rather than exposing stale snapshots.
            foreach(var item in scopes.Values)item.Retiring=true;
            PublishMonitoring();
            // A prior command may have replaced native ownership immediately
            // before its acknowledgement was canceled. Reconcile the actual
            // owner and join it; never retry forever against a cached GUID.
            var old=runtime.ReadOptionOwnerGeneration();
            await runtime.PrepareOptionOwnerAsync(old,budget.Remaining,linked.Token).ConfigureAwait(false);
            var next=runtime.ReadOptionOwnerGeneration();
            if(next==Guid.Empty||next==old)throw new InvalidOperationException("Native owner replacement did not change identity.");
            Volatile.Write(ref identity,new(next));
            // Tombstones are generation-scoped, just as in a replaced C#
            // worker. Old-generation commands are already fenced above. Retain
            // active scopes' own revisions while releasing obsolete history so
            // full recovery also resets exhausted bookkeeping capacity.
            ended.Clear();watermarks.Clear();
            var restored=new List<WorkerOptionChainRequest>();
            foreach(var pair in retained)
            {
                var scope=pair.Value;
                var options=await requalify(scope.Binding.Options,next,linked.Token).WaitAsync(linked.Token).ConfigureAwait(false);
                if(options.IsDefaultOrEmpty||WorkerOptionChainRuntime.PhysicalDigest(options)!=WorkerOptionChainRuntime.PhysicalDigest(scope.Binding.Options)
                    ||options.Any(x=>x.Pricing.GenerationId!=next||Black76PricingModel.ValidateContext(x.Pricing,clock.GetUtcNow()) is not null))
                    throw new InvalidDataException("Restored native pricing context failed generation/reference/physical identity qualification.");
                var at=clock.GetUtcNow();var prepared=Prepare(options,scope.Owners,scope.Key.Exact.Length!=0,at);
                var digest=PricingSemanticHash.Compute(new{MaturityDate=scope.Key.Expiry,SeparateContractConnection=scope.Key.Exact.Length!=0,Options=options});
                await connection.StartAsync(runtime,pair.Key,WorkerOptionChainRuntime.PhysicalDigest(options),next,resolveUnderlying(scope.Key.Underlying),
                    prepared.ToArray(),scope.Statistics,digest,policy,production,at,null,budget.Remaining,linked.Token).ConfigureAwait(false);
                scope.Binding=new(options,prepared,digest);
                foreach(var lease in scope.Leases.Where(x=>x.Value>clock.GetUtcNow()))
                    restored.Add(new(pair.Key,lease.Key,next,valueDate,scope.Key.Expiry,lease.Value,options,
                        SeparateContractConnection:scope.Key.Exact.Length!=0,IndependentChainConnection:scope.Key.Independent.Length!=0));
            }
            foreach(var dependency in prerequisiteUnderlyings.Values)
                await connection.StartUnderlyingAsync(runtime,UnderlyingScope(dependency.Contract),next,dependency,budget.Remaining,linked.Token).ConfigureAwait(false);
            linked.Token.ThrowIfCancellationRequested();
            foreach(var pair in retained)pair.Value.Retiring=false;
            Volatile.Write(ref suspended,0);PublishMonitoring();replacementScopes=null;return restored;
        }
        finally{gate.Release();}
    }

    static WorkerOptionChainResult Failure(string code)=>new(false,new(code,"Chain","","Qualified worker chain admission failed."));
    /// <summary>Owns a bounded exact futures-quote prerequisite under the options
    /// subtree. No option pricing context, primary futures route or fake price is
    /// created. Dependencies survive whole-owner replacement and retire at close.</summary>
    public async Task<decimal?> ReadUnderlyingPriceAsync(NativeRegistration underlying,TimeSpan remainingBudget,CancellationToken token)
    {
        if(underlying.Option||underlying.Instrument==0||underlying.Publisher==0)throw new ArgumentException("A qualified futures definition is required.",nameof(underlying));
        var budget=new Budget(remainingBudget);using var linked=CancellationTokenSource.CreateLinkedTokenSource(token,stopping.Token);linked.CancelAfter(budget.Remaining);
        await EnterControlAsync(token,linked.Token).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed)!=0,this);
            if(IsReplacing)throw new InvalidOperationException("Option underlying recovery is in progress.");
            if(prerequisiteUnderlyings.TryGetValue(underlying.Contract,out var previous)&&previous!=underlying)throw new InvalidDataException("Qualified underlying identity changed within its API epoch.");
            if(!prerequisiteUnderlyings.ContainsKey(underlying.Contract)&&prerequisiteUnderlyings.Count>=16)throw new InvalidOperationException("Option underlying dependency capacity exceeded.");
            var scope=UnderlyingScope(underlying.Contract);
            try{await connection.StartUnderlyingAsync(runtime,scope,generation,underlying,budget.Remaining,linked.Token).ConfigureAwait(false);}
            catch(Exception error)
            {
                // Retain failed desired ownership too: terminal native phase
                // must remain visible to the original-deadline coordinator,
                // rather than disappearing as an apparently healthy idle scope.
                prerequisiteUnderlyings[underlying.Contract]=underlying;PublishMonitoring();
                string? detail;try{detail=runtime.ReadOptionScopeStatus(scope).Error;}catch{detail="Native scope status unavailable.";}
                throw new InvalidOperationException($"Option underlying {underlying.Contract} could not qualify: {detail}",error);
            }
            prerequisiteUnderlyings[underlying.Contract]=underlying;PublishMonitoring();
            var frame=runtime.ReadOptionScopeState(scope,underlying.Instrument,underlying.Publisher);
            if(!frame.TryGetMarketPrice(underlying.Contract,AssetTypeId.Futures,out var price)||price.Quote is not{} quote)return null;
            var now=clock.GetUtcNow();
            return quote.BidPrice is>0&&quote.AskPrice>=quote.BidPrice&&quote.EventTimestamp<=now&&now-quote.EventTimestamp<=TimeSpan.FromMinutes(15)
                ?quote.BidPrice/2m+quote.AskPrice/2m:null;
        }
        finally{gate.Release();}
    }
    /// <summary>Acquires/renews an exact temporary scope, or performs an owned
    /// compare-and-swap reference refresh. Repeated healthy renewals open no socket.</summary>
    public async Task<WorkerOptionChainResult> AcquireAsync(WorkerOptionChainRequest request,TimeSpan remainingBudget,CancellationToken cancellationToken=default)
    {
        ArgumentNullException.ThrowIfNull(request);ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed)!=0,this);
        var budget=new Budget(remainingBudget);
        cancellationToken.ThrowIfCancellationRequested();
        if(TryRenewHealthyLease(request,cancellationToken,out var renewal))return renewal;
        using var linked=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,stopping.Token);linked.CancelAfter(budget.Remaining);
        await EnterControlAsync(cancellationToken,linked.Token).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed)!=0,this);
            if(Volatile.Read(ref suspended)!=0)return Failure("Recovering");
            await RemoveExpiredAsync(budget,linked.Token).ConfigureAwait(false);var at=clock.GetUtcNow();
            if(production&&!policy.IsProductionQualified)return Failure("PricingRefreshPolicyUnreviewed");
            if(request.GenerationId!=generation||request.ValueDate!=valueDate)return Failure("Recovering");
            if(string.IsNullOrWhiteSpace(request.ScopeId)||request.ScopeId.Length>128||request.LeaseId==Guid.Empty||request.LeaseExpiresAtUtc.Offset!=TimeSpan.Zero
                ||request.LeaseExpiresAtUtc<=at||request.LeaseExpiresAtUtc>at.AddSeconds(120)||request.Options.IsDefaultOrEmpty||request.Options.Length>2048
                ||MessagePackBinarySerializer.MeasureContent(request)>4*1024*1024)return Failure("InvalidChainRequest");
            if(ended.Contains(request.LeaseId))return Failure("LeaseEnded");
            if(ended.Count+scopes.Values.Sum(x=>x.Leases.Count)>=4096&&!scopes.Values.Any(x=>x.Leases.ContainsKey(request.LeaseId)))return Failure("LeaseCapacity");
            if(scopes.Any(x=>x.Key!=request.ScopeId&&x.Value.Leases.ContainsKey(request.LeaseId)))return Failure("LeaseIdentityConflict");
            var ids=new HashSet<string>(StringComparer.Ordinal);var instruments=new HashSet<uint>();string? underlying=null;
            foreach(var option in request.Options)
            {
                if(option?.Pricing is not {} context)return Failure("PricingContextUnavailable");
                var failure=Black76PricingModel.ValidateContext(context,at);if(failure is not null)return new(false,failure);
                var c=context.Contract;
                if(context.GenerationId!=generation||c.Dataset!="GLBX.MDP3"||option.Strike<=0
                    ||c.SchemaVersion==3&&(c.Strike!=option.Strike||c.Right!=(option.IsCall?PricingOptionRight.Call:PricingOptionRight.Put))
                    ||!ids.Add(c.ContractId)||!instruments.Add(c.InstrumentId)||underlying is not null&&underlying!=c.UnderlyingContractId
                    ||DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(c.ExpirationUtc,TimeZoneInfo.FindSystemTimeZoneById(context.Calendar.TimeZoneId)).DateTime)!=request.MaturityDate)return Failure("ConflictingDefinition");
                underlying=c.UnderlyingContractId;
            }
            if(request.SeparateContractConnection&&(request.Options.Length!=1||request.IndependentChainConnection))return Failure("IndividualOptionRequired");
            var ordered=request.Options.OrderBy(x=>x.Pricing.Contract.ContractId,StringComparer.Ordinal).ToImmutableArray();
            var digest=PricingSemanticHash.Compute(new{request.MaturityDate,request.SeparateContractConnection,Options=ordered});
            if(scopes.TryGetValue(request.ScopeId,out var retiring)&&retiring.Retiring)
                await RemoveAsync(request.ScopeId,retiring,budget,linked.Token).ConfigureAwait(false);
            if(scopes.TryGetValue(request.ScopeId,out var existing))
            {
                if((existing.Key.Exact.Length!=0)!=request.SeparateContractConnection||(existing.Key.Independent.Length!=0)!=request.IndependentChainConnection)return Failure("ConflictingConnectionMode");
                if(existing.Leases.Count>=128&&!existing.Leases.ContainsKey(request.LeaseId))return Failure("LeaseCapacity");
                var prior=existing.Binding;
                if(prior.Digest!=digest)
                {
                    var conflict=request.ExpectedContextDigest!=prior.Digest?"Pricing context digest changed"
                        :!existing.Leases.ContainsKey(request.LeaseId)&&!existing.Owners.Any(x=>x.LeaseId==request.LeaseId)?"Lease does not own this scope"
                        :WorkerOptionChainRuntime.PhysicalDigest(prior.Options)!=WorkerOptionChainRuntime.PhysicalDigest(ordered)?"Physical contract definitions changed"
                        :ordered.Where((x,i)=>x.Pricing.MaximumQuoteAgeMilliseconds>prior.Options[i].Pricing.MaximumQuoteAgeMilliseconds
                            ||x.Pricing.MaximumQuoteSkewMilliseconds>prior.Options[i].Pricing.MaximumQuoteSkewMilliseconds).Any()?"Quote validity limits were relaxed":null;
                    if(conflict is not null)return new(false,new("ConflictingChainScope","Chain",request.ScopeId,
                        $"{conflict}; ScopeId={request.ScopeId}; LeaseId={request.LeaseId}; ExpectedDigest={request.ExpectedContextDigest}; ActualDigest={prior.Digest}; RequestedDigest={digest}"));
                    var prepared=Prepare(ordered,existing.Owners,request.SeparateContractConnection,at);
                    await runtime.RefreshOptionScopePricingAsync(request.ScopeId,generation,prior.Digest,digest,
                        prepared.Select((x,i)=>new PreparedOptionContextRefresh(prior.Prepared[i].Identity,x)).ToArray(),at,budget.Remaining,linked.Token).ConfigureAwait(false);
                    existing.Binding=new(ordered,prepared,digest);
                    PublishMonitoring();
                }
                if(request.ReconnectExactContract)
                {
                    if(!request.SeparateContractConnection||existing.Binding.Options.Length!=1)return Failure("ExactContractRepairRequired");
                    await runtime.ReleaseOptionScopeAsync(request.ScopeId,generation,budget.Remaining,linked.Token).ConfigureAwait(false);
                }
                if(request.ReconnectExactContract||runtime.ReadOptionScopeStatus(request.ScopeId).Phase!=3)
                {
                    var binding=existing.Binding;
                    await connection.StartAsync(runtime,request.ScopeId,WorkerOptionChainRuntime.PhysicalDigest(binding.Options),generation,resolveUnderlying(existing.Key.Underlying),
                        binding.Prepared.Select(x=>x with{RiskRequired=existing.Key.Exact.Length!=0||existing.Owners.Any(owner=>owner.ContractIds.Contains(x.Context.Contract.ContractId,StringComparer.Ordinal))}).ToArray(),
                        request.Statistics.IsDefault?[]:request.Statistics.ToArray(),binding.Digest,policy,production,at,replayStart(valueDate),budget.Remaining,linked.Token).ConfigureAwait(false);
                }
                if(!existing.Owners.Any(x=>x.LeaseId==request.LeaseId))existing.Leases[request.LeaseId]=request.LeaseExpiresAtUtc;
                return new(true,null,digest);
            }
            var key=new PhysicalKey(underlying!,request.MaturityDate,request.SeparateContractConnection?ordered[0].Pricing.Contract.ContractId:"",request.IndependentChainConnection?request.ScopeId:"");
            if(scopes.Values.Any(x=>x.Key==key))return new(false,new("ConflictingChainScope","Chain",request.ScopeId,
                $"Physical subscription already belongs to another scope; ScopeId={request.ScopeId}; LeaseId={request.LeaseId}; Underlying={underlying}; Maturity={request.MaturityDate}"));
            if(scopes.Count>=16||scopes.Values.Sum(x=>x.Binding.Options.Length)+ordered.Length>2048)return Failure("ChainCapacity");
            var under=resolveUnderlying(underlying!);
            if(under.Option||under.Instrument==0||under.Contract!=underlying)return Failure("UnderlyingQuoteUnavailable");
            var contexts=Prepare(ordered,[],request.SeparateContractConnection,at);
            await connection.StartAsync(runtime,request.ScopeId,WorkerOptionChainRuntime.PhysicalDigest(ordered),generation,under,contexts.ToArray(),
                request.Statistics.IsDefault?[]:request.Statistics.ToArray(),digest,policy,production,at,replayStart(valueDate),budget.Remaining,linked.Token).ConfigureAwait(false);
            linked.Token.ThrowIfCancellationRequested();scopes.Add(request.ScopeId,new(key,new(ordered,contexts,digest),request.LeaseId,request.LeaseExpiresAtUtc){Statistics=request.Statistics.IsDefault?[]:request.Statistics.ToArray()});PublishMonitoring();return new(true,null,digest);
        }
        catch(Exception error)when(error is not OperationCanceledException||!cancellationToken.IsCancellationRequested&&!stopping.IsCancellationRequested){fault(error);throw;}
        finally{gate.Release();}
    }
    /// <summary>Renews only an unchanged, unexpired, healthy discovery lease.
    /// This memory-only operation must not queue behind provider startup or reset.
    /// Changed definitions, repair requests and new owners use serialized control.
    /// Expiry removes a matching key/value pair so a concurrent renewal cannot be
    /// deleted using an expiry timestamp captured before that renewal.</summary>
    bool TryRenewHealthyLease(WorkerOptionChainRequest request,CancellationToken token,out WorkerOptionChainResult result)
    {
        result=default!;
        var now=clock.GetUtcNow();
        if(production&&!policy.IsProductionQualified||IsReplacing||request.GenerationId!=generation||request.ValueDate!=valueDate
            ||request.ReconnectExactContract||request.ExpectedContextDigest is not null
            ||request.LeaseExpiresAtUtc.Offset!=TimeSpan.Zero||request.LeaseExpiresAtUtc<=now||request.LeaseExpiresAtUtc>now.AddSeconds(120)
            ||string.IsNullOrWhiteSpace(request.ScopeId)||!Volatile.Read(ref renewalScopes).TryGetValue(request.ScopeId,out var scope)||scope.Retiring
            ||request.MaturityDate!=scope.Key.Expiry
            ||!scope.Leases.TryGetValue(request.LeaseId,out var expires)||expires<=now
            ||(scope.Key.Exact.Length!=0)!=request.SeparateContractConnection
            ||(scope.Key.Independent.Length!=0)!=request.IndependentChainConnection)return false;
        var binding=scope.Binding;
        if(request.Options.IsDefaultOrEmpty||!request.Options.OrderBy(x=>x.Pricing.Contract.ContractId,StringComparer.Ordinal).SequenceEqual(binding.Options)
            ||binding.Options.Any(x=>Black76PricingModel.ValidateContext(x.Pricing,now) is not null)
            ||runtime.ReadOptionScopeStatus(request.ScopeId).Phase!=3)return false;
        token.ThrowIfCancellationRequested();
        if(scope.Retiring||IsReplacing||request.GenerationId!=generation||!ReferenceEquals(scope.Binding,binding)
            ||!scope.Leases.TryUpdate(request.LeaseId,request.LeaseExpiresAtUtc,expires))return false;
        result=new(true,null,binding.Digest);
        return true;
    }
    ImmutableArray<PreparedOptionPricingContext> Prepare(ImmutableArray<WorkerOptionDefinition> options,ImmutableArray<WorkerOptionChainOwner> owners,bool exact,DateTimeOffset at)
    {
        return options.Select(x=>new PreparedOptionPricingContext(Guid.NewGuid(),x.Pricing,resolveUnderlying(x.Pricing.Contract.UnderlyingContractId).Instrument,x.Strike,x.IsCall,
            exact||owners.Any(owner=>owner.ContractIds.Contains(x.Pricing.Contract.ContractId,StringComparer.Ordinal)),qualifyUntil(x.Pricing,at))).ToImmutableArray();
    }
    /// <summary>Releases a discovery lease or installs the complete durable
    /// business-owner union. Durable owners do not expire with discovery TTL.</summary>
    public async Task<WorkerOptionChainResult> ReleaseAsync(WorkerOptionChainRelease release,TimeSpan remainingBudget,CancellationToken cancellationToken=default)
    {
        ArgumentNullException.ThrowIfNull(release);ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed)!=0,this);var budget=new Budget(remainingBudget);
        using var linked=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,stopping.Token);linked.CancelAfter(budget.Remaining);
        await EnterControlAsync(cancellationToken,linked.Token).ConfigureAwait(false);
        try
        {
            if(release.GenerationId!=generation)return Failure("Recovering");
            if(!scopes.TryGetValue(release.ScopeId,out var scope))return new(false,null);
            if(release.Ownership is {} ownership)
            {
                if(release.LeaseId!=Guid.Empty||ownership.ContractSetDigest!=WorkerOptionChainRuntime.PhysicalDigest(scope.Binding.Options)||ownership.Revision<0
                    ||string.IsNullOrWhiteSpace(ownership.AuthorityScope)||ownership.AuthorityScope.Length>128||ownership.Owners.IsDefault||ownership.Owners.Length>128
                    ||ownership.Owners.Any(x=>x is null||x.LeaseId==Guid.Empty||x.ContractIds.IsDefaultOrEmpty||x.ContractIds.Length>128
                        ||x.ContractIds.Distinct(StringComparer.Ordinal).Count()!=x.ContractIds.Length||x.ContractIds.Any(id=>!scope.Binding.Options.Any(option=>option.Pricing.Contract.ContractId==id)))
                    ||ownership.Owners.Select(x=>x.LeaseId).Distinct().Count()!=ownership.Owners.Length)return Failure("OwnershipInvalid");
                var hash=PricingSemanticHash.Compute(ownership);
                if(watermarks.TryGetValue(release.ScopeId,out var previous)&&(previous.Authority!=ownership.AuthorityScope||ownership.Revision<previous.Revision
                    ||ownership.Revision==previous.Revision&&hash!=previous.Digest))return Failure("OwnershipConflict");
                if(watermarks.Count>=4096&&!watermarks.ContainsKey(release.ScopeId))return Failure("OwnershipCapacity");
                if(scope.Authority is not null&&scope.Authority!=ownership.AuthorityScope||ownership.Revision<scope.Revision
                    ||ownership.Revision==scope.Revision&&hash!=scope.OwnershipDigest)return Failure("OwnershipConflict");
                if(Volatile.Read(ref suspended)==0)await runtime.SetOptionScopeRiskRequirementsAsync(release.ScopeId,generation,scope.Binding.Digest,scope.Binding.Prepared.Select(x=>new PreparedOptionRiskDemand(
                    x.Context.Contract.InstrumentId,x.Identity,scope.Key.Exact.Length!=0||ownership.Owners.Any(owner=>owner.ContractIds.Contains(x.Context.Contract.ContractId,StringComparer.Ordinal)))).ToArray(),budget.Remaining,linked.Token).ConfigureAwait(false);
                scope.Authority=ownership.AuthorityScope;scope.Revision=ownership.Revision;scope.OwnershipDigest=hash;scope.Owners=ownership.Owners;
                watermarks[release.ScopeId]=(ownership.AuthorityScope,ownership.Revision,hash);
                PublishMonitoring();
            }
            else if(scope.Leases.TryRemove(release.LeaseId,out _))ended.Add(release.LeaseId);
            if(scope.Leases.Count==0&&scope.Owners.IsEmpty)await RemoveAsync(release.ScopeId,scope,budget,linked.Token).ConfigureAwait(false);
            return new(scope.Leases.Count!=0||!scope.Owners.IsEmpty,null,release.Ownership is null?null:scope.Binding.Digest);
        }
        catch(Exception error)when(error is not OperationCanceledException||!cancellationToken.IsCancellationRequested&&!stopping.IsCancellationRequested){fault(error);throw;}
        finally{gate.Release();}
    }
    /// <inheritdoc/>
    public async Task<CompositionMarketPage> ReadAsync(CompositionSnapshotRequest request,string? continuation,CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed)!=0,this);
        var budget=new Budget(request.DeadlineUtc-clock.GetUtcNow()>TimeSpan.Zero?request.DeadlineUtc-clock.GetUtcNow():TimeSpan.Zero);
        using var linked=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,stopping.Token);linked.CancelAfter(budget.Remaining);
        await gate.WaitAsync(linked.Token).ConfigureAwait(false);
        try
        {
            await RemoveExpiredAsync(budget,linked.Token).ConfigureAwait(false);
            if(Volatile.Read(ref suspended)!=0||request.GenerationId!=generation||!scopes.TryGetValue(request.ScopeId,out var scope)||scope.Retiring||scope.Leases.Count==0&&scope.Owners.IsEmpty)throw new CompositionMarketSourceException("ChainUnavailable");
            var binding=scope.Binding;
            var source=new NativeOptionCompositionSource(runtime,request.ScopeId,binding.Digest,generation,binding.Prepared,clock,policy,
                ()=>Volatile.Read(ref suspended)==0&&!scope.Retiring&&ReferenceEquals(scope.Binding,binding));
            return await source.ReadAsync(request,continuation,linked.Token).ConfigureAwait(false);
        }
        finally{gate.Release();}
    }
    async Task RemoveAsync(string id,Scope scope,Budget budget,CancellationToken token)
    {
        scope.Retiring=true;PublishMonitoring();if(Volatile.Read(ref suspended)==0)await runtime.ReleaseOptionScopeAsync(id,generation,budget.Remaining,token).ConfigureAwait(false);scopes.Remove(id);replacementScopes?.Remove(id);
    }
    async Task EnterControlAsync(CancellationToken caller,CancellationToken bounded)
    {
        try{await gate.WaitAsync(bounded).ConfigureAwait(false);}
        catch(OperationCanceledException error)when(!caller.IsCancellationRequested&&!stopping.IsCancellationRequested)
        {fault(new TimeoutException("Native options original control deadline expired waiting for application admission.",error));throw;}
    }
    async Task RemoveExpiredAsync(Budget budget,CancellationToken token)
    {
        var at=clock.GetUtcNow();
        foreach(var (id,scope) in scopes.ToArray())
        {
            foreach(var lease in scope.Leases.Where(x=>x.Value<=at).ToArray()){if(((ICollection<KeyValuePair<Guid,DateTimeOffset>>)scope.Leases).Remove(lease))ended.Add(lease.Key);}
            if(scope.Leases.Count==0&&scope.Owners.IsEmpty)
            {
                if(replacementScopes?.Remove(id)==true){scopes.Remove(id);PublishMonitoring();}
                else await RemoveAsync(id,scope,budget,token).ConfigureAwait(false);
            }
        }
    }
    async Task ExpireAsync()
    {
        using var timer=new PeriodicTimer(TimeSpan.FromSeconds(1),clock);
        try
        {
            while(await timer.WaitForNextTickAsync(stopping.Token).ConfigureAwait(false))
            {
                var budget=new Budget(TimeSpan.FromMinutes(2));using var linked=CancellationTokenSource.CreateLinkedTokenSource(stopping.Token);linked.CancelAfter(budget.Remaining);
                await gate.WaitAsync(linked.Token).ConfigureAwait(false);
                try{await RemoveExpiredAsync(budget,linked.Token).ConfigureAwait(false);}finally{gate.Release();}
            }
        }
        catch(OperationCanceledException)when(stopping.IsCancellationRequested){}
        catch(Exception error){fault(error);throw;}
    }
    /// <summary>Cancels application admission before joining native owned scopes;
    /// native logical-owner emergency cancellation bypasses the application gate.</summary>
    public ValueTask DisposeAsync()=>StopAndDisposeAsync(TimeSpan.FromMinutes(2));
    /// <summary>Stops application ownership under the epoch's remaining original
    /// budget. Repeated callers share the first result and cannot renew its
    /// deadline. Native cancellation bypasses the application control gate.</summary>
    public ValueTask StopAndDisposeAsync(TimeSpan remainingBudget)
    {
        if(remainingBudget<TimeSpan.Zero)throw new ArgumentOutOfRangeException(nameof(remainingBudget));
        if(remainingBudget>TimeSpan.FromMinutes(2))remainingBudget=TimeSpan.FromMinutes(2);
        lock(disposalGate)return new(disposal??=DisposeCoreAsync(remainingBudget));
    }
    async Task DisposeCoreAsync(TimeSpan remainingBudget)
    {
        if(Interlocked.Exchange(ref disposed,1)!=0)return;
        var budget=new Budget(remainingBudget);
        // Cancellation bypasses the application/control queues even when no
        // time remains for joining them. Signaling is never cleanup proof.
        runtime.SignalOptionOwnerStop(runtime.ReadOptionOwnerGeneration());
        await stopping.CancelAsync().WaitAsync(budget.Remaining).ConfigureAwait(false);
        await runtime.StopOptionOwnerAsync(generation,budget.Remaining).ConfigureAwait(false);
        Exception? expiryFailure=null;
        try{await expiry.WaitAsync(budget.Remaining).ConfigureAwait(false);}
        catch(Exception error)when(expiry.IsCompleted){expiryFailure=error;}
        if(!await gate.WaitAsync(budget.Remaining).ConfigureAwait(false))throw new TimeoutException("Native options application gate did not join within its original cleanup budget.");
        try{scopes.Clear();PublishMonitoring();}finally{gate.Release();}
        stopping.Dispose();
        if(expiryFailure is not null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(expiryFailure).Throw();
    }
}
