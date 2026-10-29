using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Rust.Host;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.TradeTickQuoteDb;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Recovery;
using TomasAI.IFM.Shared.MarketData.Contracts.Historical;
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
var definition=System.Text.Json.Nodes.JsonNode.Parse(source)!;definition["hd"]!["publisher_id"]=3;definition["asset"]="VX";definition["raw_symbol"]="VX/X6";source=definition.ToJsonString();
// Quote/statistics-only wire: native transport can be up without a trade.
var recordOffset=checked(8+(int)System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(wire.AsSpan(4)));
var quoteOnly=wire.AsSpan(0,recordOffset).ToArray().ToList();
while(recordOffset<wire.Length){var length=wire[recordOffset]*4;if(wire[recordOffset+1]!=0)quoteOnly.AddRange(wire.AsSpan(recordOffset,length).ToArray());recordOffset+=length;}
wire=quoteOnly.ToArray();
var date=new DateOnly(2026,9,8);var at=new DateTimeOffset(2026,9,8,16,0,1,TimeSpan.Zero);var budget=TimeSpan.FromSeconds(8);
var clock=new FixedClock(at);var history=new TcpListener(IPAddress.Loopback,0);var live=new TcpListener(IPAddress.Loopback,0);history.Start();live.Start();
using var stopping=new CancellationTokenSource();var peers=new ConcurrentBag<Task>();var errors=new ConcurrentQueue<Exception>();var diagnostics=new ConcurrentQueue<LiveDiagnostic>();
var historyPort=((IPEndPoint)history.LocalEndpoint).Port;var livePort=(ushort)((IPEndPoint)live.LocalEndpoint).Port;var accepted=0;var closed=0;
var http=Task.Run(async()=>{try{while(true){var client=await history.AcceptTcpClientAsync(stopping.Token);peers.Add(Task.Run(async()=>{using(client){
    var stream=client.GetStream();var head=new List<byte>();var one=new byte[1];while(!head.TakeLast(4).SequenceEqual(new byte[]{13,10,13,10})){await stream.ReadExactlyAsync(one,stopping.Token);head.Add(one[0]);if(head.Count>16384)throw new InvalidDataException("Oversize request");}
    var text=Encoding.ASCII.GetString(head.ToArray());var length=text.Split("\r\n").Where(x=>x.StartsWith("Content-Length:",StringComparison.OrdinalIgnoreCase)).Select(x=>int.Parse(x.Split(':')[1])).DefaultIfEmpty().Single();
    await stream.ReadExactlyAsync(new byte[length],stopping.Token);
    var body=text.Contains("get_dataset_range",StringComparison.Ordinal)?"""{"start":"1990-01-01T00:00:00Z","end":"2026-12-31T00:00:00Z","schema":{"definition":{"start":"1990-01-01T00:00:00Z","end":"2026-10-25T00:00:00Z"}}}""":source+"\n";
    var bytes=Encoding.UTF8.GetBytes(body);await stream.WriteAsync(Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Length: {bytes.Length}\r\nConnection: close\r\n\r\n"),stopping.Token);await stream.WriteAsync(bytes,stopping.Token);
}}));}}catch(OperationCanceledException)when(stopping.IsCancellationRequested){}});
var firstStream=new TaskCompletionSource<NetworkStream>(TaskCreationOptions.RunContinuationsAsynchronously);
var provider=Task.Run(async()=>{try{while(true){var client=await live.AcceptTcpClientAsync(stopping.Token);var connection=Interlocked.Increment(ref accepted);peers.Add(Task.Run(async()=>{using(client)try{
    var stream=client.GetStream();using var reader=new StreamReader(stream,Encoding.UTF8,false,1024,true);
    await stream.WriteAsync("lsg-test\ncram=t7kNhwj4xqR0QYjzFKtBEG2ec2pXJ4FK\n"u8.ToArray(),stopping.Token);await reader.ReadLineAsync(stopping.Token);
    await stream.WriteAsync("success=1|session_id=5\n"u8.ToArray(),stopping.Token);string? line;while((line=await reader.ReadLineAsync(stopping.Token))is not null&&line!="start_session"){}
    if(line is null)throw new InvalidDataException("Session never started");await stream.WriteAsync(wire,stopping.Token);
    if(connection==1)firstStream.TrySetResult(stream);
    if(await reader.ReadLineAsync(stopping.Token)is not null)throw new InvalidDataException("Expected joined source EOF");
}finally{Interlocked.Increment(ref closed);}}));}}catch(OperationCanceledException)when(stopping.IsCancellationRequested){}});
var sink=new Captured();NativeEpochResources? resources=null;
var registrations=new DatabentoContractRegistration[]{new(){DomainContractId="VX20261225",ProviderContractName="VX/X6",RootSymbol="VX",Dataset="XCBF.PITCH",AssetTypeId=AssetTypeId.Futures,OnTheRun=true}};
var options=new DatabentoMarketDataRuntimeOptions{FeedOptions=DatabentoFeedOptions.ForProfile(FeedDeploymentProfile.Development,"XCBF.PITCH"),Contracts=registrations,FuturesQuoteBatchCapacity=2,LastPriceCapacity=16};
var factory=new NativeMarketDataEpochFactory(clock,async(valueDate,remaining,token)=>resources=await NativeEpochResources.StartAsync(library,new('x',32),$"http://127.0.0.1:{historyPort}/",["127.0.0.1:1"],valueDate,options,
    [new(MarketDataLane.FuturesXcbf,"127.0.0.1",livePort)],new(new('x',32),"127.0.0.1",1,["127.0.0.1:1"]),clock,false,false,sink,new NeverEvidence(),new NoRoutes(),
    (_,_)=>throw new InvalidOperationException("No option context expected"),_=>null,new(diagnostics.Enqueue,_=>{},(_,_)=>{},(_,error)=>errors.Enqueue(error)),remaining,requireFuturesObservations:false,token:token));
var state=new NativeHostState(factory,new DatasetWorkerAdmissionRegistry(),clock,NullLogger<NativeHostState>.Instance,new OptionFeedProcessingHealth(clock));
var desired=new DatasetDesiredSubscriptionRegistry();state.PrepareDate(date,[desired.Set("XCBF.PITCH",date,registrations)]);
var db=Substitute.For<IDbContextFactory>();var raw=Substitute.For<ITradeTickQuoteDbContext>();db.TradeTickQuoteDb.Returns(raw);
var past=at.AddHours(-2);raw.GetLatestRawFuturesTradesAsync("VX20261225",date,Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<BootstrapTradeRow>>([new(date,20.5m,2,77,past.ToUnixTimeMilliseconds()*1000000,past.ToUnixTimeMilliseconds()*1000000+100,84,0,42,3)]));
var historical=Substitute.For<IMarketDataHistoricalProvider>();
await using var services=new ServiceCollection().AddSingleton<IDbContextFactory>(db).AddSingleton(historical).AddSingleton<ILogger<NativeVxBootstrapLoader>>(NullLogger<NativeVxBootstrapLoader>.Instance).BuildServiceProvider();
var log=new ReadyLog();using var bootstrap=new NativeBootstrapPrices(services,factory,state,clock,log);
var epoch=factory.Create(date);
try
{
    await epoch.StartAsync(default);await bootstrap.StartAsync(default);
    if(bootstrap.Capture(date).Length!=0)throw new InvalidDataException("A historical estimate appeared before session activation.");
    bootstrap.StartSession(date);await log.Ready.Task.WaitAsync(budget);
    var seed=bootstrap.Capture(date).Single();
    if(seed.Source!="TradeTickQuoteDb"||seed.Price.Trade?.LastPrice!=20.5m||seed.Price.Trade?.EventTimestamp!=past
        ||epoch.TryGetLastTickPrice("VX20261225",out var current)&&current.Trade is not null)
        throw new InvalidDataException("Historical bootstrap was lost, retimestamped, or treated as a native live trade.");
    bootstrap.StartSession(date);
    await raw.Received(1).GetLatestRawFuturesTradesAsync("VX20261225",date,Arg.Any<CancellationToken>());
    if(historical.ReceivedCalls().Any())throw new InvalidDataException("A valid persisted fallback made an unnecessary provider request.");
    bootstrap.StopSession(date);if(bootstrap.Capture(date).Length!=0)throw new InvalidDataException("Stopped session retained historical fallback authority.");
    await bootstrap.StopAsync(default);await epoch.StopAsync();await epoch.DisposeAsync();
    await Task.WhenAll(peers).WaitAsync(budget);
    if(accepted!=closed||!errors.IsEmpty)throw new InvalidDataException("Bootstrap service retained native sockets or caused a feed fault.");
    Console.WriteLine("PASS VX bootstrap service with real native quote-only SDK epoch: event-driven activation, exact persisted historical estimate, no false live trade/readiness, idempotent activation, no unnecessary HTTP, session close and joined shutdown.");
}
finally{await bootstrap.StopAsync(default);await epoch.DisposeAsync();stopping.Cancel();history.Stop();live.Stop();await http;try{await provider;await Task.WhenAll(peers);}catch(OperationCanceledException)when(stopping.IsCancellationRequested){}}
sealed class ReadyLog:ILogger<NativeBootstrapPrices>
{
    public readonly TaskCompletionSource Ready=new(TaskCreationOptions.RunContinuationsAsynchronously);
    public IDisposable? BeginScope<TState>(TState state)where TState:notnull=>null;public bool IsEnabled(LogLevel level)=>true;
    public void Log<TState>(LogLevel level,EventId id,TState state,Exception? error,Func<TState,Exception?,string> format){if(format(state,error).Contains("VX historical bootstrap ready"))Ready.TrySetResult();}
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
