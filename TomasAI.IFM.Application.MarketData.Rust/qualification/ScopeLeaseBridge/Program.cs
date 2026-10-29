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
void Equal<T>(T expected,T actual,string stage){if(!MessagePackBinarySerializer.Shared.Serialize(expected)!.SequenceEqual(MessagePackBinarySerializer.Shared.Serialize(actual)!))throw new InvalidDataException($"{stage}: expected {expected}; actual {actual}");}
async Task Closed(int total){using var timeout=new CancellationTokenSource(budget);while(Volatile.Read(ref closed)<total)await closures.Reader.ReadAsync(timeout.Token);}
try{
    using(var cancelled=new CancellationTokenSource()){
        cancelled.Cancel();try{await leases.AcquireAsync(request,budget,cancelled.Token);throw new InvalidDataException("Cancelled lease acquired provider ownership.");}catch(OperationCanceledException){}
    }
    if(Volatile.Read(ref accepted)!=0||!faults.IsEmpty)throw new InvalidDataException("Expected caller cancellation opened a socket or entered recovery.");
    if((await leases.AcquireAsync(request with{ValueDate=new(2026,9,7)},budget)).Failure?.Code!="Recovering")throw new InvalidDataException("Wrong API ValueDate accepted.");
    if((await leases.AcquireAsync(request with{LeaseExpiresAtUtc=at},budget)).Failure?.Code!="InvalidChainRequest")throw new InvalidDataException("Expired discovery request accepted.");
    Interlocked.Exchange(ref stallNext,1);
    var stalled=request with{ScopeId="stalled",LeaseId=Guid.NewGuid()};
    var started=System.Diagnostics.Stopwatch.GetTimestamp();var startupFailed=false;
    var pendingAcquisition=leases.AcquireAsync(stalled,TimeSpan.FromSeconds(1));
    if(!leases.CaptureWorkloadEvidence().ReplacementPending)
        throw new InvalidDataException("Busy first acquisition was falsely reported as an idle native workload.");
    try{await pendingAcquisition;}
    catch(Exception error)when(error is OperationCanceledException or TimeoutException or InvalidOperationException){startupFailed=true;}
    if(!startupFailed||faults.Count!=1||System.Diagnostics.Stopwatch.GetElapsedTime(started)>TimeSpan.FromSeconds(3))throw new InvalidDataException("Stalled startup did not fail/report under its original remaining budget.");
    if(Volatile.Read(ref accepted)!=1)throw new InvalidDataException("Stalled startup fixture was not actually reached; accepted="+accepted);
    await Closed(1);faults.TryDequeue(out _);
    Equal(new WorkerOptionChainResult(true,null,expectedDigest),await leases.AcquireAsync(stalled,budget),"fresh acquisition joins failed partial scope");
    Equal(new WorkerOptionChainResult(false,null),await leases.ReleaseAsync(new(stalled.ScopeId,stalled.LeaseId,generation),budget),"failed/retried scope final release");
    await Closed(2);
    Equal(new WorkerOptionChainResult(true,null,expectedDigest),await leases.AcquireAsync(request,budget),"initial native lease");
    var connections=Volatile.Read(ref accepted);
    Equal(new WorkerOptionChainResult(true,null,expectedDigest),await leases.AcquireAsync(request with{LeaseExpiresAtUtc=at.AddSeconds(40)},budget),"idempotent renewal");
    // Reproduce the live failure deterministically: unrelated provider control
    // holds admission longer than the legacy three-second renewal allowance.
    // An unchanged healthy lease must renew without joining that wait at all.
    var control=(SemaphoreSlim)typeof(NativeOptionChainRuntime).GetField("gate",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.GetValue(leases)!;
    await control.WaitAsync();
    try
    {
        using var renewalDeadline=new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        Equal(new WorkerOptionChainResult(true,null,expectedDigest),await leases.AcquireAsync(request with{LeaseExpiresAtUtc=at.AddSeconds(45)},budget,renewalDeadline.Token),"healthy renewal bypasses busy provider control");
        using var canceledRenewal=new CancellationTokenSource();canceledRenewal.Cancel();
        try{await leases.AcquireAsync(request,budget,canceledRenewal.Token);throw new InvalidDataException("Canceled healthy renewal was admitted.");}catch(OperationCanceledException){}
        using var newOwnerDeadline=new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        try{await leases.AcquireAsync(request with{LeaseId=Guid.NewGuid()},budget,newOwnerDeadline.Token);throw new InvalidDataException("An unknown lease bypassed serialized provider control.");}catch(OperationCanceledException){}
        using var changedDefinitionDeadline=new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        try{await leases.AcquireAsync(request with{MaturityDate=request.MaturityDate.AddDays(1)},budget,changedDefinitionDeadline.Token);throw new InvalidDataException("A changed scope definition bypassed serialized provider control.");}catch(OperationCanceledException){}
        if(!faults.IsEmpty)throw new InvalidDataException("Healthy renewal contention entered recovery.");
    }
    finally{control.Release();}
    Console.WriteLine("PASS renewal control contention: healthy lease renews under a 200ms caller deadline with the provider gate held; cancellation, unknown owner and changed definitions cannot bypass control; no recovery fault or extra socket.");
    Equal(new WorkerOptionChainResult(true,null,expectedDigest),await leases.AcquireAsync(request with{LeaseId=second},budget),"shared discovery owner");
    if(Volatile.Read(ref accepted)!=connections)throw new InvalidDataException("Shared/renewed lease opened another socket.");
    if(leases.MonitoredQuotes.Length!=0||await quoteDelivery.PublishAvailableAsync(budget)!=0)throw new InvalidDataException("Unowned discovery quotes entered position monitoring.");
    var capture=new CompositionSnapshotRequest(Guid.NewGuid(),request.ScopeId,"Daily",generation,at,at.AddSeconds(4),true){AllowMissingOptionQuotes=true,SelectionOnly=true};
    using(var timeout=new CancellationTokenSource(budget)){
        while((await leases.ReadAsync(capture,null,timeout.Token)).Instruments[0].Selection is null)await runtime.WaitForLiveChangesAsync(timeout.Token);
    }
    var page=await leases.ReadAsync(capture,null,default);
    if(page.ScopeToken!=expectedDigest||page.Instruments.Length!=1||page.Instruments[0].SessionVolume!=12345||page.Instruments[0].OpenInterest!=222)throw new InvalidDataException("Leased composition omitted identity or saved statistics.");
    var collision=await leases.AcquireAsync(request with{ScopeId="collision",LeaseId=Guid.NewGuid()},budget);
    if(collision.Failure?.Code!="ConflictingChainScope")throw new InvalidDataException("Physical scope collision was not refused.");
    var physical=WorkerOptionChainRuntime.PhysicalDigest(request.Options);
    var ownership=new WorkerOptionChainOwnership(1,"trusted-parent",[new(business,[contract.ContractId])],physical);
    Equal(new WorkerOptionChainResult(true,null,expectedDigest),await leases.ReleaseAsync(new(request.ScopeId,Guid.Empty,generation,ownership),budget),"business ownership union");
    if(leases.MonitoredQuotes.Length!=1)throw new InvalidDataException("Business ownership did not publish monitoring authority.");
    var stableOption=directory.GetFuturesOptionReader(contract.ContractId,new(2026,9,8));
    try{directory.GetFuturesReader(contract.ContractId,new(2026,9,8));throw new InvalidDataException("An option was returned as a futures reader.");}catch(InvalidOperationException){}
    try{directory.GetFuturesOptionReader(contract.UnderlyingContractId,new(2026,9,8));throw new InvalidDataException("A future was returned as an option reader.");}catch(InvalidOperationException){}
    if(!ReferenceEquals(stableOption,directory.GetFuturesOptionReader(contract.ContractId,new(2026,9,8)))||directory.Count!=2)
        throw new InvalidDataException("Option reader identity/capacity differs from the date-scoped provider.");
    using(var timeout=new CancellationTokenSource(budget)){
        while(true){await quoteDelivery.PublishAvailableAsync(budget,timeout.Token);if(eventSink.Events.LastOrDefault()?.OptionMarketPriceObservation?.OptionTickData.GreeksAvailable==true)break;await runtime.WaitForLiveChangesAsync(timeout.Token);}
    }
    var delivered=eventSink.Events[^1];
    if(delivered.OptionMarketPriceObservation?.OptionTickData.OptionPrice!=100)throw new InvalidDataException("Native monitoring quote mark differs from exact midpoint.");
    using var tradeSource=new NativeOptionTradeSourceReader(runtime,generation,new(2026,9,8));
    var evidenceWriter=new CapturedEvidence();var tradeSink=new CapturedTickPublisher();
    var transient=new CapturedRoutes(contract.ContractId);
    var tradeDelivery=new NativeOptionTradeDelivery(tradeSource,new(runtime),leases,evidenceWriter,tradeSink,transient);
    if(await tradeDelivery.DrainAvailableAsync(request.ScopeId,budget)==0||evidenceWriter.Calls!=1||tradeSink.Events.Count!=1)
        throw new InvalidDataException("Latest-view delivery consumed or waited for the independent actual-trade drain.");
    Equal(evidenceWriter.Value!.ToRealtimeEvent(),tradeSink.Events[0],"acknowledged exact trade / existing actor event mapping");
    if(transient.Quotes.Count!=1||transient.Trades.Count!=1)throw new InvalidDataException("Activated native option route omitted exact raw quote/trade delivery");
    var rawQuote=transient.Quotes[0];var rawTrade=transient.Trades[0];var baseNs=baseAt.ToUnixTimeMilliseconds()*1_000_000;
    Equal(new FuturesTickQuoteData(10,baseNs+1800,baseNs+1900,0,99_750_000_000,99.75m,10,7,100_250_000_000,100.25m,10,8),rawQuote.Quote,"exact native option quote route");
    if(rawTrade.Trade.EventTimestampNanoseconds!=evidenceWriter.Value.Source.EventNanoseconds||rawTrade.Trade.ReceiveTimestampNanoseconds!=evidenceWriter.Value.Source.ReceiveNanoseconds
        ||rawTrade.Trade.SourceSequence!=evidenceWriter.Value.Source.Sequence||rawTrade.Trade.Price!=evidenceWriter.Value.Source.Price
        ||rawQuote.ContractId!=contract.ContractId||rawQuote.InstrumentId!=43||rawQuote.PublisherId!=3||rawQuote.ValueDate!=request.ValueDate)
        throw new InvalidDataException("Transient option delivery changed source time/identity or acknowledged trade price");
    if(!leases.MonitoredQuotes[0].Reader.TryGetLastTradeWithGreeks(out var acknowledgedTrade)||!acknowledgedTrade.Greeks.IsValid)
        throw new InvalidDataException("Durably acknowledged native trade did not reach the stable reader.");
    if(!stableOption.TryGetLastTradeWithGreeks(out var stableTrade)||stableTrade!=acknowledgedTrade)
        throw new InvalidDataException("Epoch reader omitted durably acknowledged source evidence.");
    var sharedAgain=Guid.NewGuid();var acknowledgedBinding=leases.FindMonitoredQuote(contract.ContractId);
    await leases.AcquireAsync(request with{LeaseId=sharedAgain},budget);
    await leases.ReleaseAsync(new(request.ScopeId,sharedAgain,generation),budget);
    if(!ReferenceEquals(acknowledgedBinding,leases.FindMonitoredQuote(contract.ContractId))||!stableOption.TryGetLastTradeWithGreeks(out var afterOwnership)||afterOwnership!=acknowledgedTrade)
        throw new InvalidDataException("An ownership-only transition discarded acknowledged trade evidence or replaced its unchanged reader.");
    if(await tradeDelivery.DrainAvailableAsync(request.ScopeId,budget)!=0||evidenceWriter.Calls!=1||tradeSink.Events.Count!=1)
        throw new InvalidDataException("Drained actual trade was duplicated.");
    if(await quoteDelivery.PublishAvailableAsync(budget)!=0)throw new InvalidDataException("Unchanged native cache read manufactured a new publication.");
    var stalledSink=new CapturedTickPublisher{Hold=new(TaskCreationOptions.RunContinuationsAsynchronously)};
    var boundedDelivery=new NativeOptionQuoteDelivery(leases,new NativeOptionObservationPublisher(generation,stalledSink,leases.IsMonitoredContract,clock));
    var publicationStarted=System.Diagnostics.Stopwatch.GetTimestamp();
    try{await boundedDelivery.PublishAvailableAsync(TimeSpan.FromMilliseconds(100));throw new InvalidDataException("Stalled publication completed without acknowledgment.");}catch(OperationCanceledException){}
    if(System.Diagnostics.Stopwatch.GetElapsedTime(publicationStarted)>TimeSpan.FromSeconds(1)||stalledSink.Events.Count!=1)throw new InvalidDataException("Publication did not use its original caller budget.");
    try{await boundedDelivery.PublishAvailableAsync(budget);throw new InvalidDataException("Unjoined publisher was retried into another detached task.");}catch(InvalidOperationException){}
    if(stalledSink.Events.Count!=1)throw new InvalidDataException("Stalled callback retry leaked another publication.");
    stalledSink.Hold!.SetResult();stalledSink.Hold=null;
    if(await boundedDelivery.PublishAvailableAsync(budget)!=1||stalledSink.Events.Count!=2)throw new InvalidDataException("Joined publisher did not resume from the latest native view.");
    var oldMonitoring=leases.MonitoredQuotes[0];
    Equal(new WorkerOptionChainResult(true,null),await leases.ReleaseAsync(new(request.ScopeId,first,generation),budget),"discovery release preserves business owner");
    Equal(new WorkerOptionChainResult(true,null),await leases.ReleaseAsync(new(request.ScopeId,second,generation),budget),"last temporary owner does not close business scope");
    if((await leases.AcquireAsync(request,budget)).Failure?.Code!="LeaseEnded")throw new InvalidDataException("Ended discovery lease revived.");
    if((await leases.ReleaseAsync(new(request.ScopeId,Guid.Empty,generation,ownership with{Revision=0}),budget)).Failure?.Code!="OwnershipConflict")throw new InvalidDataException("Ownership revision regressed.");
    var newContext=context with{Rate=TreasuryRateConversion.Convert(curve with{Rates=[new(TreasuryTenor.OneMonth,5.25m)]},TreasuryTenor.OneMonth,conversion).Value!};
    var refreshed=request with{LeaseId=business,ExpectedContextDigest=expectedDigest,Options=[optionDefinition with{Pricing=newContext}]};
    var updated=await leases.AcquireAsync(refreshed,budget);
    if(!updated.Active||updated.Failure is not null||updated.ContextDigest==expectedDigest)throw new InvalidDataException("Owned context CAS did not update semantic identity.");
    if(oldMonitoring.IsCurrent())throw new InvalidDataException("Old context monitoring binding survived refresh.");
    using(var timeout=new CancellationTokenSource(budget)){
        while(true){await quoteDelivery.PublishAvailableAsync(budget,timeout.Token);if(eventSink.Events.LastOrDefault()?.OptionMarketPriceObservation?.OptionTickData.PricingInputs?.AnnualContinuousRate==newContext.Rate.AnnualContinuousRate
            &&stableOption.TryGetLastQuoteWithGreeks(out var ready)&&ready.Greeks.RiskFreeRate==newContext.Rate.AnnualContinuousRate)break;await runtime.WaitForLiveChangesAsync(timeout.Token);}
    }
    if(!stableOption.TryGetLastQuoteWithGreeks(out var refreshedQuote)||refreshedQuote.Greeks.RiskFreeRate!=newContext.Rate.AnnualContinuousRate)
        throw new InvalidDataException($"An issued epoch reader kept the retired reference context: {refreshedQuote.Greeks}; expected={newContext.Rate.AnnualContinuousRate:R}; binding={leases.FindMonitoredQuote(contract.ContractId)?.Prepared.Identity}");
    if((await leases.AcquireAsync(request with{LeaseId=business,ExpectedContextDigest=expectedDigest},budget)).Failure?.Code!="ConflictingChainScope")throw new InvalidDataException("Stale context CAS was accepted.");
    if(Volatile.Read(ref accepted)!=connections)throw new InvalidDataException("Ownership or reference refresh reconnected transport.");
    await runtime.ReleaseOptionScopeAsync(request.ScopeId,generation,budget);
    await Closed(connections);
    if(stableOption.TryGetLastQuote(out _)||stableOption.TryGetLastTradeWithGreeks(out _))throw new InvalidDataException("Stopped physical scope remained readable.");
    Equal(new WorkerOptionChainResult(true,null,updated.ContextDigest),await leases.AcquireAsync(refreshed,budget),"retained owner repairs stopped native scope");
    connections++;
    using(var timeout=new CancellationTokenSource(budget)){
        var revision=eventSink.Events[^1].OptionMarketPriceObservation!.OptionTickData.ObservationRevision;
        while(true){await quoteDelivery.PublishAvailableAsync(budget,timeout.Token);if(eventSink.Events[^1].OptionMarketPriceObservation!.OptionTickData.ObservationRevision>revision)break;await runtime.WaitForLiveChangesAsync(timeout.Token);}
    }
    if(Volatile.Read(ref accepted)!=connections)throw new InvalidDataException("Stopped native scope was not actually restarted.");
    if(!stableOption.TryGetLastQuote(out _))throw new InvalidDataException("An issued epoch reader did not follow physical replacement.");
    var stalledEvidence=new CapturedEvidence{Hold=new(TaskCreationOptions.RunContinuationsAsynchronously)};
    var silentTrades=new CapturedTickPublisher();
    var stalledTradeDelivery=new NativeOptionTradeDelivery(tradeSource,new(runtime),leases,stalledEvidence,silentTrades);
    try{await stalledTradeDelivery.DrainAvailableAsync(request.ScopeId,TimeSpan.FromMilliseconds(100));throw new InvalidDataException("Unacknowledged trade passed its original deadline.");}catch(OperationCanceledException){}
    if(stalledEvidence.Calls!=1||silentTrades.Events.Count!=0)throw new InvalidDataException("Trade published before durable acknowledgment.");
    try{await stalledTradeDelivery.DrainAvailableAsync(request.ScopeId,budget);throw new InvalidDataException("Unjoined evidence callback was retried.");}catch(InvalidOperationException){}
    if(stalledEvidence.Calls!=1||silentTrades.Events.Count!=0)throw new InvalidDataException("Unjoined writer spawned another callback or event.");
    stalledEvidence.Hold!.SetResult();
    await stalledEvidence.Exited.Task.WaitAsync(budget);
    if(silentTrades.Events.Count!=0)throw new InvalidDataException("Late canceled evidence produced an actor event.");

    var physicalBeforeReset=runtime.ReadOptionScopeStatus(request.ScopeId).Generation;
    await leases.ResetAsync(budget);
    connections++;
    if(runtime.ReadOptionScopeStatus(request.ScopeId) is not {Phase:3} resetState||resetState.Generation==physicalBeforeReset
        ||leases.ActiveScopes.Length!=1||leases.MonitoredQuotes.Length!=1)
        throw new InvalidDataException("Native recovery did not replace physical ownership while retaining logical monitoring leases.");
    if(stableOption.TryGetLastTradeWithGreeks(out _))
        throw new InvalidDataException("Reset exposed acknowledged trade enrichment from the retired physical connection.");

    Equal(new WorkerOptionChainResult(false,null,updated.ContextDigest),await leases.ReleaseAsync(new(request.ScopeId,Guid.Empty,generation,ownership with{Revision=2,Owners=[]}),budget),"last business owner joined release");
    await Closed(connections);
    if(leases.MonitoredQuotes.Length!=0||await quoteDelivery.PublishAvailableAsync(budget)!=0)throw new InvalidDataException("Released business ownership still published monitoring values.");
    if(stableOption.TryGetLastQuote(out _)||stableOption.TryGetLastTrade(out _)||stableOption.TryGetLastQuoteWithGreeks(out _)||stableOption.TryGetLastTradeWithGreeks(out _))
        throw new InvalidDataException("Released monitoring authority leaked values through an issued epoch reader.");
    var fresh=request with{LeaseId=Guid.NewGuid(),LeaseExpiresAtUtc=at.AddSeconds(10)};
    Equal(new WorkerOptionChainResult(true,null,expectedDigest),await leases.AcquireAsync(fresh,budget),"fresh owner after joined release");
    Equal(new WorkerOptionChainResult(true,null),await leases.ReleaseAsync(new(request.ScopeId,first,generation),budget),"late old lease cannot release replacement");
    clock.Set(at.AddSeconds(11));runtime.UpdateLiveClock(clock.GetUtcNow(),false);
    await Closed(connections+1);
    if((await leases.AcquireAsync(fresh with{LeaseExpiresAtUtc=clock.GetUtcNow().AddSeconds(10)},budget)).Failure?.Code!="LeaseEnded")throw new InvalidDataException("Expired lease revived.");
    var bulkAt=clock.GetUtcNow();
    var capacityCalendar=calendar with{Version="capacity-calendar/v1",CoverageFrom=new(2026,9,8),CoverageUntil=new(2026,10,2),
        TradingDates=calendar.TradingDates.Where(d=>d>=new DateOnly(2026,9,8)&&d<=new DateOnly(2026,10,2)).ToImmutableArray()};
    var bulk=Enumerable.Range(0,2048).Select(i=>new PreparedOptionPricingContext(Guid.NewGuid(),context with{Calendar=capacityCalendar,Contract=contract with{
        CalendarVersion=capacityCalendar.Version,ContractId=$"ES20261002C{6000+i}",InstrumentId=(uint)(43+i),RawSymbol=$"capacity-{i}",Strike=6000+i}},42,6000+i,true,false,bulkAt.AddSeconds(30))).ToArray();
    var bulkRegistrations=bulk.Select(x=>new NativeRegistration(x.Context.Contract.InstrumentId,x.Context.Contract.ContractId,x.Context.Contract.InstrumentId.ToString(System.Globalization.CultureInfo.InvariantCulture),true,null,false,false,x.Context.Contract.PublisherId))
        .Prepend(new NativeRegistration(42,contract.UnderlyingContractId,"42",false,null,false,true,3)).ToArray();
    var allSymbols=bulkRegistrations.Select(x=>x.Source).ToArray();
    NativeSubscription[] bulkSubscriptions=[new(allSymbols,"trades","instrument_id"),new(allSymbols,"mbp-1","instrument_id"),new(bulkRegistrations.Skip(1).Select(x=>x.Source).ToArray(),"statistics","instrument_id")];
    await runtime.StartPricedOptionScopeAsync("capacity",new('f',64),generation,new('x',32),"127.0.0.1",port,["127.0.0.1:1"],bulkSubscriptions,bulkRegistrations,true,
        new('e',64),bulk,refreshPolicy,false,bulkAt,budget);
    if(runtime.ReadOptionScopeState("capacity",bulk[^1].Context.Contract.InstrumentId,3).Source.Instrument!=2090)throw new InvalidDataException("Full 2048-option capacity lost its final registration.");
    if(Volatile.Read(ref maxWireLines)<=3)throw new InvalidDataException("Full-chain test did not reach SDK subscription chunking.");
    var maximumConnections=Volatile.Read(ref accepted);
    try{await runtime.StartPricedOptionScopeAsync("overflow",new('f',64),generation,new('x',32),"127.0.0.1",port,["127.0.0.1:1"],
        [new(["42","43"],"trades","instrument_id"),new(["42","43"],"mbp-1","instrument_id"),new(["43"],"statistics","instrument_id")],bulkRegistrations.Take(2).ToArray(),true,
        new('e',64),[bulk[0]],refreshPolicy,false,bulkAt,budget);throw new InvalidDataException("Global option capacity overflow was accepted.");}catch(InvalidOperationException){}
    if(Volatile.Read(ref accepted)!=maximumConnections||runtime.ReadOptionScopeStatus("capacity").Phase!=3)throw new InvalidDataException("Capacity refusal opened a socket or disturbed the owned full chain.");
    await runtime.ReleaseOptionScopeAsync("capacity",generation,budget);await Closed(maximumConnections);
    if(!faults.IsEmpty)throw new AggregateException(faults);
    await leases.DisposeAsync();await leases.DisposeAsync();await runtime.StopAndDisposeAsync(budget);await Task.WhenAll(peers).WaitAsync(budget);
    directory.Dispose();directory.Dispose();
    if(directory.IsActive||stableFuture.TryGetLastQuote(out _)||stableFuture.TryGetLastTrade(out _)||stableOption.TryGetLastQuote(out _))throw new InvalidDataException("Retired epoch reader remained active.");
    try{directory.GetFuturesOptionReader(contract.ContractId,new(2026,9,8));throw new InvalidDataException("Retired epoch issued another reader.");}catch(ObjectDisposedException){}
    if(Volatile.Read(ref closed)!=Volatile.Read(ref accepted))throw new InvalidDataException("Lease lifecycle retained a socket.");
    Console.WriteLine("PASS: native option scope leases; shared/idempotent acquisition, exact context hash, saved statistics, business ownership/revision fencing, discovery expiry, stale releases, context CAS without reconnect, fresh owner and joined disposal.");
}finally{stopping.Cancel();listener.Stop();await server;try{await Task.WhenAll(peers);}catch(OperationCanceledException)when(stopping.IsCancellationRequested){}}
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
    public TaskCompletionSource? Hold;
    public bool IsRunning=>true;
    public ValueTask StartAsync()=>ValueTask.CompletedTask;
    public ValueTask StopAsync()=>ValueTask.CompletedTask;
    public ValueTask DisposeAsync()=>ValueTask.CompletedTask;
    public ValueTask PublishAsync(FuturesMarketPriceUpdatedRealtimeEvent value)=>ValueTask.CompletedTask;
    public ValueTask PublishAsync(FuturesTickTradeDataChangedEvent value){Events.Add(value);return Hold is null?ValueTask.CompletedTask:new(Hold.Task);}
    public ValueTask PublishAsync(FuturesTickQuoteDataChangedEvent value,ITickQuoteBufferLease lease)=>ValueTask.CompletedTask;
}


sealed class CapturedEvidence:IOptionTradeEvidenceWriter {
    public OptionTradeEvidence? Value;
    public int Calls;
    public TaskCompletionSource? Hold;
    public TaskCompletionSource Exited=new(TaskCreationOptions.RunContinuationsAsynchronously);
    public async ValueTask WriteAsync(OptionTradeEvidence value,CancellationToken token){
        Interlocked.Increment(ref Calls);
        try{if(Hold is not null)await Hold.Task;token.ThrowIfCancellationRequested();Value=value;}
        finally{Exited.TrySetResult();}
    }
}
