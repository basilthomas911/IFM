using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Net;
using System.Net.Sockets;
using System.Text;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.MarketData.Rust.Api;
using TomasAI.IFM.Application.MarketData.Rust.OptionChains;
using TomasAI.IFM.Application.MarketData.Rust.Publication;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Shared.MarketData.Contracts.TickAggregation;
using TomasAI.IFM.Application.MarketData.Rust.Delivery;
using TomasAI.IFM.Framework.MarketData.DataBento.OptionChain;
using TomasAI.IFM.Framework.MarketData.ReferenceData;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.MarketData.Contracts;
using TomasAI.IFM.Shared.MarketData.Contracts.LastPrice;
using TomasAI.IFM.Shared.MarketData.Contracts.Pricing;
using TomasAI.IFM.Shared.MarketData.Definitions;
var library=Path.GetFullPath(args[0]);var wire=await File.ReadAllBytesAsync(args[1]);
var baseAt=new DateTimeOffset(2026,9,8,16,0,0,TimeSpan.Zero);var at=baseAt.AddSeconds(1);var budget=TimeSpan.FromSeconds(5);
var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();var port=(ushort)((IPEndPoint)listener.LocalEndpoint).Port;
using var stopping=new CancellationTokenSource();var peers=new ConcurrentBag<Task>();int accepted=0,closed=0,stallNext=0,maxWireLines=0;var closures=System.Threading.Channels.Channel.CreateBounded<int>(16);
var server=Task.Run(async()=>{try{while(true){var client=await listener.AcceptTcpClientAsync(stopping.Token);Interlocked.Increment(ref accepted);
    peers.Add(Task.Run(async()=>{using(client){try{var stream=client.GetStream();using var reader=new StreamReader(stream,Encoding.UTF8,false,1024,true);
        await stream.WriteAsync("lsg-test\ncram=t7kNhwj4xqR0QYjzFKtBEG2ec2pXJ4FK\n"u8.ToArray(),stopping.Token);
        if(await reader.ReadLineAsync(stopping.Token)is null)return;await stream.WriteAsync("success=1|session_id=5\n"u8.ToArray(),stopping.Token);
        string? line;int count=0;var completedSubscriptions=new HashSet<string>(StringComparer.Ordinal);
        while((line=await reader.ReadLineAsync(stopping.Token))is not null&&line!="start_session"){
            count++;var fields=line.Split('|').Select(x=>x.Split('=',2)).ToDictionary(x=>x[0],x=>x[1],StringComparer.Ordinal);
            if(fields["is_last"]=="1")completedSubscriptions.Add(fields["id"]);
        }
        Interlocked.Exchange(ref maxWireLines,Math.Max(count,Volatile.Read(ref maxWireLines)));
        if(line is null)return;if(!completedSubscriptions.SetEquals(["1","2","3"]))throw new InvalidDataException("Expected three complete logical subscriptions, including all SDK chunks.");
        if(Interlocked.Exchange(ref stallNext,0)==0)await stream.WriteAsync(wire,stopping.Token);if(await reader.ReadLineAsync(stopping.Token)is not null)throw new InvalidDataException("Expected provider cleanup EOF.");
    }catch(IOException){}finally{closures.Writer.TryWrite(Interlocked.Increment(ref closed));}}}));
}}catch(OperationCanceledException)when(stopping.IsCancellationRequested){}});
await using var runtime=new NativeMarketDataRuntime(library,2);
runtime.ConfigureLive(new(2026,9,8),Guid.NewGuid(),at,false,64);await runtime.PrepareOptionOwnerAsync(Guid.Empty,budget);
var generation=runtime.ReadOptionOwnerGeneration();
var calendar=new OptionPricingCalendar("fixture-calendar/v1","America/New_York",new(2026,1,1),new(2026,12,31),new(18,0),
    Enumerable.Range(0,365).Select(i=>new DateOnly(2026,1,1).AddDays(i)).Where(d=>d.DayOfWeek is not(DayOfWeek.Saturday or DayOfWeek.Sunday)).ToImmutableArray());
