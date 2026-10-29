using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TomasAI.IFM.Application.MarketData.Rust.Api;
using TomasAI.IFM.Application.MarketData.Rust.Delivery;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Framework.MarketData.DataBento.Historical;
using TomasAI.IFM.Shared.MarketData.Contracts.Historical;

using var sdkOracle=JsonDocument.Parse(await File.ReadAllBytesAsync(args[2]));
var library=Path.GetFullPath(args[0]);var fixtures=Path.GetFullPath(args[1]);var at=new DateTimeOffset(2026,10,26,14,0,0,TimeSpan.Zero);var budget=TimeSpan.FromSeconds(20);
var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();var endpoint=$"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/";using var stopping=new CancellationTokenSource();var peers=new ConcurrentBag<Task>();var errors=new ConcurrentQueue<Exception>();var requests=new ConcurrentQueue<string>();var current="trades";
var jobResponse=sdkOracle.RootElement.EnumerateArray().Single(x=>x.GetProperty("source").GetString()=="0.00000012345678901234567").GetProperty("jobInput").GetString()!;
var downloadData=Enumerable.Range(0,5*1024*1024+17).Select(i=>(byte)(i*31)).ToArray();var downloadHash=Convert.ToHexStringLower(SHA256.HashData(downloadData));var downloadMode=0;

