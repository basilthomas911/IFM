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
var first=Guid.NewGuid();var second=Guid.NewGuid();var business=Guid.NewGuid();
var optionDefinition=new WorkerOptionDefinition(context,6000,true);
var request=new WorkerOptionChainRequest("leases",first,generation,new(2026,9,8),new(2026,10,2),at.AddSeconds(30),[optionDefinition],
    Statistics:[new(contract.ContractId,12345,new(2026,9,8),222,new(2026,9,7))]);
var expectedDigest=PricingSemanticHash.Compute(new{request.MaturityDate,request.SeparateContractConnection,Options=request.Options});

using var tradeSource=new NativeOptionTradeSourceReader(runtime,generation,new(2026,9,8));
var writer=new HeldEvidence();var tradeSink=new CapturedTickPublisher();
var delivery=new NativeOptionTradeDelivery(tradeSource,new(runtime),leases,writer,tradeSink);
var diagnostics=new ConcurrentQueue<LiveDiagnostic>();var states=new ConcurrentQueue<LiveLaneStatus>();
var primaryChanged=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
var receiver=new NativeMarketDataDelivery(runtime,leases,quoteDelivery,delivery,(bits,_)=>{if((bits&2)!=0)primaryChanged.TrySetResult();return ValueTask.CompletedTask;},diagnostics.Enqueue,
    _=>throw new InvalidDataException("No HTTP warning expected in this fixture."),(_,state)=>states.Enqueue(state),(_,error)=>faults.Enqueue(error));