var conversion=new TreasuryRateConversionPolicy("FinancialModelingPrep","fixture:CMT",TreasuryRateConvention.UsTreasuryCmtNominalSemiannual,"test/v1","synthetic-evidence");
var curve=new TreasuryCurveSnapshot(new(2026,9,8),[new(TreasuryTenor.OneMonth,5m)],baseAt,"FinancialModelingPrep");
var contract=new OptionPricingConvention{SchemaVersion=3,ContractId="ES20261002C6000",Dataset="GLBX.MDP3",PublisherId=3,InstrumentId=43,
    RawSymbol="fixture-option",Root="ES",Exchange="XCME",Currency="USD",UnderlyingContractId="ES20261218",ExerciseStyle=OptionExerciseStyle.European,
    SettlementStyle=OptionSettlementStyle.DeliveryOfFuture,ExpirationUtc=baseAt.AddDays(24),LastTradingUtc=baseAt.AddDays(24),DayCount=PricingDayCount.Actual365Fixed,
    CalendarVersion=calendar.Version,Multiplier=50,TickSize=.25m,TickRuleVersion="fixture-tick/v1",DefinitionDigest=new('a',64),MappingVersion="fixture-mapping/v1",
    EvidenceId="synthetic-series",EffectiveFromUtc=baseAt.AddDays(-10),EffectiveUntilUtc=baseAt.AddDays(40),PremiumStyle=OptionPremiumStyle.PaidUpfront,
    UnderlyingKind=PricingUnderlyingKind.Futures,Strike=6000,Right=PricingOptionRight.Call,PremiumTickRule=OptionPremiumTickRule.Fixed};
var context=new OptionPricingContext(contract,calendar,TreasuryRateConversion.Convert(curve,TreasuryTenor.OneMonth,conversion).Value!,baseAt.AddHours(1),
    generation,Black76PricingModel.EngineFor(contract),1000,250,"fixture-publication/v1");

var clock=new MovingTime(at);var faults=new ConcurrentQueue<Exception>();
var refreshPolicy=new OptionPricingRefreshPolicy{PriceDeltaMilliseconds=10,FullRiskMilliseconds=20};
await using var leases=new NativeOptionChainRuntime(runtime,new NativeOptionConnection(new('x',32),"127.0.0.1",port,["127.0.0.1:1"]),generation,new(2026,9,8),clock,refreshPolicy,false,
    id=>new NativeRegistration(42,id,"42",false,null,false,true,3),(_,now)=>now.AddMinutes(1),date=>date==new DateOnly(2026,9,8)?null:throw new InvalidDataException("Replay used a local date."),faults.Enqueue);
var eventSink=new CapturedTickPublisher();var quoteDelivery=new NativeOptionQuoteDelivery(leases,new NativeOptionObservationPublisher(generation,eventSink,leases.IsMonitoredContract,clock));
using var directory=new NativeLastPriceDirectory(runtime,leases,new(2026,9,8),[(contract.UnderlyingContractId,MarketDataLane.FuturesGlbx)],2);
var stableFuture=directory.GetFuturesReader(contract.UnderlyingContractId,new(2026,9,8));
if(!ReferenceEquals(stableFuture,directory.GetFuturesReader(contract.UnderlyingContractId,new(2026,9,8))))throw new InvalidDataException("Futures reader identity was not stable.");
try{directory.GetFuturesReader(contract.UnderlyingContractId,new(2026,9,7));throw new InvalidDataException("Wrong date reader accepted.");}catch(ArgumentException){}
try{directory.GetFuturesOptionReader(contract.ContractId,new(2026,9,8));throw new InvalidDataException("Unowned option acquired a reader/subscription.");}catch(KeyNotFoundException){}
var first=Guid.NewGuid();var second=Guid.NewGuid();var business=Guid.NewGuid();
var optionDefinition=new WorkerOptionDefinition(context,6000,true);
var request=new WorkerOptionChainRequest("leases",first,generation,new(2026,9,8),new(2026,10,2),at.AddSeconds(30),[optionDefinition],
    Statistics:[new(contract.ContractId,12345,new(2026,9,8),222,new(2026,9,7))]);
