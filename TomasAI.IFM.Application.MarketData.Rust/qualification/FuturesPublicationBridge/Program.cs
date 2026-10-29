using System.Net;
using System.Collections.Concurrent;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.MarketData.Rust.Api;
using TomasAI.IFM.Application.MarketData.Rust.OptionChains;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using TomasAI.IFM.Application.MarketData.Rust.Delivery;
using TomasAI.IFM.Application.MarketData.Rust.Publication;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.FuturesVwapSignal;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Framework.MarketData.DataBento.LastPrice;
using TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation;
using TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation.Contracts;
using TomasAI.IFM.Framework.MarketData.TickAggregation;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.MarketData.Contracts.TickAggregation;
using TomasAI.IFM.Shared.MarketData.Contracts.Ticker;
using TomasAI.IFM.Shared.MarketData.Definitions;
var library=Path.GetFullPath(args[0]);var wire=await File.ReadAllBytesAsync(args[1]);
var vx=args.Length>2&&args[2]=="VX";var lane=vx?MarketDataLane.FuturesXcbf:MarketDataLane.FuturesGlbx;var dataset=vx?"XCBF.PITCH":"GLBX.MDP3";var contractId=vx?"VX20261218":"ES20261218";
var statisticsReplay=args.Length>3&&args[3]=="stats";
var mixedPublishers=args.Length>3&&args[3]=="mixed";
ushort definitionPublisher=mixedPublishers?(ushort)105:(ushort)3;
if(mixedPublishers)
{
    if(!vx)throw new ArgumentException("Mixed publisher fixture requires XCBF.PITCH.");
    var offset=checked(8+(int)System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(wire.AsSpan(4)));var index=0;
    while(offset<wire.Length)
    {
        var length=wire[offset]*4;
        if(System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(wire.AsSpan(offset+4))==42)
            System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(wire.AsSpan(offset+2),(ushort)(++index%2==0?105:106));
        offset+=length;
    }
}
var date=new DateOnly(2026,9,8);var at=new DateTimeOffset(2026,9,8,16,0,1,TimeSpan.Zero);var budget=TimeSpan.FromSeconds(10);
var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();var port=(ushort)((IPEndPoint)listener.LocalEndpoint).Port;
using var stopping=new CancellationTokenSource();
var server=Task.Run(async()=>{using var client=await listener.AcceptTcpClientAsync(stopping.Token);var stream=client.GetStream();using var reader=new StreamReader(stream,Encoding.UTF8,false,1024,true);
    await stream.WriteAsync("lsg-test\ncram=t7kNhwj4xqR0QYjzFKtBEG2ec2pXJ4FK\n"u8.ToArray(),stopping.Token);if(await reader.ReadLineAsync(stopping.Token)is null)return;
    await stream.WriteAsync("success=1|session_id=5\n"u8.ToArray(),stopping.Token);string? line;var count=0;
    while((line=await reader.ReadLineAsync(stopping.Token))is not null&&line!="start_session")count++;
    if(count!=3||line is null)throw new InvalidDataException("Expected trades/quotes/statistics subscriptions.");
    await stream.WriteAsync(wire,stopping.Token);if(await reader.ReadLineAsync(stopping.Token)is not null)throw new InvalidDataException("Expected source cleanup EOF.");});
await using var runtime=new NativeMarketDataRuntime(library,2);runtime.ConfigureLive(date,Guid.NewGuid(),at,false,16);
var generation=Guid.NewGuid();var mapping=new TickContractMapping(dataset,date,definitionPublisher,42,contractId,AssetTypeId.Futures,
    new TickerContractDetails{ContractId=contractId,Dataset=dataset,DefinitionDate=date,PublisherId=definitionPublisher,InstrumentId=42,AssetTypeId=AssetTypeId.Futures,Ticker=vx?"VX":"ES",TickSize=.25m});
var actual=new Captured();var expected=new Captured();var pool=new TickQuoteBufferPool([(2,4)]);
var originalRoutes=new Routed();var nativeRoutes=new Routed();
using var mapper=new NativeFuturesPublication(actual,pool,date,generation,[(lane,mapping)],2,
    (selected,physical)=>runtime.ReadLiveStatus(selected).Generation==physical,nativeRoutes);
