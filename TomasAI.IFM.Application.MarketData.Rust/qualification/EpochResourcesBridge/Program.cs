using TomasAI.IFM.Application.MarketData.Rust.Publication;
using TomasAI.IFM.Application.MarketData.Contracts;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.MarketData.Rust.Api;
using TomasAI.IFM.Application.MarketData.Rust.Delivery;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Shared.MarketData.Contracts.TickAggregation;
using TomasAI.IFM.Shared.MarketData.Contracts.Ticker;

var library=Path.GetFullPath(args[0]);var wire=await File.ReadAllBytesAsync(args[1]);
using var corpus=JsonDocument.Parse(await File.ReadAllTextAsync(args[2]));var source=corpus.RootElement[0].GetProperty("json").GetString()!;
var definition=System.Text.Json.Nodes.JsonNode.Parse(source)!;definition["hd"]!["publisher_id"]=3;source=definition.ToJsonString();
var extraDefinition=System.Text.Json.Nodes.JsonNode.Parse(source)!;extraDefinition["raw_symbol"]="ESH7";
extraDefinition["expiration"]=checked((new DateTimeOffset(2027,3,19,13,30,0,TimeSpan.Zero).UtcTicks-DateTimeOffset.UnixEpoch.UtcTicks)*100).ToString(System.Globalization.CultureInfo.InvariantCulture);
var extraSource=extraDefinition.ToJsonString();
var quoteWire=new List<byte>();
if(args.Contains("--underlying"))
{
    var offset=checked(8+(int)System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(wire.AsSpan(4)));quoteWire.AddRange(wire.AsSpan(0,offset).ToArray());
    while(offset<wire.Length)
    {
        var length=wire[offset]*4;var record=wire.AsSpan(offset,length).ToArray();
        var message=record.AsSpan().IndexOf("subscription_id=1"u8);
        if(message>=0)
        {
            quoteWire.AddRange(record);
        }
        else if(record[1]==1)quoteWire.AddRange(record);
        offset+=length;
    }
}
var date=new DateOnly(2026,9,8);var at=new DateTimeOffset(2026,9,8,16,0,1,TimeSpan.Zero);var budget=TimeSpan.FromSeconds(8);
var clock=new FixedClock(at);var history=new TcpListener(IPAddress.Loopback,0);var live=new TcpListener(IPAddress.Loopback,0);history.Start();live.Start();
using var stopping=new CancellationTokenSource();var peers=new ConcurrentBag<Task>();var errors=new ConcurrentQueue<Exception>();var diagnostics=new ConcurrentQueue<LiveDiagnostic>();
var historyPort=((IPEndPoint)history.LocalEndpoint).Port;var livePort=(ushort)((IPEndPoint)live.LocalEndpoint).Port;var accepted=0;var closed=0;
var http=Task.Run(async()=>{try{while(true){var client=await history.AcceptTcpClientAsync(stopping.Token);peers.Add(Task.Run(async()=>{using(client){
    var stream=client.GetStream();var head=new List<byte>();var one=new byte[1];while(!head.TakeLast(4).SequenceEqual(new byte[]{13,10,13,10})){await stream.ReadExactlyAsync(one,stopping.Token);head.Add(one[0]);if(head.Count>16384)throw new InvalidDataException("Oversize request");}
    var text=Encoding.ASCII.GetString(head.ToArray());var length=text.Split("\r\n").Where(x=>x.StartsWith("Content-Length:",StringComparison.OrdinalIgnoreCase)).Select(x=>int.Parse(x.Split(':')[1])).DefaultIfEmpty().Single();
    var requestBody=new byte[length];await stream.ReadExactlyAsync(requestBody,stopping.Token);
    var body=text.Contains("get_dataset_range",StringComparison.Ordinal)?"""{"start":"1990-01-01T00:00:00Z","end":"2027-12-31T00:00:00Z","schema":{"definition":{"start":"1990-01-01T00:00:00Z","end":"2026-10-25T00:00:00Z"}}}""":(Encoding.UTF8.GetString(requestBody).Contains("ES.FUT",StringComparison.Ordinal)?extraSource:source)+"\n";
    var bytes=Encoding.UTF8.GetBytes(body);await stream.WriteAsync(Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Length: {bytes.Length}\r\nConnection: close\r\n\r\n"),stopping.Token);await stream.WriteAsync(bytes,stopping.Token);
}}));}}catch(OperationCanceledException)when(stopping.IsCancellationRequested){}});
var firstStream=new TaskCompletionSource<NetworkStream>(TaskCreationOptions.RunContinuationsAsynchronously);
var provider=Task.Run(async()=>{try{while(true){var client=await live.AcceptTcpClientAsync(stopping.Token);var connection=Interlocked.Increment(ref accepted);peers.Add(Task.Run(async()=>{using(client)try{
    var stream=client.GetStream();using var reader=new StreamReader(stream,Encoding.UTF8,false,1024,true);
    await stream.WriteAsync("lsg-test\ncram=t7kNhwj4xqR0QYjzFKtBEG2ec2pXJ4FK\n"u8.ToArray(),stopping.Token);await reader.ReadLineAsync(stopping.Token);
    await stream.WriteAsync("success=1|session_id=5\n"u8.ToArray(),stopping.Token);string? line;while((line=await reader.ReadLineAsync(stopping.Token))is not null&&line!="start_session"){}
    if(line is null)throw new InvalidDataException("Session never started");await stream.WriteAsync(args.Contains("--underlying")&&connection>=4?quoteWire.ToArray():wire,stopping.Token);
    if(connection==1)firstStream.TrySetResult(stream);
    if(await reader.ReadLineAsync(stopping.Token)is not null)throw new InvalidDataException("Expected joined source EOF");
}finally{Interlocked.Increment(ref closed);}}));}}catch(OperationCanceledException)when(stopping.IsCancellationRequested){}});
var sink=new Captured();var injectDiagnosticFailure=0;var receiveFailure=new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
var options=new DatabentoMarketDataRuntimeOptions{FeedOptions=DatabentoFeedOptions.ForProfile(FeedDeploymentProfile.Development,"GLBX.MDP3"),
    Contracts=[new(){DomainContractId="ES20261225",ProviderContractName="ESZ6",AssetTypeId=AssetTypeId.Futures}],FuturesQuoteBatchCapacity=2,LastPriceCapacity=16};
try{
    NativeEpochResources? resources=null;var bootstraps=0;var liveRoutes=new NoRoutes();
    var factory=new NativeMarketDataEpochFactory(clock,async(valueDate,remaining,token)=>{
        Interlocked.Increment(ref bootstraps);
        return resources=await NativeEpochResources.StartAsync(library,new('x',32),$"http://127.0.0.1:{historyPort}/",["127.0.0.1:1"],valueDate,options,
        [new(MarketDataLane.FuturesGlbx,"127.0.0.1",livePort)],new(new('x',32),"127.0.0.1",args.Contains("--underlying")?livePort:(ushort)1,["127.0.0.1:1"]),clock,false,false,sink,new NeverEvidence(),liveRoutes,
        (_,_)=>throw new InvalidOperationException("No option context should be prepared"),d=>d==date?1788883200000000000:throw new InvalidDataException("Wrong replay date"),
        new(d=>{if(Interlocked.Exchange(ref injectDiagnosticFailure,0)==1)throw new InvalidOperationException("fixture transient receiver failure");diagnostics.Enqueue(d);},_=>{},(_,_)=>{},(_,e)=>{errors.Enqueue(e);receiveFailure.TrySetResult(e);}),remaining,token:token);});
    var epoch=factory.Create(date);
    try{_=factory.Current;throw new InvalidDataException("Unstarted epoch was exposed to Host services.");}catch(MarketDataApiNotRunningException){}
    if(bootstraps!=0||epoch.GetHealth().Running)throw new InvalidDataException("Create performed I/O or claimed a running epoch");
    await epoch.StartAsync(default);await epoch.StartAsync(default);
    if(!ReferenceEquals(factory.Current,epoch))throw new InvalidDataException("Host registry did not publish the successfully started native epoch.");
    var composition=new NativeCompositionMarketDataApi(factory);
    var staleGeneration=Guid.NewGuid();
    var staleCapture=await composition.CaptureAsync("GLBX.MDP3",new(Guid.NewGuid(),"missing","Daily",staleGeneration,at,at.AddSeconds(1),true),default);
    var staleAcquire=await composition.AcquireAsync("GLBX.MDP3",new("missing",Guid.NewGuid(),staleGeneration,date,date.AddDays(1),at.AddSeconds(60),[]),default);
    if(staleCapture.Snapshot is not null||staleCapture.Failure?.Code!="Recovering"||staleAcquire.Active||staleAcquire.Failure?.Code!="Recovering")
        throw new InvalidDataException("Stale-generation maintenance must remain fenced with typed Recovering results, without an unknown processing exception or provider acquisition.");
    if(bootstraps!=1||resources is null)throw new InvalidDataException("Idempotent native epoch startup duplicated its resource owner");
    await sink.All.Task.WaitAsync(budget);
    var reader=resources.LastPrices.GetFuturesReader("ES20261225",date);
    if(!reader.TryGetLastTrade(out var trade)||trade.SourceSequence!=23||!reader.TryGetLastQuote(out _))throw new InvalidDataException("Bootstrapped native readers unavailable");
    if(resources.OptionScopes.ActiveScopes.Length!=0||resources.OptionGeneration==Guid.Empty||resources.FuturesGeneration==Guid.Empty)throw new InvalidDataException("Bootstrap created an unexpected option feed or omitted logical identity");
    if(sink.Quotes!=3||sink.Market!=4||sink.Stats!=5)throw new InvalidDataException($"Epoch event assembly differs: {sink.Quotes}/{sink.Market}/{sink.Stats}");
    if(!epoch.IsFeedUp(budget)||!epoch.TryGetLastTickPrice("ES20261225",out var mark)||mark.Trade?.LastPrice is null
        ||!epoch.TryGetFuturesSessionStatistics("ES20261225",out var statistics)||statistics.Volume!=9999
        ||!epoch.TryGetMarketInstrumentId("ES20261225",out var instrument)||instrument!=42)throw new InvalidDataException("Public native epoch snapshots/health are incomplete");
    var owner=new TickerStreamOwner("fixture","epoch","leg1");var other=owner with{LegId="leg2"};
    if(!epoch.StartFuturesRoute(owner,"ES20261225")||epoch.StartFuturesRoute(owner,"ES20261225")||!epoch.StartFuturesRoute(other,"ES20261225")
        ||!epoch.StopFuturesRoute(owner,"ES20261225")||!epoch.IsTickDataStreamActive("ES20261225")||!epoch.GetAggregationStatus("ES20261225").StreamActive)
        throw new InvalidDataException("Public native route ownership is not idempotent/reference-counted");
    var offset=checked(8+(int)System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(wire.AsSpan(4)));
    while(wire[offset+1]!=1)offset=checked(offset+wire[offset]*4);
    var injected=wire.AsSpan(offset,wire[offset]*4).ToArray();
    System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(injected.AsSpan(44),100);
    System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(injected.AsSpan(8),1788883200000010001);
    System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(injected.AsSpan(32),1788883200000010101);
    await (await firstStream.Task.WaitAsync(budget)).WriteAsync(injected);
    var routed=await liveRoutes.Quote.Task.WaitAsync(budget);
    if(routed.Quote.SourceSequence!=100)throw new InvalidDataException("Activated futures transient route did not deliver exact new source");
    var beforeGeneration=epoch.GetHealth().DatasetFeedStatuses!.Single().GenerationId;
    var resetRequest=new DatabentoDatasetResetRequest("GLBX.MDP3",beforeGeneration,date,DatabentoDatasetFailureReason.FullPipelineUnhealthy,budget,budget,Guid.NewGuid());
    var reset=await epoch.ResetDatasetAsync(resetRequest,default);
    if(!reset.Succeeded||reset.GenerationId==beforeGeneration)throw new InvalidDataException("Public native epoch reset did not replace its logical source");
    await sink.WaitAsync(24,budget);
    if(sink.Quotes!=6||!reader.TryGetLastQuote(out var replaced)||replaced.SourceSequence!=14||resources.OptionGeneration!=resources.Runtime.ReadOptionOwnerGeneration()
        ||!ReferenceEquals(reader,epoch.LastPrices.GetFuturesReader("ES20261225",date))||sink.Generation!=reset.GenerationId)
        throw new InvalidDataException("Native reset retained stale buffers/readers, changed option ownership, or mismatched publication/health generations");
    var stale=await epoch.ResetDatasetAsync(resetRequest,default);
    if(!stale.Succeeded||stale.GenerationId!=reset.GenerationId||accepted!=2)throw new InvalidDataException("Stale public reset reconnected an already replaced source");
    var nativeEpoch=(NativeMarketDataEpoch)epoch;
    var optionsBefore=nativeEpoch.OptionGeneration;
    var futuresBefore=resources.Runtime.ReadLiveStatus(MarketDataLane.FuturesGlbx).Generation;
    var restoredOptions=await nativeEpoch.ReplaceOptionOwnerAsync((_,_,_)=>throw new InvalidOperationException("Idle options must not invent reference/subscription work."),budget);
    if(restoredOptions.Count!=0||nativeEpoch.OptionGeneration==optionsBefore||nativeEpoch.IsOptionOwnerReplacementPending
        ||resources.Runtime.ReadLiveStatus(MarketDataLane.FuturesGlbx).Generation!=futuresBefore
        ||epoch.GetHealth().DatasetFeedStatuses!.Single().GenerationId!=reset.GenerationId||accepted!=2
        ||!reader.TryGetLastQuote(out _))throw new InvalidDataException("Whole option-owner replacement disturbed futures or opened an unwanted option socket.");
    var deliveredBefore=sink.Quotes+sink.Market+sink.Stats;
    Volatile.Write(ref injectDiagnosticFailure,1);
    await resources.Runtime.ResetLiveLaneAsync(MarketDataLane.FuturesGlbx,budget);
    var expectedReceiverFailure=await receiveFailure.Task.WaitAsync(budget);
    if(expectedReceiverFailure is not NativeDeliveryFailure{Component:"Receiver",LaneMask:3}||epoch.GetHealth().Running)
        throw new InvalidDataException("A terminated receiver stayed healthy or lost its component/lane evidence.");
    if(!await nativeEpoch.EnsureDeliveryOperationalAsync(budget)||!resources.IsDeliveryOperational)
        throw new InvalidDataException("Transient receiver failure could not recreate joined delivery ownership.");
    await nativeEpoch.ReplaceOptionOwnerAsync((_,_,_)=>throw new InvalidOperationException("Idle recovery invented option definitions."),budget);
    await sink.WaitAsync(deliveredBefore+12,budget);
    if(!epoch.GetHealth().Running||accepted!=3||errors.TryDequeue(out var actualFailure)!=true||actualFailure!=expectedReceiverFailure)
        throw new InvalidDataException("Receiver repair did not resume actual source events without extra provider processes/sockets.");
    if(args.Contains("--host-postgres"))await HostRecoveryCases.VerifyAsync(factory,nativeEpoch,clock,budget);
    if(args.Contains("--underlying"))
    {
        var primaryOwner=resources.Runtime.ReadLiveStatus(MarketDataLane.FuturesGlbx).Generation;
        // The fixture's qualified canonical definition is deliberately distinct
        // from its configured primary domain alias: this must be option-only.
        var price=await nativeEpoch.ReadAdditionalUnderlyingPriceAsync("ES20270319",budget,default);
        var dependency=resources.OptionScopes.ActiveScopes.Single(x=>x.StartsWith("underlying-",StringComparison.Ordinal));
        var dependencyOwner=resources.Runtime.ReadOptionScopeStatus(dependency).Generation;
        if(price is not>0||!resources.OptionScopes.MonitoredQuotes.IsEmpty||resources.Runtime.ReadLiveStatus(MarketDataLane.FuturesGlbx).Generation!=primaryOwner)
            throw new InvalidDataException("Additional underlying used a fake price, monitored an option, or changed primary futures.");
        await nativeEpoch.ReplaceOptionOwnerAsync((_,_,_)=>throw new InvalidDataException("A quote prerequisite must not request option pricing references."),budget);
        if(resources.Runtime.ReadOptionScopeStatus(dependency).Generation==dependencyOwner||await nativeEpoch.ReadAdditionalUnderlyingPriceAsync("ES20270319",budget,default) is not>0
            ||resources.Runtime.ReadLiveStatus(MarketDataLane.FuturesGlbx).Generation!=primaryOwner)
            throw new InvalidDataException("Underlying quote dependency did not restore with its replaced options subtree.");
        Console.WriteLine("PASS exact additional underlying: native catalog qualification, real SDK quote, no option pricing context or primary subscription mutation, whole option owner replacement/restoration, joined shutdown.");
    }
    if(!epoch.StopFuturesRoute(other,"ES20261225")||epoch.IsTickDataStreamActive("ES20261225"))throw new InvalidDataException("Final route owner did not deactivate delivery");
    await epoch.StopAsync();await epoch.DisposeAsync();
    if(epoch.GetHealth().Running)throw new InvalidDataException("Stopped public epoch still reports running");
    await Task.WhenAll(peers).WaitAsync(budget);
    if(reader.TryGetLastTrade(out _)||resources.LastPrices.IsActive||accepted!=closed||!errors.IsEmpty)throw new InvalidDataException("Epoch cleanup retained authority/socket or reported an unexpected error");
    await VerifyStalledBootstrap();
    Console.WriteLine("PASS: native catalog HTTP -> complete epoch bootstrap -> SDK futures -> receiver/publisher -> stable readers -> joined idempotent cleanup; no option socket without acquisition and API clock/date only.");
}finally{stopping.Cancel();history.Stop();live.Stop();await http;try{await provider;await Task.WhenAll(peers);}catch(OperationCanceledException)when(stopping.IsCancellationRequested){}}
async Task VerifyStalledBootstrap()
{
    var connectionsBefore=Volatile.Read(ref accepted);
    var fixture=new TcpListener(IPAddress.Loopback,0);fixture.Start();using var stop=new CancellationTokenSource();
    var peer=Task.Run(async()=>{using var client=await fixture.AcceptTcpClientAsync(stop.Token);var stream=client.GetStream();
        var one=new byte[1];var request=new List<byte>();while(!request.TakeLast(4).SequenceEqual(new byte[]{13,10,13,10})){await stream.ReadExactlyAsync(one,stop.Token);request.Add(one[0]);}
        var text=Encoding.ASCII.GetString(request.ToArray());var length=text.Split("\r\n").Where(x=>x.StartsWith("Content-Length:",StringComparison.OrdinalIgnoreCase)).Select(x=>int.Parse(x.Split(':')[1])).DefaultIfEmpty().Single();
        await stream.ReadExactlyAsync(new byte[length],stop.Token);
        await stream.WriteAsync("HTTP/1.1 200 OK\r\nContent-Length: 10000\r\nConnection: close\r\n\r\n{"u8.ToArray(),stop.Token);
        try{if(await stream.ReadAsync(one,stop.Token)!=0)throw new InvalidDataException("Stalled bootstrap source did not close");}catch(IOException){}
    });
    var started=System.Diagnostics.Stopwatch.GetTimestamp();
    try
    {
        try{await NativeEpochResources.StartAsync(library,new('x',32),$"http://127.0.0.1:{((IPEndPoint)fixture.LocalEndpoint).Port}/",["127.0.0.1:1"],date,options,
            [new(MarketDataLane.FuturesGlbx,"127.0.0.1",livePort)],new(new('x',32),"127.0.0.1",1,["127.0.0.1:1"]),clock,false,false,sink,new NeverEvidence(),new NoRoutes(),
            (_,_)=>throw new InvalidOperationException(),_=>null,new(_=>{},_=>{},(_,_)=>{},(_,_)=>{}),TimeSpan.FromMilliseconds(250));
            throw new InvalidDataException("Stalled bootstrap succeeded");}
        catch(Exception error)when(error is TimeoutException or OperationCanceledException or AggregateException){}
        if(System.Diagnostics.Stopwatch.GetElapsedTime(started)>TimeSpan.FromSeconds(2)||accepted!=connectionsBefore)throw new InvalidDataException("Bootstrap renewed its budget or connected a live lane before catalog qualification");
        await peer.WaitAsync(budget);
    }
    finally{stop.Cancel();fixture.Stop();try{await peer;}catch(OperationCanceledException)when(stop.IsCancellationRequested){}}
}
sealed class FixedClock(DateTimeOffset at):TimeProvider{public override DateTimeOffset GetUtcNow()=>at;}
sealed class NeverEvidence:IOptionTradeEvidenceWriter{public ValueTask WriteAsync(OptionTradeEvidence value,CancellationToken token)=>throw new InvalidOperationException("No option trade expected");}
sealed class NoRoutes:ITickLiveRouter{
    readonly ConcurrentDictionary<string,byte> active=new();public TaskCompletionSource<LiveTickQuoteServiceEvent> Quote=new(TaskCreationOptions.RunContinuationsAsynchronously);
    public bool Activate(string id)=>active.TryAdd(id,0);public bool Deactivate(string id)=>active.TryRemove(id,out _);public bool IsActive(string id)=>active.ContainsKey(id);public void Clear()=>active.Clear();
    public ValueTask RouteAsync(LiveTickQuoteServiceEvent e){if(IsActive(e.ContractId))Quote.TrySetResult(e);return ValueTask.CompletedTask;}public ValueTask RouteAsync(LiveTickTradeServiceEvent e)=>ValueTask.CompletedTask;
}
sealed class Captured:ITickAggregationEventPublisher{
    public int Quotes,Market,Stats;public Guid Generation;readonly System.Threading.Channels.Channel<int> changed=System.Threading.Channels.Channel.CreateBounded<int>(new System.Threading.Channels.BoundedChannelOptions(1){FullMode=System.Threading.Channels.BoundedChannelFullMode.DropOldest});
    public TaskCompletionSource All=new(TaskCreationOptions.RunContinuationsAsynchronously);void Check(){changed.Writer.TryWrite(Quotes+Market+Stats);if(Quotes+Market+Stats>=12)All.TrySetResult();}
    public async Task WaitAsync(int count,TimeSpan timeout){using var deadline=new CancellationTokenSource(timeout);while(Quotes+Market+Stats<count)await changed.Reader.ReadAsync(deadline.Token);}
    public bool IsRunning=>true;public ValueTask StartAsync()=>ValueTask.CompletedTask;public ValueTask StopAsync()=>ValueTask.CompletedTask;public ValueTask DisposeAsync()=>ValueTask.CompletedTask;
    public ValueTask PublishAsync(FuturesMarketPriceUpdatedRealtimeEvent value){Generation=value.SourceGenerationId;Market++;Check();return ValueTask.CompletedTask;}
    public ValueTask PublishAsync(FuturesTickTradeDataChangedEvent value)=>throw new InvalidDataException("No option or standalone trade expected");
    public ValueTask PublishAsync(FuturesTickQuoteDataChangedEvent value,ITickQuoteBufferLease lease){Generation=value.SourceGenerationId;Quotes++;lease.Dispose();Check();return ValueTask.CompletedTask;}
    public ValueTask PublishAsync(FuturesSessionStatisticsUpdatedRealtimeEvent value){Generation=value.SourceGenerationId;Stats++;Check();return ValueTask.CompletedTask;}
}
