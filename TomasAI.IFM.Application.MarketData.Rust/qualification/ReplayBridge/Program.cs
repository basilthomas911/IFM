using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using TomasAI.IFM.Application.MarketData.Rust.Delivery;

var wire=await File.ReadAllBytesAsync(args[1]);var mode=0;
using var stop=new CancellationTokenSource();var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();var port=(ushort)((IPEndPoint)listener.LocalEndpoint).Port;
var peers=new ConcurrentBag<Task>();var faults=new ConcurrentQueue<Exception>();var subscriptions=new ConcurrentQueue<string>();int closed=0,opened=0;
var server=Task.Run(async()=>{try{while(true){var client=await listener.AcceptTcpClientAsync(stop.Token);Interlocked.Increment(ref opened);peers.Add(Task.Run(async()=>{using(client)try{
    var stream=client.GetStream();using var reader=new StreamReader(stream,Encoding.UTF8,false,1024,true);
    await stream.WriteAsync("lsg-test\ncram=t7kNhwj4xqR0QYjzFKtBEG2ec2pXJ4FK\n"u8.ToArray(),stop.Token);
    if(await reader.ReadLineAsync(stop.Token)is null)return;await stream.WriteAsync("success=1|session_id=5\n"u8.ToArray(),stop.Token);
    string? line;while((line=await reader.ReadLineAsync(stop.Token))is not null&&line!="start_session")subscriptions.Enqueue(line);
    if(line is null)return;
    var selected=Volatile.Read(ref mode);var payload=(byte[])wire.Clone();
    if(selected==3){var header=8+(int)BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(4,4));BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(header+4,4),99);}
    if(selected==1){await stream.WriteAsync(payload.AsMemory(0,4),stop.Token);}
    else{await stream.WriteAsync(selected==2?payload.AsMemory(0,payload.Length-256):payload,stop.Token);if(selected==2)return;}
    if(await reader.ReadLineAsync(stop.Token)is not null)throw new InvalidDataException("Replay did not close its SDK socket.");
}catch(IOException){}catch(OperationCanceledException)when(stop.IsCancellationRequested){}catch(Exception e){faults.Enqueue(e);}finally{Interlocked.Increment(ref closed);}}));}}catch(OperationCanceledException)when(stop.IsCancellationRequested){}});
await using var runtime=new NativeMarketDataRuntime(Path.GetFullPath(args[0]),2);runtime.ConfigureHistory("http://127.0.0.1:9/",new('x',32),["127.0.0.1:9"]);
var start=DateTimeOffset.FromUnixTimeSeconds(1_790_000_000);
TomasAI.IFM.Shared.MarketData.Contracts.Historical.IHistoricalRecordReader Open(TimeSpan budget,CancellationToken token=default)=>runtime.OpenFuturesReplay("GLBX.MDP3","ESZ6",new('x',32),"127.0.0.1",port,["127.0.0.1:9"],start,128,budget,token);
try{
    await using(var reader=Open(TimeSpan.FromSeconds(10))){var rows=0;while(await reader.ReadNextAsync(default)is{} batch){foreach(var row in batch.Records){rows++;if(row.SourceSequence!=rows||row.CloseOrPrice!=6000m+rows/1000000000m||row.VolumeOrSize!=rows%9+1||row.Symbol!="ESZ6"||row.PublisherId!="3")throw new InvalidDataException("Replay changed an ordered source row.");}if(rows==128)await Task.Delay(100);}if(rows!=10000)throw new InvalidDataException("Replay lost rows or consumed post-completion live data.");}
    var warnings=runtime.DrainHistoricalWarnings();if(warnings.Length!=3||warnings.All(x=>x.Text!="replay warning fixture"))throw new InvalidDataException("Replay provider notices were dropped.");
    foreach(var faultMode in new[]{1,2,3}){
        Volatile.Write(ref mode,faultMode);var failed=false;var began=System.Diagnostics.Stopwatch.GetTimestamp();
        try{await using var reader=Open(TimeSpan.FromMilliseconds(faultMode==1?150:3000));while(await reader.ReadNextAsync(default)is not null){}}
        catch(Exception e)when(e is InvalidOperationException or TimeoutException or OperationCanceledException){failed=true;}
        if(!failed||System.Diagnostics.Stopwatch.GetElapsedTime(began)>TimeSpan.FromSeconds(5))throw new InvalidDataException("Incomplete, stalled or unmapped replay claimed success or hung.");
    }
    Volatile.Write(ref mode,0);using(var cancel=new CancellationTokenSource()){
        await using var reader=Open(TimeSpan.FromSeconds(3),cancel.Token);if(await reader.ReadNextAsync(default)is null)throw new InvalidDataException("Cancel replay empty.");cancel.Cancel();
        try{await reader.ReadNextAsync(default);throw new InvalidDataException("Canceled replay delivered buffered rows.");}catch(OperationCanceledException){}
    }
    await runtime.StopAndDisposeAsync(TimeSpan.FromSeconds(3));await Task.WhenAll(peers).WaitAsync(TimeSpan.FromSeconds(3));
    if(opened!=closed)throw new InvalidDataException("Replay retained sockets.");if(faults.TryDequeue(out var fault))throw new InvalidDataException("Replay fixture failed.",fault);
    if(!subscriptions.Any(x=>x.Contains("schema=trades")&&x.Contains("symbols=ESZ6")&&x.Contains("start=")))throw new InvalidDataException("Replay request lost its source symbol/start.");
    Console.WriteLine("PASS read-only native replay: 10000 exact ordered rows, bounded consumer stall, explicit completion excludes live tail, all provider notices, metadata timeout, premature EOF, wrong mapping, cancellation and joined sockets; no primary lane or local ValueDate.");
}finally{stop.Cancel();listener.Stop();await server;try{await Task.WhenAll(peers);}catch(OperationCanceledException)when(stop.IsCancellationRequested){}}
