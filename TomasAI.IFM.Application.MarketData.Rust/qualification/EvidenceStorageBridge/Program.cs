using MessagePack;
using System.Text.Json;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.MarketData.Rust.Persistence;

var cases=JsonDocument.Parse(await File.ReadAllTextAsync(args[0])).RootElement;
var content=cases[0].GetProperty("evidenceSuccess").GetBytesFromBase64();
var evidence=MessagePackSerializer.Deserialize<OptionTradeEvidence>(content);
var request=new NativeEvidenceWrite(17,evidence.Source.Identity,evidence.Source.SourceDigest,content);
var writer=new ControlledWriter();var storage=new NativeEvidenceStorage(writer);
var pending=storage.WriteAsync(request,CancellationToken.None).AsTask();
await writer.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
if(pending.IsCompleted)throw new InvalidDataException("Storage acknowledged a blocked writer.");
// This external consumer does not contain any native runtime reference. It can
// remain pending while the native dataset independently cancels/joins/rebuilds.
writer.Complete.TrySetResult();var receipt=await pending;
if(!receipt.Persisted||receipt.Receipt!=17||receipt.Identity!=request.Identity||receipt.Digest!=request.Digest)throw new InvalidDataException("Invalid durable storage receipt.");
int before=writer.Calls;
foreach(var bad in new[]{request with{Receipt=0},request with{Identity=new('a',64)},request with{Digest=new('b',64)},request with{Content=[]},request with{Content=new byte[131073]}}){
 try{await storage.WriteAsync(bad,CancellationToken.None);throw new Exception("Malformed evidence was accepted.");}catch(InvalidDataException){}
}
if(writer.Calls!=before)throw new InvalidDataException("Malformed evidence reached the writer.");
writer.Failure=new IOException("Injected external storage failure.");
try{await storage.WriteAsync(request,CancellationToken.None);throw new Exception("Failed storage was acknowledged.");}catch(IOException){}
writer.Failure=null;
using(var stopping=new CancellationTokenSource()){
 stopping.Cancel();try{await storage.WriteAsync(request,stopping.Token);throw new Exception("Cancelled storage was acknowledged.");}catch(OperationCanceledException){}
}
await File.WriteAllTextAsync(args[1],JsonSerializer.Serialize(new{request=MessagePackSerializer.Serialize(request),receipt=MessagePackSerializer.Serialize(receipt),negative=MessagePackSerializer.Serialize(receipt with{Persisted=false,Error="Durable option evidence persistence failed."})},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine("PASS: copied evidence contract, blocked-writer no-ack, immutable source identity, bounded input, failed/cancelled writer no-ack; exported exact C# handoff/receipt fixtures.");

sealed class ControlledWriter:IOptionTradeEvidenceWriter {
 public TaskCompletionSource Entered{get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
 public TaskCompletionSource Complete{get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
 public int Calls;public Exception? Failure;
 public async ValueTask WriteAsync(OptionTradeEvidence evidence,CancellationToken cancellationToken){
  cancellationToken.ThrowIfCancellationRequested();Interlocked.Increment(ref Calls);Entered.TrySetResult();
  await Complete.Task.WaitAsync(cancellationToken);if(Failure is not null)throw Failure;
 }
}
