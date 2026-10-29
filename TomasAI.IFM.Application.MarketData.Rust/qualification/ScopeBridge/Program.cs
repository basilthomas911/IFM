using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using TomasAI.IFM.Application.MarketData.Rust.Delivery;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;

var library=Path.GetFullPath(args[0]);var wire=await File.ReadAllBytesAsync(args[1]);
var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();var port=(ushort)((IPEndPoint)listener.LocalEndpoint).Port;
using var stopping=new CancellationTokenSource();var peers=new ConcurrentBag<Task>();int accepted=0,closed=0,stall=0;
var server=Task.Run(async()=>{
    try{while(true){var client=await listener.AcceptTcpClientAsync(stopping.Token);Interlocked.Increment(ref accepted);
        peers.Add(Task.Run(async()=>{using(client){try{
            var stream=client.GetStream();if(Volatile.Read(ref stall)!=0){var one=new byte[1];if(await stream.ReadAsync(one,stopping.Token)!=0)throw new InvalidDataException("Unexpected stalled-auth input.");return;}
            using var reader=new StreamReader(stream,Encoding.UTF8,false,1024,true);
            await stream.WriteAsync("lsg-test\ncram=t7kNhwj4xqR0QYjzFKtBEG2ec2pXJ4FK\n"u8.ToArray(),stopping.Token);
            if(await reader.ReadLineAsync(stopping.Token)is null)return;
            await stream.WriteAsync("success=1|session_id=5\n"u8.ToArray(),stopping.Token);
            int subscriptions=0;string? line;while((line=await reader.ReadLineAsync(stopping.Token))is not null&&line!="start_session"){subscriptions++;}
            if(line is null)return;if(subscriptions!=2)throw new InvalidDataException("Scope fixture expects trade and quote subscriptions.");
            await stream.WriteAsync(wire,stopping.Token);if(await reader.ReadLineAsync(stopping.Token)is not null)throw new InvalidDataException("Expected scope socket EOF.");
        }catch(IOException){/* Windows may report reset instead of EOF during timeout. */}finally{Interlocked.Increment(ref closed);}}}));
    }}catch(OperationCanceledException)when(stopping.IsCancellationRequested){}
});
await using var runtime=new NativeMarketDataRuntime(library,2);
runtime.ConfigureLive(new(2026,10,26),Guid.NewGuid(),DateTimeOffset.UnixEpoch,false,64);
var budget=TimeSpan.FromSeconds(3);NativeSubscription[] subscriptions=[new(["ESZ6"],"trades","raw_symbol"),new(["ESZ6"],"mbp-1","raw_symbol")];
NativeRegistration[] Registration(string id,bool option=true)=>[new(42,id,"ESZ6",option,null,false,true)];
Task Start(string scope,Guid generation)=>runtime.StartOptionScopeAsync(scope,new('a',64),generation,new('x',32),"127.0.0.1",port,["127.0.0.1:1"],subscriptions,Registration("ES20261218C6000"),true,budget);
async Task<LivePublication> Quoted(string scope){using var deadline=new CancellationTokenSource(budget);while(true){var value=runtime.ReadOptionScopeState(scope,42,3);if((value.Flags&2)!=0)return value;await runtime.WaitForLiveChangesAsync(deadline.Token);}}
try{
    if(runtime.ReadOptionOwnerGeneration()!=Guid.Empty)throw new InvalidDataException("Query created an options owner.");
    await runtime.StartLiveLaneAsync(MarketDataLane.FuturesGlbx,new('x',32),"127.0.0.1",port,["127.0.0.1:1"],subscriptions,Registration("ES20261218",false),true,budget);
    var futures=runtime.ReadLiveStatus(MarketDataLane.FuturesGlbx).Generation;
    await runtime.PrepareOptionOwnerAsync(Guid.Empty,budget);var generation=runtime.ReadOptionOwnerGeneration();if(generation==Guid.Empty)throw new InvalidDataException("Missing prepared owner generation.");
    await Start("chain",generation);var initial=await Quoted("chain");var old=(initial.Source.GenerationLow,initial.Source.GenerationHigh);
    if(runtime.ReadOptionScopeStatus("chain").Phase!=3||runtime.ReadOptionScopeFailure()!=null)throw new InvalidDataException("Prepared scope did not expose current lifecycle health.");
    var unicode=new string('界',100);await Start(unicode,generation);var other=await Quoted(unicode);var otherGeneration=(other.Source.GenerationLow,other.Source.GenerationHigh);
    var count=Volatile.Read(ref accepted);await Start("chain",generation);if(Volatile.Read(ref accepted)!=count)throw new InvalidDataException("Identical acquisition opened another socket.");
    try{await runtime.StartOptionScopeAsync("chain",new('a',64),generation,new('x',32),"127.0.0.1",port,["127.0.0.1:1"],subscriptions,Registration("ES20261218P6000"),true,budget);throw new InvalidDataException("Changed physical intent reused a cached acquisition.");}catch(InvalidOperationException){}
    await runtime.ResetOptionScopeAsync("chain",generation,budget);var reset=await Quoted("chain");if((reset.Source.GenerationLow,reset.Source.GenerationHigh)==old)throw new InvalidDataException("Scope reset reused its physical generation.");
    var unchanged=runtime.ReadOptionScopeState(unicode,42,3);if((unchanged.Source.GenerationLow,unchanged.Source.GenerationHigh)!=otherGeneration)throw new InvalidDataException("Scope reset replaced its sibling.");
    var publications=new LivePublication[16];if(runtime.DrainOptionScopePublications("chain",publications)!=2||publications[0].Source.Kind!=2||publications[1].Source.Kind!=1)throw new InvalidDataException("Scoped contributions lost or reordered.");
    if(!reset.TryGetMarketPrice("ES20261218C6000",AssetTypeId.FuturesOption,out var price)||price.Quote?.BidPrice!=6000m||price.Trade?.LastPrice!=6000.25m)throw new InvalidDataException("Scoped fixed snapshot differs from source values.");
    await runtime.ReleaseOptionScopeAsync("chain",generation,budget);if(runtime.ReadOptionScopeState("chain",42,3).Flags!=0)throw new InvalidDataException("Released scope returned old data.");
    await runtime.PrepareOptionOwnerAsync(generation,budget);var replacement=runtime.ReadOptionOwnerGeneration();if(replacement==generation||replacement==Guid.Empty)throw new InvalidDataException("Owner replacement did not advance generation.");
    await Start("chain",replacement);await Quoted("chain");await runtime.ReleaseOptionScopeAsync("chain",generation,budget);if(runtime.ReadOptionScopeState("chain",42,3).Flags==0)throw new InvalidDataException("Delayed old-generation release removed replacement data.");
    Volatile.Write(ref stall,1);
    try{await runtime.StartOptionScopeAsync("timeout",new('a',64),replacement,new('x',32),"127.0.0.1",port,["127.0.0.1:1"],subscriptions,Registration("ES20261218C6000"),true,TimeSpan.FromMilliseconds(300));throw new InvalidDataException("Stalled scope unexpectedly recovered.");}catch(TimeoutException){}
    Volatile.Write(ref stall,0);await Start("timeout",replacement);await Quoted("timeout");
    await runtime.StopOptionOwnerAsync(replacement,budget);if(runtime.ReadOptionScopeState("chain",42,3).Flags!=0||runtime.ReadOptionScopeState("timeout",42,3).Flags!=0)throw new InvalidDataException("Stopped subtree retained current data.");
    var diagnostics=runtime.DrainLiveDiagnostics();
    if(diagnostics.Count(d=>d.Message.EndsWith("complete test warning",StringComparison.Ordinal))!=6
        ||diagnostics.Count(d=>d.Message.StartsWith("[scope=",StringComparison.Ordinal))!=15
        ||!diagnostics.Any(d=>d.Message.Contains(unicode,StringComparison.Ordinal)))
        throw new InvalidDataException("Scope cleanup/replacement discarded unread diagnostics or scope correlation.");
    if(runtime.ReadLiveStatus(MarketDataLane.FuturesGlbx).Generation!=futures||runtime.ReadLiveStatus(MarketDataLane.FuturesGlbx).Phase!=3)throw new InvalidDataException("Options control reset primary futures.");
    await runtime.StopAndDisposeAsync(budget);
    await Task.WhenAll(peers).WaitAsync(budget);
    if(Volatile.Read(ref accepted)!=Volatile.Read(ref closed))throw new InvalidDataException("Successful shutdown left provider peers open.");
    Console.WriteLine("PASS: DLL scoped prepare/install/reuse/reset/release/replace; stale-generation release protection, UTF-16 scope names, coherent ordered snapshots, auth timeout then fresh acquisition, options-only shutdown and futures isolation.");
}
finally{stopping.Cancel();listener.Stop();await server;await Task.WhenAll(peers);}