using var currentLast=new DatabentoLastPriceStore(date,1);currentLast.RegisterContract(mapping.ContractId,AssetTypeId.Futures);
var clock=new MutableTime(at);
await using var current=new TickAggregationService(DispatchProxy.Create<IDatabentoTickerFeed,Unused>(),DispatchProxy.Create<ITickContractMappingProvider,Unused>(),
    expected,new TickQuoteBufferPool([(2,4)]),new FixedDate(date),new(){Dataset=mapping.Dataset,DefinitionDate=date,FuturesQuoteBatchCapacity=2},clock,lastPrices:currentLast,liveRouter:originalRoutes,generationId:generation);
var type=typeof(TickAggregationService);var stateType=type.GetNestedType("TickerState",BindingFlags.NonPublic)!;
var process=type.GetMethod("ProcessRecordCoreAsync",BindingFlags.Instance|BindingFlags.NonPublic)!;
var flush=type.GetMethod("FlushAsync",BindingFlags.Instance|BindingFlags.NonPublic)!;
object? state=null;Guid physical=Guid.Empty;var records=new LivePublication[16];var total=0;
try{
    await runtime.StartLiveLaneAsync(lane,new('x',32),"127.0.0.1",port,["127.0.0.1:1"],
        [new(["42"],"trades","instrument_id",1788883200000000000),new(["42"],"mbp-1","instrument_id"),new(["42"],"statistics","instrument_id",statisticsReplay?1788883200000000000:null)],
        [new(42,mapping.ContractId,"42",false,250000000,!vx,true,definitionPublisher)],true,budget);
    var finished=false;using var timeout=new CancellationTokenSource(budget);
    while(true){
        var length=runtime.DrainLivePublications(records);
        for(var i=0;i<length;i++){
            var frame=records[i];total++;
            if(frame.ObservedAtUtc!=at)throw new InvalidDataException("Source observation time was replaced at managed drain.");
            if(state is null){
                physical=Identity(frame.Source.GenerationLow,frame.Source.GenerationHigh);state=Activator.CreateInstance(stateType,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,[mapping],null)!;
                stateType.GetField("ValueDate")!.SetValue(state,date);stateType.GetProperty("StreamEpochId")!.SetValue(state,physical);
                var vwap=stateType.GetProperty("Vwap")!.GetValue(state)!;vwap.GetType().GetMethod("Reset")!.Invoke(vwap,[physical]);
                // Native replay rows have already contributed to its book and
                // intentionally do not occupy the live output FIFO. Feed the
                // original first wire trade independently to the C# oracle.
                await ((ValueTask)process.Invoke(current,[state,InitialReplayTrade(wire),CancellationToken.None])!);
            }
            clock.At=frame.ObservedAtUtc;
            await ((ValueTask)process.Invoke(current,[state,Source(frame.Source,mapping),CancellationToken.None])!);
            await mapper.ProcessAsync(frame);
        }
        foreach(var message in runtime.DrainLiveDiagnostics())if(message.Message=="futures fixture complete")finished=true;
        if(finished&&length<16)break;
        if(length<16)await runtime.WaitForLiveChangesAsync(timeout.Token);
    }
    if(state is null||total<15)throw new InvalidDataException("Incomplete futures replay fixture.");
    await ((ValueTask)flush.Invoke(current,[state,QuoteEmissionReason.FeedStopped,(DateTime?)at.UtcDateTime,CancellationToken.None])!);
    await mapper.FlushAsync(QuoteEmissionReason.FeedStopped,at);
    var totalMetrics=mapper.GetMetrics();var laneMetrics=mapper.GetMetrics(lane);var otherMetrics=mapper.GetMetrics(lane==MarketDataLane.FuturesGlbx?MarketDataLane.FuturesXcbf:MarketDataLane.FuturesGlbx);
    if(totalMetrics.SourceQuoteRecords!=laneMetrics.SourceQuoteRecords||totalMetrics.SourceTradeRecords!=laneMetrics.SourceTradeRecords||totalMetrics.RecordsStarted!=laneMetrics.RecordsStarted||totalMetrics.RecordsCompleted!=laneMetrics.RecordsCompleted||otherMetrics.SourceQuoteRecords!=0||otherMetrics.SourceTradeRecords!=0||otherMetrics.RecordsStarted!=0)throw new InvalidDataException("Dataset health duplicated global delivery counters.");
    if(expected.Packets.Count!=actual.Packets.Count)throw new InvalidDataException($"Futures event count differs: C# {expected.Packets.Count}, Rust {actual.Packets.Count}.");
    for(var i=0;i<expected.Packets.Count;i++)if(!expected.Packets[i].AsSpan().SequenceEqual(actual.Packets[i]))
    {
        var evidence=Path.GetDirectoryName(Path.GetFullPath(args[1]))!;
        var jsonOptions=MessagePack.MessagePackSerializerOptions.Standard.WithCompression(MessagePack.MessagePackCompression.Lz4BlockArray);
        await File.WriteAllTextAsync(Path.Combine(evidence,"publication-expected.json"),MessagePack.MessagePackSerializer.ConvertToJson(expected.Packets[i],jsonOptions));
        await File.WriteAllTextAsync(Path.Combine(evidence,"publication-actual.json"),MessagePack.MessagePackSerializer.ConvertToJson(actual.Packets[i],jsonOptions));
        throw new InvalidDataException($"Futures complete event mismatch at {i}: C# {expected.Names[i]}, native {actual.Names[i]}.");
    }
    if(actual.Names.Count(x=>x=="quote")!=3||actual.Names.Count(x=>x=="market")!=(vx?8:2)||actual.Names.Count(x=>x=="vwap")!=(vx?0:2))
        throw new InvalidDataException("Replay/quote batch/changed-price/VWAP fixture did not reach expected paths: "+string.Join(",",actual.Names));
    if(originalRoutes.Packets.Count!=nativeRoutes.Packets.Count||originalRoutes.Packets.Where((packet,i)=>!packet.AsSpan().SequenceEqual(nativeRoutes.Packets[i])).Any())
        throw new InvalidDataException("Activated individual live routes differ from C#.");
    if(nativeRoutes.Packets.Count!=10)throw new InvalidDataException("Replay/suppressed-trade live routing differs.");
    var failing=new Captured{Fail=true};using var failurePool=new TickQuoteBufferPool([(2,4)]);using(var failedMapper=new NativeFuturesPublication(failing,failurePool,date,generation,[(lane,mapping)],2,(_,_)=>true)){
        var failed=false;foreach(var frame in records.Take(Math.Min(total,records.Length))){try{await failedMapper.ProcessAsync(frame);}catch(InvalidOperationException error)when(error.Message=="injected publication failure"){failed=true;break;}}
        if(!failed)throw new InvalidDataException("Unknown publisher exception was swallowed instead of reaching recovery.");
    }
    var before=actual.Packets.Count;await runtime.StopLiveLaneAsync(lane,budget);
    await mapper.ProcessAsync(records[0]);if(actual.Packets.Count!=before)throw new InvalidDataException("Retired physical source emitted another event.");
    await runtime.StopAndDisposeAsync(budget);await server.WaitAsync(budget);pool.Dispose();
    await VerifyReceiverAsync();
    Console.WriteLine($"PASS: {total} actual SDK futures records -> native frozen state -> {actual.Packets.Count} events exactly match unchanged C# service after normalizing independent generated IDs; replay/VWAP/session/quote batches/changed trade, physical fencing and joined socket.");
}finally{stopping.Cancel();listener.Stop();try{await server;}catch(OperationCanceledException)when(stopping.IsCancellationRequested){}}
async Task VerifyReceiverAsync(){
    var fixture=new TcpListener(IPAddress.Loopback,0);fixture.Start();var endpoint=(ushort)((IPEndPoint)fixture.LocalEndpoint).Port;
    using var stop=new CancellationTokenSource();
    var sourceStream=new TaskCompletionSource<NetworkStream>(TaskCreationOptions.RunContinuationsAsynchronously);
    var peer=Task.Run(async()=>{using var client=await fixture.AcceptTcpClientAsync(stop.Token);var stream=client.GetStream();using var reader=new StreamReader(stream,Encoding.UTF8,false,1024,true);
        await stream.WriteAsync("lsg-test\ncram=t7kNhwj4xqR0QYjzFKtBEG2ec2pXJ4FK\n"u8.ToArray(),stop.Token);await reader.ReadLineAsync(stop.Token);
        await stream.WriteAsync("success=1|session_id=5\n"u8.ToArray(),stop.Token);string? line;while((line=await reader.ReadLineAsync(stop.Token))is not null&&line!="start_session"){}
        await stream.WriteAsync(wire,stop.Token);sourceStream.TrySetResult(stream);if(await reader.ReadLineAsync(stop.Token)is not null)throw new InvalidDataException("Expected signal receiver source EOF.");});
    try{
        await using var owner=new NativeMarketDataRuntime(library,2);owner.ConfigureLive(date,Guid.NewGuid(),at,false,16);await owner.PrepareOptionOwnerAsync(Guid.Empty,budget);
        var optionGeneration=owner.ReadOptionOwnerGeneration();var errors=new ConcurrentQueue<Exception>();
        await using var options=new NativeOptionChainRuntime(owner,new(new('x',32),"127.0.0.1",1,["127.0.0.1:1"]),optionGeneration,date,clock,new(),false,
            _=>throw new InvalidOperationException("No option acquisition expected"),(_,_)=>throw new InvalidOperationException("No option preparation expected"),_=>null,errors.Enqueue);
        var sink=new Captured{Target=expected.Packets.Count};using var quotePool=new TickQuoteBufferPool([(2,4)]);
        using var assemble=new NativeFuturesPublication(sink,quotePool,date,generation,[(lane,mapping)],2,(selected,id)=>owner.ReadLiveStatus(selected).Generation==id);
        var primary=new NativeFuturesDelivery(owner,assemble);
        using var source=new NativeOptionTradeSourceReader(owner,optionGeneration,date);
        var quote=new NativeOptionQuoteDelivery(options,new(optionGeneration,sink,options.IsMonitoredContract,clock));
        var trade=new NativeOptionTradeDelivery(source,new(owner),options,new NeverEvidence(),sink);
        await using var receiver=new NativeMarketDataDelivery(owner,options,quote,trade,primary.OnChangesAsync,_=>{},_=>{},(_,_)=>{},(_,error)=>errors.Enqueue(error));
        await owner.StartLiveLaneAsync(lane,new('x',32),"127.0.0.1",endpoint,["127.0.0.1:1"],
            [new(["42"],"trades","instrument_id",1788883200000000000),new(["42"],"mbp-1","instrument_id"),new(["42"],"statistics","instrument_id",statisticsReplay?1788883200000000000:null)],
            [new(42,mapping.ContractId,"42",false,250000000,!vx,true,definitionPublisher)],true,budget);
        await sink.All.Task.WaitAsync(budget);await receiver.DisposeAsync();await assemble.FlushAsync(QuoteEmissionReason.FeedStopped,at);
        if(!errors.IsEmpty)throw new AggregateException(errors);
        if(sink.Packets.Count!=expected.Packets.Count||sink.Packets.Where((packet,i)=>!packet.AsSpan().SequenceEqual(expected.Packets[i])).Any())
            throw new InvalidDataException("Single signal receiver changed primary futures event ordering/payloads.");
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var exited=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var blocked=new NativeMarketDataDelivery(owner,options,quote,trade,async(_,_)=>{entered.TrySetResult();await release.Task;exited.TrySetResult();},
            _=>{},_=>{},(_,_)=>{},(_,error)=>errors.Enqueue(error));
        var sourceOffset=checked(8+(int)System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(wire.AsSpan(4)));
        while(wire[sourceOffset+1]!=0x17)sourceOffset=checked(sourceOffset+wire[sourceOffset]*4);
        var firstRecordLength=wire[sourceOffset]*4;
        await (await sourceStream.Task.WaitAsync(budget)).WriteAsync(wire.AsMemory(sourceOffset,firstRecordLength));
        await entered.Task.WaitAsync(budget);
        var cleanupAt=System.Diagnostics.Stopwatch.GetTimestamp();
        try{await blocked.StopAndDisposeAsync(TimeSpan.FromMilliseconds(100));throw new InvalidDataException("An unjoined callback claimed successful shutdown.");}
        catch(OperationCanceledException){}
        if(blocked.IsJoined||System.Diagnostics.Stopwatch.GetElapsedTime(cleanupAt)>TimeSpan.FromSeconds(1))throw new InvalidDataException("Shutdown ignored its original remaining budget.");
        cleanupAt=System.Diagnostics.Stopwatch.GetTimestamp();
        try{await blocked.StopAndDisposeAsync(budget);throw new InvalidDataException("A second shutdown renewed the original deadline.");}catch(OperationCanceledException){}
        if(System.Diagnostics.Stopwatch.GetElapsedTime(cleanupAt)>TimeSpan.FromSeconds(1))throw new InvalidDataException("Repeated shutdown waited on a renewed deadline.");
        release.TrySetResult();await exited.Task.WaitAsync(budget);
        await owner.StopLiveLaneAsync(lane,budget);await options.DisposeAsync();await owner.StopAndDisposeAsync(budget);await peer.WaitAsync(budget);
    }finally{stop.Cancel();fixture.Stop();try{await peer;}catch(OperationCanceledException)when(stop.IsCancellationRequested){}}
}
static Guid Identity(ulong low,ulong high){Span<byte>b=stackalloc byte[16];System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(b,low);System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(b[8..],high);return new(b);}
static unsafe MarketRecord64 Source(LiveMarketEvent source,TickContractMapping mapping){
    var kind=source.Kind switch{1=>MarketRecordKind.Quote,2=>MarketRecordKind.Trade,4=>MarketRecordKind.Statistics,5=>MarketRecordKind.TradeReplayComplete,6=>MarketRecordKind.StatisticsReplayComplete,_=>throw new InvalidDataException("Unexpected fixture record")};
    var header=new MarketRecordHeader32(source.Instrument,source.Kind>=5?mapping.PublisherId:checked((ushort)(source.Fields[4]&0xffff)),kind,checked((byte)(source.Fields[4]>>16)),source.Fields[1],source.Fields[2],checked((uint)source.Fields[0]));
    return source.Kind switch{
        1=>new(new QuoteRecord64(header,source.Fields[5],source.Fields[6],checked((uint)source.Fields[7]),checked((uint)source.Fields[8]),checked((uint)source.Fields[9]),checked((uint)source.Fields[10]))),
        2=>new(new TradeRecord64(header,source.Fields[5],checked((uint)source.Fields[6]),checked((byte)source.Fields[7]),checked((byte)source.Fields[8]),checked((byte)source.Fields[9]))),
        _=>new(new StatisticsRecord64(header,source.Fields[5],source.Fields[6],source.Fields[7],checked((ushort)source.Fields[8]),checked((ushort)source.Fields[9]),checked((byte)source.Fields[10]),checked((byte)source.Fields[11])))};
}
static MarketRecord64 InitialReplayTrade(byte[] wire){
    var offset=checked(8+(int)System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(wire.AsSpan(4)));
    while(wire[offset+1]!=0)offset=checked(offset+wire[offset]*4);
    var r=wire.AsSpan(offset,48);
    var header=new MarketRecordHeader32(System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(r[4..]),System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(r[2..]),MarketRecordKind.Trade,2,
        System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(r[8..]),System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(r[32..]),System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(r[44..]));
    return new(new TradeRecord64(header,System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(r[16..]),System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(r[24..]),r[28],r[29],r[30]));
}
public class Unused:DispatchProxy{protected override object? Invoke(MethodInfo? method,object?[]? args){if(method?.Name=="Dispose")return null;throw new InvalidOperationException("Qualification must not call a current provider: "+method?.Name);}}
sealed class FixedDate(DateOnly date):ITickValueDateProvider{public DateOnly GetValueDate(DateTime at)=>date;}
sealed class MutableTime(DateTimeOffset at):TimeProvider{public DateTimeOffset At=at;public override DateTimeOffset GetUtcNow()=>At;}
sealed class Captured:ITickAggregationEventPublisher{
    public List<byte[]> Packets=[];public List<string> Names=[];public bool IsRunning=>true;public bool Fail;public int Target=int.MaxValue;public TaskCompletionSource All=new(TaskCreationOptions.RunContinuationsAsynchronously);
    void Accepted(){if(Packets.Count>=Target)All.TrySetResult();}
    static FuturesMarketPriceSnapshot Normalize(FuturesMarketPriceSnapshot snapshot)=>snapshot with{Trade=snapshot.Trade is{} trade?trade with{StreamEpochId=Guid.Empty}:null};
    static FuturesTickTradeDataChangedEvent Trade(FuturesTickTradeDataChangedEvent value)=>value with{Id=Guid.Empty,CommandId=Guid.Empty};
    public ValueTask PublishAsync(FuturesMarketPriceUpdatedRealtimeEvent value){if(Fail)throw new InvalidOperationException("injected publication failure");Names.Add(value.VwapCheckpoint is null?"market":"vwap");Packets.Add(MessagePackBinarySerializer.Shared.Serialize(value with{Id=Guid.Empty,CommandId=Guid.Empty,Price=Normalize(value.Price),SourceTrade=value.SourceTrade is{} trade?Trade(trade):null,
        VwapCheckpoint=value.VwapCheckpoint is{} cp?cp with{StreamEpochId=Guid.Empty,RecoveryGenerationId=Guid.Empty}:null})!);Accepted();return ValueTask.CompletedTask;}
    public ValueTask PublishAsync(FuturesSessionStatisticsUpdatedRealtimeEvent value){Names.Add("stats");Packets.Add(MessagePackBinarySerializer.Shared.Serialize(value with{Id=Guid.Empty,CommandId=Guid.Empty})!);Accepted();return ValueTask.CompletedTask;}
    public ValueTask PublishAsync(FuturesTickTradeDataChangedEvent value){Names.Add("trade");Packets.Add(MessagePackBinarySerializer.Shared.Serialize(Trade(value))!);Accepted();return ValueTask.CompletedTask;}
    public ValueTask PublishAsync(FuturesTickQuoteDataChangedEvent value,ITickQuoteBufferLease lease){Names.Add("quote");Packets.Add(MessagePackBinarySerializer.Shared.Serialize(value with{Id=Guid.Empty,CommandId=Guid.Empty})!);lease.Dispose();Accepted();return ValueTask.CompletedTask;}
    public ValueTask StartAsync()=>ValueTask.CompletedTask;public ValueTask StopAsync()=>ValueTask.CompletedTask;public ValueTask DisposeAsync()=>ValueTask.CompletedTask;
}

sealed class Routed:ITickLiveRouter{
    public List<byte[]> Packets=[];public bool Active=true;
    public bool Activate(string id)=>Active=true;public bool Deactivate(string id){Active=false;return true;}public bool IsActive(string id)=>Active;public void Clear()=>Active=false;
    public ValueTask RouteAsync(LiveTickQuoteServiceEvent value){Packets.Add(MessagePackBinarySerializer.Shared.Serialize(value with{EventId=Guid.Empty})!);return ValueTask.CompletedTask;}
    public ValueTask RouteAsync(LiveTickTradeServiceEvent value){Packets.Add(MessagePackBinarySerializer.Shared.Serialize(value with{EventId=Guid.Empty})!);return ValueTask.CompletedTask;}
}

sealed class NeverEvidence:IOptionTradeEvidenceWriter{public ValueTask WriteAsync(OptionTradeEvidence value,CancellationToken token)=>throw new InvalidOperationException("Unexpected option evidence on futures-only source");}
