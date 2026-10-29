using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using TomasAI.IFM.Application.MarketData.Rust.Api;
using TomasAI.IFM.Application.MarketData.Rust.Delivery;
using TomasAI.IFM.Shared.MarketData.Definitions;

// Exact DTO parity and bounded full-universe delivery through the real DLL.
// Every connection is localhost and credentials are fabricated.
using var corpus=JsonDocument.Parse(await File.ReadAllBytesAsync(args[1]));
await using var runtime=new NativeMarketDataRuntime(Path.GetFullPath(args[0]),2);
var count=0;
foreach(var item in corpus.RootElement.EnumerateArray()){
    var source=item.GetProperty("json").GetString()!;
    if(item.GetProperty("valid").GetBoolean()){
        var expected=ExactInstrumentDefinition.Parse("GLBX.MDP3",source);
        var actual=runtime.ProjectDefinition("GLBX.MDP3",source);
        if(JsonSerializer.Serialize(expected)!=JsonSerializer.Serialize(actual))throw new InvalidDataException($"Definition DTO mismatch at case {count}.");
    }else{
        try{runtime.ProjectDefinition("GLBX.MDP3",source);throw new Exception($"Invalid definition accepted at {count}.");}catch(InvalidDataException){}
    }
    count++;
}
var seed=corpus.RootElement[0].GetProperty("json").GetString()!;
var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();
using var stop=new CancellationTokenSource();var peers=new ConcurrentBag<Task>();var faults=new ConcurrentQueue<Exception>();
var coverage=true;var requests=new ConcurrentQueue<string>();
var server=Task.Run(async()=>{try{while(true){var client=await listener.AcceptTcpClientAsync(stop.Token);peers.Add(Task.Run(async()=>{using(client)try{
    using var stream=client.GetStream();using var reader=new StreamReader(stream,Encoding.UTF8,false,1024,true);
    var first=await reader.ReadLineAsync(stop.Token);if(first is null)return;
    var length=0;string? header;while((header=await reader.ReadLineAsync(stop.Token)) is {Length:>0})if(header.StartsWith("Content-Length:",StringComparison.OrdinalIgnoreCase))length=int.Parse(header.Split(':')[1]);
    var body=new char[length];var offset=0;while(offset<body.Length){var n=await reader.ReadAsync(body.AsMemory(offset),stop.Token);if(n==0)throw new EndOfStreamException();offset+=n;}requests.Enqueue(first+" "+new string(body));
    var meta=first.Contains("get_dataset_range",StringComparison.Ordinal);
    var range=Volatile.Read(ref coverage)?"\"schema\":{\"definition\":{\"start\":\"2026-10-20T00:00:00Z\",\"end\":\"2026-10-26T12:00:00Z\"}},":"";
    var bytes=Encoding.UTF8.GetBytes(meta?"{"+range+"\"start\":\"2026-10-20T00:00:00Z\",\"end\":\"2026-10-26T12:00:00Z\"}":"\uFEFF"+string.Join("\r\n",Enumerable.Repeat(seed,10000))+"\r\n");
    await stream.WriteAsync(Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Length: {bytes.Length}\r\nConnection: close\r\n\r\n"),stop.Token);await stream.WriteAsync(bytes,stop.Token);
}catch(IOException){}catch(OperationCanceledException)when(stop.IsCancellationRequested){}catch(Exception e){faults.Enqueue(e);}}));}}catch(OperationCanceledException)when(stop.IsCancellationRequested){}});
try{
    runtime.ConfigureHistory($"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/",new('x',32),["127.0.0.1:9"]);
    var provider=new NativeInstrumentDefinitionProvider(runtime,TimeSpan.FromSeconds(20));var rows=0;
    var expected=JsonSerializer.Serialize(ExactInstrumentDefinition.Parse("GLBX.MDP3",seed));
    await foreach(var row in provider.ReadLatestAsync("GLBX.MDP3")){
        if(JsonSerializer.Serialize(row)!=expected)throw new InvalidDataException("Stream changed original source or projection.");
        if(rows++==10)await Task.Delay(100); // Force bounded downstream backpressure.
    }
    if(rows!=10000)throw new InvalidDataException("Stream lost or duplicated rows.");
    if(!requests.Any(x=>x.Contains("symbols=ALL_SYMBOLS")&&x.Contains("start=2026-10-25")&&x.Contains("schema=definition")))throw new InvalidDataException("Definition request policy changed.");
    using(var cancel=new CancellationTokenSource()){
        await using var iterator=provider.ReadLatestAsync("GLBX.MDP3",cancel.Token).GetAsyncEnumerator();
        if(!await iterator.MoveNextAsync())throw new InvalidDataException("Cancellation fixture empty.");
        cancel.Cancel();try{await iterator.MoveNextAsync();throw new Exception("Canceled definition reader delivered data.");}catch(OperationCanceledException){}
    }
    Volatile.Write(ref coverage,false);
    try{await foreach(var _ in provider.ReadLatestAsync("GLBX.MDP3")){}throw new Exception("Missing schema coverage accepted.");}catch(InvalidOperationException){}
    Console.WriteLine($"DefinitionBridge: {count} exact parser cases, 10000 exact streamed rows, bounded stall, cancellation and missing coverage passed.");
}finally{stop.Cancel();listener.Stop();await server;await Task.WhenAll(peers);if(faults.TryDequeue(out var fault))throw fault;}