try{
    var acquired=await leases.AcquireAsync(request,budget);if(!acquired.Active)throw new InvalidDataException("Signal fixture lease failed: "+acquired.Failure);
    var ownership=new WorkerOptionChainOwnership(1,"trusted-parent",[new(business,[contract.ContractId])],WorkerOptionChainRuntime.PhysicalDigest(request.Options));
    await leases.ReleaseAsync(new(request.ScopeId,Guid.Empty,generation,ownership),budget);
    await writer.Entered.Task.WaitAsync(budget);
    await eventSink.Arrived.Task.WaitAsync(budget);
    if(tradeSink.Events.Count!=0)throw new InvalidDataException("Actor trade preceded its durable acknowledgment.");
    using(var qualificationDeadline=new CancellationTokenSource(TimeSpan.FromMilliseconds(100)))
    {
        try{await receiver.QualifyOptionRecoveryAsync(qualificationDeadline.Token);throw new InvalidDataException("Blocked evidence writer was declared recovered.");}
        catch(OperationCanceledException)when(qualificationDeadline.IsCancellationRequested){}
    }

    // A reset waiting for an actual blocked option writer must not block the
    // sole native signal receiver, diagnostics, or independent futures delivery.
    using(var resetDeadline=new CancellationTokenSource(budget))
    {
        var resetCalled=false;
        var pendingReset=receiver.ReplaceOptionOwnerAsync(()=>{resetCalled=true;return Task.CompletedTask;},resetDeadline.Token);
        await runtime.ResetOptionScopeAsync(request.ScopeId,generation,budget);
        await runtime.StartLiveLaneAsync(MarketDataLane.FuturesGlbx,new('x',32),"127.0.0.1",port,["127.0.0.1:1"],
            [new(["42"],"trades","instrument_id"),new(["42"],"mbp-1","instrument_id"),new(["42"],"statistics","instrument_id")],
            [new(42,contract.UnderlyingContractId,"42",false,null,false,true,3)],true,budget);
        await primaryChanged.Task.WaitAsync(budget);
        if(resetCalled||pendingReset.IsCompleted||runtime.ReadLiveStatus(MarketDataLane.FuturesGlbx).Phase!=3)
            throw new InvalidDataException("Blocked option reset held up futures or falsely joined its writer.");
        resetDeadline.Cancel();
        try{await pendingReset;throw new InvalidDataException("Blocked reset ignored its original cancellation.");}catch(OperationCanceledException){}
    }

    if(eventSink.Events[0].OptionMarketPriceObservation?.PriceBasis!=OptionMarketPriceBasis.QuoteMidpoint)
        throw new InvalidDataException("Independent quote worker did not publish the latest native view.");
    writer.Release.SetResult();
    await tradeSink.Arrived.Task.WaitAsync(budget);
    if(writer.Value is null||writer.Calls is <1 or >2||tradeSink.Events.Count is <1 or >2)throw new InvalidDataException("Signal-driven evidence acknowledgment/event delivery is incomplete.");
    if(!MessagePackBinarySerializer.Shared.Serialize(writer.Value.ToRealtimeEvent())!.SequenceEqual(MessagePackBinarySerializer.Shared.Serialize(tradeSink.Events[0])!))
        throw new InvalidDataException("Signal-driven actor event differs from the existing exact mapping.");
    await receiver.DisposeAsync();
    if(diagnostics.Count<3||states.IsEmpty||!faults.IsEmpty)throw new InvalidDataException("Diagnostic/state observer or independent delivery failed.");
    var report=new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
    var broken=new NativeMarketDataDelivery(runtime,leases,quoteDelivery,delivery,(_,_)=>ValueTask.CompletedTask,
        _=>throw new InvalidOperationException("injected diagnostic observer failure"),_=>{},(_,_)=>{},(_,error)=>report.TrySetResult(error));
    await runtime.ResetOptionScopeAsync(request.ScopeId,generation,budget);
    var observerFailure=await report.Task.WaitAsync(budget);
    if(!observerFailure.Message.Contains("injected diagnostic observer failure"))throw new InvalidDataException("Unknown observer failure was hidden.");
    try{await broken.Completion.WaitAsync(budget);throw new InvalidDataException("Failed signal receiver claimed healthy completion.");}
    catch(InvalidOperationException error)when(error.Message.Contains("injected diagnostic observer failure")){}
    try{await broken.DisposeAsync();throw new InvalidDataException("Failed signal receiver shutdown hid its fault.");}
    catch(InvalidOperationException error)when(error.Message.Contains("injected diagnostic observer failure")){}
    var after=eventSink.Events.Count;
    await leases.ReleaseAsync(new(request.ScopeId,first,generation),budget);
    await leases.ReleaseAsync(new(request.ScopeId,Guid.Empty,generation,ownership with{Revision=2,Owners=[]}),budget);
    if(eventSink.Events.Count!=after)throw new InvalidDataException("Disposed receiver continued publishing.");
    await leases.DisposeAsync();await runtime.StopAndDisposeAsync(budget);
    await Task.WhenAll(peers).WaitAsync(budget);
    if(accepted!=closed)throw new InvalidDataException("Signal receiver retained provider sockets.");
    Console.WriteLine("PASS: single event-driven native receiver; independent quote delivery during blocked trade evidence; durable acknowledgment before exact actor trade; complete diagnostics/status, unknown observer failure reporting, joined workers and sockets; no polling.");
}finally{
    writer.Release.TrySetResult();await receiver.DisposeAsync();stopping.Cancel();listener.Stop();await server;
    try{await Task.WhenAll(peers);}catch(OperationCanceledException)when(stopping.IsCancellationRequested){}
}
sealed class HeldEvidence:IOptionTradeEvidenceWriter{
    public TaskCompletionSource Entered=new(TaskCreationOptions.RunContinuationsAsynchronously),Release=new(TaskCreationOptions.RunContinuationsAsynchronously);
    public int Calls;public OptionTradeEvidence? Value;
    public async ValueTask WriteAsync(OptionTradeEvidence value,CancellationToken token){Interlocked.Increment(ref Calls);Entered.TrySetResult();await Release.Task.WaitAsync(token);Value=value;}
}
sealed class MovingTime(DateTimeOffset value):TimeProvider{public override DateTimeOffset GetUtcNow()=>value;}
sealed class CapturedTickPublisher:ITickAggregationEventPublisher{
    public List<FuturesTickTradeDataChangedEvent> Events=[];
    public TaskCompletionSource Arrived=new(TaskCreationOptions.RunContinuationsAsynchronously);
    public bool IsRunning=>true;
    public ValueTask StartAsync()=>ValueTask.CompletedTask;public ValueTask StopAsync()=>ValueTask.CompletedTask;public ValueTask DisposeAsync()=>ValueTask.CompletedTask;
    public ValueTask PublishAsync(FuturesMarketPriceUpdatedRealtimeEvent value)=>ValueTask.CompletedTask;
    public ValueTask PublishAsync(FuturesTickTradeDataChangedEvent value){Events.Add(value);Arrived.TrySetResult();return ValueTask.CompletedTask;}
    public ValueTask PublishAsync(FuturesTickQuoteDataChangedEvent value,ITickQuoteBufferLease lease)=>ValueTask.CompletedTask;
}