var server=Task.Run(async()=>{try{while(true){var client=await listener.AcceptTcpClientAsync(stopping.Token);peers.Add(Task.Run(async()=>{using(client)try{using var stream=client.GetStream();var head=new List<byte>();var one=new byte[1];while(!head.TakeLast(4).SequenceEqual(new byte[]{13,10,13,10})){await stream.ReadExactlyAsync(one,stopping.Token);head.Add(one[0]);if(head.Count>16384)throw new InvalidDataException("Oversize request.");}var text=Encoding.ASCII.GetString(head.ToArray());var length=text.Split("\r\n").Where(x=>x.StartsWith("Content-Length:",StringComparison.OrdinalIgnoreCase)).Select(x=>int.Parse(x.Split(':')[1])).DefaultIfEmpty().Single();var body=new byte[length];await stream.ReadExactlyAsync(body,stopping.Token);requests.Enqueue(text.Split("\r\n")[0]+" "+Encoding.UTF8.GetString(body));if(!text.Contains("Authorization: Basic ",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Historical credential header missing.");byte[] payload;
if(text.Contains("GET /download-fixture ",StringComparison.Ordinal))payload=downloadData;
else if(text.Contains("timeseries.get_range",StringComparison.Ordinal))payload=await File.ReadAllBytesAsync(Path.Combine(fixtures,Volatile.Read(ref current)+".dbn.zst"),stopping.Token);
else if(text.Contains("metadata.get_cost",StringComparison.Ordinal))payload="0.12345678901234567"u8.ToArray();
else if(text.Contains("metadata.get_billable_size",StringComparison.Ordinal))payload="12345678901234"u8.ToArray();
else if(text.Contains("metadata.get_record_count",StringComparison.Ordinal))payload="5000"u8.ToArray();
else if(text.Contains("batch.list_files",StringComparison.Ordinal)&&Volatile.Read(ref downloadMode)!=0)payload=JsonSerializer.SerializeToUtf8Bytes(new[]{new{filename="large.dbn.zst",size=downloadData.Length,hash="sha256:"+(Volatile.Read(ref downloadMode)==2?new string('0',64):downloadHash),urls=new Dictionary<string,string>{{"https",endpoint+"download-fixture"}}}});
else if(text.Contains("batch.list_files",StringComparison.Ordinal))payload="""[{"filename":"trades.dbn.zst","size":1234,"hash":"sha256:abc","urls":{"https":"https://fixture.invalid/file"}},{"filename":"ignored.json","size":0,"hash":"sha256:abc","urls":{}}]"""u8.ToArray();
else payload=Encoding.UTF8.GetBytes(Volatile.Read(ref jobResponse));
await stream.WriteAsync(Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Length: {payload.Length}\r\nConnection: close\r\n\r\n"),stopping.Token);await stream.WriteAsync(payload,stopping.Token);
}catch(IOException){}catch(OperationCanceledException)when(stopping.IsCancellationRequested){}catch(Exception e){errors.Enqueue(e);}}));}}catch(OperationCanceledException)when(stopping.IsCancellationRequested){}});
await using var runtime=new NativeMarketDataRuntime(library,2);runtime.ConfigureLive(new(2026,10,26),Guid.NewGuid(),at,false,8);runtime.ConfigureHistory(endpoint,new('x',32),["127.0.0.1:9"]);var native=new NativeHistoricalProvider(runtime,budget);var baseline=new DatabentoHistoricalProvider(new(){TimeoutMilliseconds=20000,MaximumBatchRecords=4096},new FixedClock(at));
HistoricalProviderRequest Request(HistoricalDataSchema schema)=>new(){Dataset="GLBX.MDP3",Symbols=["ESZ6"],Schema=schema,Symbology=HistoricalSymbology.RawSymbol,StartUtc=DateTimeOffset.UnixEpoch,EndUtc=at,RecordLimit=0,RequestHash="fixture"};
var cases=0;
try{
foreach(var (name,schema) in new[]{("trades",HistoricalDataSchema.Trades),("ohlcv",HistoricalDataSchema.OhlcvOneMinute)})foreach(var size in new[]{1,128,4096}){
    Volatile.Write(ref current,name);await using var original=await baseline.OpenFileAsync(Path.Combine(fixtures,name+".dbn"),schema,size,default);await using var candidate=await native.OpenRangeAsync(Request(schema),size,default);var count=0;
    while(true){var expected=await original.ReadNextAsync(default);var result=await candidate.ReadNextAsync(default).AsTask().WaitAsync(budget);if(JsonSerializer.Serialize(expected)!=JsonSerializer.Serialize(result))throw new InvalidDataException($"Historical batch differs: {name}, batch size {size}, ordinal {expected?.BatchOrdinal}.");if(result is null)break;count+=result.Records.Count;}if(count!=5000)throw new InvalidDataException("Historical rows were dropped or duplicated.");cases++;
}
foreach(var (name,schema) in new[]{("trades",HistoricalDataSchema.Trades),("ohlcv",HistoricalDataSchema.OhlcvOneMinute)})foreach(var suffix in new[]{".dbn",".dbn.zst"}){
    await using var original=await baseline.OpenFileAsync(Path.Combine(fixtures,name+suffix),schema,128,default);await using var candidate=await native.OpenFileAsync(Path.Combine(fixtures,name+suffix),schema,128,default);var count=0;
    while(true){var expected=await original.ReadNextAsync(default);var result=await candidate.ReadNextAsync(default).AsTask().WaitAsync(budget);if(JsonSerializer.Serialize(expected)!=JsonSerializer.Serialize(result))throw new InvalidDataException($"File decoder differs: {name}{suffix}, ordinal {expected?.BatchOrdinal}.");if(result is null)break;count+=result.Records.Count;}if(count!=5000)throw new InvalidDataException("File records were dropped or duplicated.");cases++;
}
await using(var original=await baseline.OpenFileAsync(Path.Combine(fixtures,"legacy-trades.dbz"),HistoricalDataSchema.Trades,128,default))await using(var candidate=await native.OpenFileAsync(Path.Combine(fixtures,"legacy-trades.dbz"),HistoricalDataSchema.Trades,128,default)){
    var expected=await original.ReadNextAsync(default);var result=await candidate.ReadNextAsync(default).AsTask().WaitAsync(budget);if(JsonSerializer.Serialize(expected)!=JsonSerializer.Serialize(result))throw new InvalidDataException("Legacy DBZ file differs from current provider.");cases++;
}
foreach(var (name,data) in new[]{("invalid-dbn.dbn",new byte[]{68,66,78,3,255,255,255,255,0,0,0,0}),("invalid-dbz.dbz",new byte[]{80,42,77,24,255,255,255,255,68,66,90,1}),("truncated.dbn",new byte[]{68,66,78,3})}){
    var path=Path.Combine(fixtures,name);await File.WriteAllBytesAsync(path,data);await using var reader=await native.OpenFileAsync(path,HistoricalDataSchema.Trades,128,default);
    try{await reader.ReadNextAsync(default).AsTask().WaitAsync(TimeSpan.FromSeconds(3));throw new InvalidDataException("Malformed file was accepted: "+name);}catch(InvalidOperationException error)when(error.Message.Contains("File decoder failed",StringComparison.Ordinal)){}cases++;
}
var estimate=await native.EstimateAsync(Request(HistoricalDataSchema.Trades),default);if(estimate.EstimatedCostUsd!=(decimal)0.12345678901234567||estimate.EstimatedBytes!=12345678901234||estimate.EstimatedRecords!=5000||estimate.EstimatedAtUtc!=at)throw new InvalidDataException("Historical estimate units/time differ.");cases++;
var expectedCost=sdkOracle.RootElement.EnumerateArray().Single(x=>x.GetProperty("source").GetString()=="0.00000012345678901234567").GetProperty("costLiteral").GetString()!;
var expectedJob=(HistoricalProviderJob)typeof(DatabentoHistoricalProvider).GetMethod("ParseJob",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static)!.Invoke(null,["{\"providerJobId\":\"job-123\",\"state\":\"Completed\",\"costUsd\":"+expectedCost+",\"recordCount\":5000,\"billedBytes\":1234,\"progressPercent\":100}"])!;
var job=await native.SubmitBatchAsync(Request(HistoricalDataSchema.Trades),default);var status=await native.GetBatchJobAsync("job-123",default);if(job!=status||job.ProviderJobId!="job-123"||job.State!=HistoricalProviderJobState.Completed||job.RecordCount!=5000||job!=expectedJob||!decimal.GetBits(job.CostUsd).SequenceEqual(decimal.GetBits(expectedJob.CostUsd)))throw new InvalidDataException("Historical job mapping differs. Expected="+JsonSerializer.Serialize(expectedJob)+"; actual="+JsonSerializer.Serialize(job)+"; status="+JsonSerializer.Serialize(status));cases+=2;
var files=await native.ListBatchFilesAsync("job-123",default);if(files.Count!=1||files[0]!=new HistoricalProviderFile("trades.dbn.zst","trades.dbn.zst",HistoricalDataSchema.Trades,1234,"abc"))throw new InvalidDataException("Historical immutable file filtering/mapping differs.");cases++;
foreach(var oracle in sdkOracle.RootElement.EnumerateArray()){
    Volatile.Write(ref jobResponse,oracle.GetProperty("jobInput").GetString()!);var literal=oracle.GetProperty("costLiteral").GetString()!;
    var expected=(HistoricalProviderJob)typeof(DatabentoHistoricalProvider).GetMethod("ParseJob",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static)!.Invoke(null,["{\"providerJobId\":\"job-123\",\"state\":\"Completed\",\"costUsd\":"+literal+",\"recordCount\":5000,\"billedBytes\":1234,\"progressPercent\":100}"])!;
    var result=await native.GetBatchJobAsync("job-123",default);if(result!=expected||!decimal.GetBits(result.CostUsd).SequenceEqual(decimal.GetBits(expected.CostUsd)))throw new InvalidDataException("Frozen SDK historical cost conversion differs: "+oracle.GetProperty("source").GetString());cases++;
}
var destination=Path.Combine(fixtures,"download-acceptance.dbn.zst");await File.WriteAllTextAsync(destination,"previous");Volatile.Write(ref downloadMode,1);
var descriptor=new HistoricalProviderFile("large.dbn.zst","large.dbn.zst",HistoricalDataSchema.Trades,downloadData.Length,downloadHash);
await native.DownloadBatchFileAsync("job-123",descriptor,destination,default).AsTask().WaitAsync(budget);if(!(await File.ReadAllBytesAsync(destination)).SequenceEqual(downloadData))throw new InvalidDataException("Streamed historical download bytes differ.");cases++;
Volatile.Write(ref downloadMode,2);var corrupt=descriptor with{Sha256=new string('0',64)};
try{await native.DownloadBatchFileAsync("job-123",corrupt,destination,default).AsTask().WaitAsync(budget);throw new InvalidDataException("Corrupt download was accepted.");}catch(InvalidOperationException error)when(error.Message.Contains("checksum mismatch",StringComparison.Ordinal)){}
if(!(await File.ReadAllBytesAsync(destination)).SequenceEqual(downloadData)||Directory.EnumerateFiles(fixtures,"*.partial").Any())throw new InvalidDataException("Failed download replaced the valid destination or retained a provisional file.");cases++;
Volatile.Write(ref downloadMode,0);
using(var cancel=new CancellationTokenSource()){
    await using var reader=await native.OpenRangeAsync(Request(HistoricalDataSchema.Trades),1,cancel.Token);await reader.ReadNextAsync(default).AsTask().WaitAsync(budget);cancel.Cancel();try{await reader.ReadNextAsync(default).AsTask().WaitAsync(budget);throw new InvalidDataException("Canceled historical reader succeeded.");}catch(OperationCanceledException){}cases++;
}
if(requests.Count(x=>x.Contains("batch.submit_job",StringComparison.Ordinal))!=1)throw new InvalidDataException("Paid historical submission was retried.");if(!errors.IsEmpty)throw new AggregateException(errors);
Console.WriteLine($"PASS historical DLL bridge: {cases} range/batch/cancellation cases, 50,000 rows and batch envelopes exactly match current C# + legacy DBN decoder; controls preserve source units and no paid retry.");
Console.WriteLine("Baseline DLL SHA256: "+Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory,"runtimes/win-x64/native/databento_feed_native.dll")))));
}finally{stopping.Cancel();listener.Stop();await server;await Task.WhenAll(peers);}
sealed class FixedClock(DateTimeOffset at):TimeProvider{public override DateTimeOffset GetUtcNow()=>at;}