var expectedDigest=PricingSemanticHash.Compute(new{request.MaturityDate,request.SeparateContractConnection,Options=request.Options});
async Task Closed(int total){using var timeout=new CancellationTokenSource(budget);while(Volatile.Read(ref closed)<total)await closures.Reader.ReadAsync(timeout.Token);}
try
{
    var owned=request with{SeparateContractConnection=true};
    if(!(await leases.AcquireAsync(owned,budget)).Active)throw new InvalidDataException("Initial owner failed.");
    var originalBinding=leases.MonitoredQuotes.Single();
    var reader=directory.GetFuturesOptionReader(contract.ContractId,request.ValueDate);
    var originalPhysical=runtime.ReadOptionScopeStatus(request.ScopeId).Generation;
    // Requalification failure happens after native cleanup. It must not turn
    // the temporarily empty monitoring view into successful/idle readiness.
    try
    {
        await leases.ReplaceOwnerAsync((_,_,_)=>Task.FromException<ImmutableArray<WorkerOptionDefinition>>(new IOException("fixture reference failure")),budget);
        throw new InvalidDataException("Reference failure was hidden.");
    }
    catch(IOException error)when(error.Message=="fixture reference failure"){}
    await Closed(1);
    if(!leases.IsReplacing||!leases.CaptureWorkloadEvidence().ReplacementPending||!leases.MonitoredQuotes.IsEmpty||originalBinding.IsCurrent()||reader.TryGetLastQuote(out _))
        throw new InvalidDataException("Incomplete replacement leaked old state or claimed readiness.");
    var intermediate=leases.Generation;
    if(intermediate==generation)throw new InvalidDataException("Native owner did not actually rotate.");
    // Simulate lost prepare acknowledgement: the native logical owner changes
    // without the facade committing its cached identity. Retry must reconcile
    // and join the actual owner, rather than permanently rejecting cached GUIDs.
    var unacknowledged=runtime.ReadOptionOwnerGeneration();
    await runtime.PrepareOptionOwnerAsync(unacknowledged,budget);
    if(runtime.ReadOptionOwnerGeneration()==unacknowledged||leases.Generation!=intermediate)
        throw new InvalidDataException("Lost-acknowledgement fixture did not diverge native/managed ownership.");
    var restored=await leases.ReplaceOwnerAsync((options,next,_)=>Task.FromResult(options.Select(x=>x with{Pricing=x.Pricing with{GenerationId=next}}).ToImmutableArray()),budget);
    var nextGeneration=leases.Generation;
    if(leases.IsReplacing||nextGeneration==intermediate||nextGeneration==generation||restored.Count!=1||restored[0].GenerationId!=nextGeneration)
        throw new InvalidDataException("Replacement failed to retain and restore original lease after partial failure.");
    using(var ready=new CancellationTokenSource(budget))while(!reader.TryGetLastQuoteWithGreeks(out _))await runtime.WaitForLiveChangesAsync(ready.Token);
    if(runtime.ReadOptionScopeStatus(request.ScopeId).Generation==originalPhysical||!reader.TryGetLastQuoteWithGreeks(out _))
        throw new InvalidDataException("Stable reader failed to follow a real new physical/pricing owner.");
    if((await leases.AcquireAsync(owned,budget)).Failure?.Code!="Recovering")throw new InvalidDataException("Old-generation request was admitted.");
    if(!(await leases.AcquireAsync(restored[0],budget)).Active)throw new InvalidDataException("Restored lease could not renew.");
    var cycles=args.Length>2?int.Parse(args[2],System.Globalization.CultureInfo.InvariantCulture):1;
    if(cycles is <1 or >10000)throw new ArgumentOutOfRangeException(nameof(cycles));
    var durations=new List<double>();var diagnostics=0;
    long warmPrivate=0,warmManaged=0,warmAllocated=0;
    using var measuredProcess=System.Diagnostics.Process.GetCurrentProcess();
    void ConsumeDiagnostics(){LiveDiagnostic[] batch;do{batch=runtime.DrainLiveDiagnostics();diagnostics+=batch.Length;}while(batch.Length==64);}
    ConsumeDiagnostics();
    for(var cycle=1;cycle<cycles;cycle++)
    {
        var previous=nextGeneration;var physical=runtime.ReadOptionScopeStatus(request.ScopeId).Generation;
        var start=System.Diagnostics.Stopwatch.GetTimestamp();
        try{restored=await leases.ReplaceOwnerAsync((options,next,_)=>Task.FromResult(options.Select(x=>x with{Pricing=x.Pricing with{GenerationId=next}}).ToImmutableArray()),budget);}
        catch(Exception error){throw new InvalidOperationException($"Cycle={cycle}; accepted={accepted}; closed={closed}; owner={runtime.ReadOptionScopeFailure()}; scope={runtime.ReadOptionScopeStatus(request.ScopeId)}; diagnostics={string.Join(";",runtime.DrainLiveDiagnostics().Select(x=>x.Message))}",error);}
        nextGeneration=leases.Generation;
        if(nextGeneration==previous||restored.Single().GenerationId!=nextGeneration||runtime.ReadOptionScopeStatus(request.ScopeId).Generation==physical)
            throw new InvalidDataException("A repeated reset reused a retired owner.");
        using(var ready=new CancellationTokenSource(budget))while(!reader.TryGetLastQuoteWithGreeks(out _))await runtime.WaitForLiveChangesAsync(ready.Token);
        await Closed(cycle+1);
        if(accepted!=closed+1)throw new InvalidDataException("A reset retained more than its one active socket.");
        ConsumeDiagnostics();
        durations.Add(System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalMilliseconds);
        if(cycle==100)
        {
            // Qualification-only full collection separates retained state from
            // ordinary allocation churn; production reset never forces a GC.
            warmManaged=GC.GetTotalMemory(true);measuredProcess.Refresh();warmPrivate=measuredProcess.PrivateMemorySize64;
            warmAllocated=GC.GetTotalAllocatedBytes();
        }
    }
    if(durations.Count>0)
    {
        durations.Sort();
        Console.WriteLine($"Owner replacement cycles={cycles}; p50_ms={durations[durations.Count/2]:F3}; p99_ms={durations[(int)((durations.Count-1)*.99)]:F3}; max_ms={durations[^1]:F3}; active_sockets={accepted-closed}; consumed_diagnostics={diagnostics}");
        if(warmPrivate!=0)
        {
            var managed=GC.GetTotalMemory(true);measuredProcess.Refresh();
            Console.WriteLine($"Post-warmup memory: warm_private_bytes={warmPrivate}; final_private_bytes={measuredProcess.PrivateMemorySize64}; warm_managed_retained_bytes={warmManaged}; final_managed_retained_bytes={managed}; managed_allocated_bytes={GC.GetTotalAllocatedBytes()-warmAllocated}. Process-private bytes include CLR/native allocators and fixture ownership; this measurement alone is not a leak proof.");
        }
    }
    runtime.SignalOptionOwnerStop(generation); // stale: must not touch replacement
    if(runtime.ReadOptionScopeStatus(request.ScopeId).Phase!=3)throw new InvalidDataException("A retired owner stopped the current option scope.");
    leases.SuspendMonitoring();
    var stopStarted=System.Diagnostics.Stopwatch.GetTimestamp();runtime.SignalOptionOwnerStop(nextGeneration);
    if(System.Diagnostics.Stopwatch.GetElapsedTime(stopStarted)>TimeSpan.FromSeconds(1))throw new InvalidDataException("Emergency stop waited for an asynchronous control deadline.");
    await Closed(cycles+1);
    if(reader.TryGetLastQuote(out _))throw new InvalidDataException("Terminal stop exposed cached option data.");
    restored=await leases.ReplaceOwnerAsync((options,next,_)=>Task.FromResult(options.Select(x=>x with{Pricing=x.Pricing with{GenerationId=next}}).ToImmutableArray()),budget);
    nextGeneration=leases.Generation;
    if(leases.IsReplacing||restored.Count!=1)throw new InvalidDataException("A stopped owner could not be explicitly replaced.");
    leases.SuspendMonitoring();runtime.SignalOptionOwnerStop(nextGeneration);
    await Closed(cycles+2);
    var releasedWhileStopped=await leases.ReleaseAsync(new(request.ScopeId,request.LeaseId,nextGeneration),budget);
    if(releasedWhileStopped.Active||releasedWhileStopped.Failure is not null)throw new InvalidDataException("Explicit release could not retire a stopped owner's intent.");
    var empty=await leases.ReplaceOwnerAsync((_,_,_)=>throw new InvalidOperationException("Released intent must not be requalified or resurrected."),budget);
    if(empty.Count!=0||!leases.ActiveScopes.IsEmpty)throw new InvalidDataException("Stopped-and-released intent resurrected on recovery.");

    await Closed(cycles+2);
    if(reader.TryGetLastQuote(out _))throw new InvalidDataException("Released replacement reader retained data.");
    await leases.DisposeAsync();await runtime.StopAndDisposeAsync(budget);
    await Task.WhenAll(peers).WaitAsync(budget);
    if(accepted!=closed)throw new InvalidDataException("Owner replacement retained provider sockets.");
    Console.WriteLine("PASS whole native option owner replacement: actual old socket joined; new logical/physical identities; failed requalification retains restore intent and unavailable readers; retry restores lease and qualified pricing; old generation rejected; stable reader follows replacement; all sockets joined.");
}
finally{stopping.Cancel();listener.Stop();await server;try{await Task.WhenAll(peers);}catch(OperationCanceledException)when(stopping.IsCancellationRequested){}}
sealed class MovingTime(DateTimeOffset at):TimeProvider {
    long ticks=at.UtcTicks;
    public override DateTimeOffset GetUtcNow()=>new(Interlocked.Read(ref ticks),TimeSpan.Zero);
    public void Set(DateTimeOffset value)=>Interlocked.Exchange(ref ticks,value.UtcTicks);
}
sealed class CapturedRoutes(string contract):ITickLiveRouter{
    public List<LiveTickQuoteServiceEvent> Quotes=[];public List<LiveTickTradeServiceEvent> Trades=[];
    public bool Activate(string id)=>true;public bool Deactivate(string id)=>true;public bool IsActive(string id)=>id==contract;public void Clear(){}
    public ValueTask RouteAsync(LiveTickQuoteServiceEvent value){Quotes.Add(value);return ValueTask.CompletedTask;}
    public ValueTask RouteAsync(LiveTickTradeServiceEvent value){Trades.Add(value);return ValueTask.CompletedTask;}
}


sealed class CapturedTickPublisher:ITickAggregationEventPublisher {
    public List<FuturesTickTradeDataChangedEvent> Events=[];
    public TaskCompletionSource? Hold = null;
    public bool IsRunning=>true;
    public ValueTask StartAsync()=>ValueTask.CompletedTask;
    public ValueTask StopAsync()=>ValueTask.CompletedTask;
    public ValueTask DisposeAsync()=>ValueTask.CompletedTask;
    public ValueTask PublishAsync(FuturesMarketPriceUpdatedRealtimeEvent value)=>ValueTask.CompletedTask;
    public ValueTask PublishAsync(FuturesTickTradeDataChangedEvent value){Events.Add(value);return Hold is null?ValueTask.CompletedTask:new(Hold.Task);}
    public ValueTask PublishAsync(FuturesTickQuoteDataChangedEvent value,ITickQuoteBufferLease lease)=>ValueTask.CompletedTask;
}


