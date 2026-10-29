using System.Collections.Immutable;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.MarketData.Subscriptions.Persistence;
using TomasAI.IFM.Application.MarketData.Rust.Host;
using TomasAI.IFM.Application.MarketData.Rust.Persistence;
var pendingRows=new List<DurableSubscriptionOutboxItem>();var acknowledged=0;var rejectAcknowledgement=false;
var revision=1L;var count=10000;var hold=false;var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var resume=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
var journal=Proxy.Create<IDurableSubscriptionIntentStore>((m,a)=>m.Name switch {
 "ReadAsync"=>Read((CancellationToken)a![2]!),
 "ReadPendingOutboxAsync"=>Task.FromResult<IReadOnlyList<DurableSubscriptionOutboxItem>>(pendingRows.Take((int)a![2]!).ToArray()),
 "AcknowledgeOutboxAsync"=>Acknowledge((Guid)a![2]!),
 _=>throw new Exception("Unexpected journal operation: "+m.Name)});
Task<bool> Acknowledge(Guid id){if(rejectAcknowledgement)return Task.FromResult(false);var removed=pendingRows.RemoveAll(row=>row.TransitionId==id);acknowledged+=removed;return Task.FromResult(removed==1);}

async Task<DurableSubscriptionSnapshot> Read(CancellationToken token){if(hold){entered.TrySetResult();await resume.Task.WaitAsync(token);}return new(1,"IFM","GLBX.MDP3",revision,Enumerable.Range(0,count).Select(i=>new DurableAuthorityState("terminal:"+i,1,new Guid(i+1,0,0,new byte[8]),new string('a',64),new("fixture","workflow-"+i,"leg"),DurableAuthorityStatus.Terminal,"Completed",[])).ToArray());}
var plans=Proxy.Create<ICompositionRoutePlanStore>((m,a)=>throw new Exception("An empty financial journal unexpectedly read a route plan."));
var clock=new MonotonicClock();var source=new NativeJournalFacts(journal,plans,clock);var storage=new NativeReferenceStorage(new Facts(source));ulong next=0;
JsonElement Query(object input)=>JsonSerializer.SerializeToElement(input);
async Task<byte[]> Transfer(JsonElement arguments,CancellationToken token=default){
 var id=++next;var bytes=JsonSerializer.SerializeToUtf8Bytes(arguments);using var output=new MemoryStream();uint total=0;string? digest=null;
 while(true){var page=await storage.ReadAsync(new(id,9,bytes,checked((uint)output.Length)),token);total=page.Total;digest??=page.Digest;
  if(page.Offset!=output.Length||page.Bytes.Length>4096||page.Digest!=digest)throw new Exception("Raw journal page continuity failed.");output.Write(page.Bytes);if(output.Length==total)break;}
 var result=output.ToArray();if(Convert.ToHexString(SHA256.HashData(result))!=digest)throw new Exception("Raw journal transfer digest failed.");return result;
}
var begin=Query(new{authority="IFM",dataset="GLBX.MDP3"});
var first=JsonDocument.Parse(await Transfer(begin)).RootElement;var id=first.GetProperty("id").GetString();var total=first.GetProperty("total").GetInt32();
if(total>32*1024*1024||first.GetProperty("page_bytes").GetInt32()!=32768)throw new Exception("Unbounded raw journal source.");
revision=2;using(var captured=new MemoryStream()){
 for(var offset=0;offset<total;offset+=32768){var page=await Transfer(Query(new{snapshot=id,offset}));if(page.Length!=Math.Min(32768,total-offset))throw new Exception("Raw journal page size failed.");captured.Write(page);}
 var all=captured.ToArray();if(Convert.ToHexString(SHA256.HashData(all))!=first.GetProperty("digest").GetString())throw new Exception("Full journal digest changed during paging.");
 var copied=JsonDocument.Parse(all).RootElement;if(copied.GetProperty("revision").GetInt64()!=1||copied.GetProperty("authorities").GetArrayLength()!=10000)throw new Exception("Immutable complete journal was truncated or reread.");
}
foreach(var offset in new[]{-1,1,checked(((total+32767)/32768)*32768)}){try{await Transfer(Query(new{snapshot=id,offset}));throw new Exception("Invalid journal offset accepted.");}catch(InvalidDataException){}}
clock.Advance(119);await Transfer(Query(new{snapshot=id,offset=0}));clock.Advance(1);
try{await Transfer(Query(new{snapshot=id,offset=0}));throw new Exception("Journal snapshot survived exact two-minute expiry.");}catch(InvalidDataException){}
count=0;var empty=JsonDocument.Parse(await Transfer(begin)).RootElement;await Transfer(Query(new{snapshot=empty.GetProperty("id").GetString(),offset=0}));
count=10001;try{await Transfer(begin);throw new Exception("Oversized journal accepted.");}catch(ArgumentException){}
count=1;hold=true;using(var cancellation=new CancellationTokenSource()){
 var pending=Transfer(begin,cancellation.Token);await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
 try{await Transfer(begin);throw new Exception("Concurrent unbounded journal capture accepted.");}catch(InvalidOperationException){}
 cancellation.Cancel();try{await pending;throw new Exception("Cancelled journal capture completed.");}catch(OperationCanceledException){}
}
resume.TrySetResult();hold=false;await Transfer(begin);
var digest=DurableSubscriptionContract.Digest(DurableSubscriptionContract.Freeze(await Read(default)));
for(var i=0;i<100;i++)pendingRows.Add(new(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),revision,"terminal:0",1,DurableAuthorityStatus.Terminal,"Completed",DateTimeOffset.UnixEpoch));
var stale=JsonDocument.Parse(await Transfer(Query(new{action="pending",authority="IFM",dataset="GLBX.MDP3",revision=revision-1,digest}))).RootElement;
if(stale.GetProperty("accepted").GetBoolean()||acknowledged!=0)throw new Exception("Stale delivery watermark was accepted.");
var deliveryPage=JsonDocument.Parse(await Transfer(Query(new{action="pending",authority="IFM",dataset="GLBX.MDP3",revision,digest}))).RootElement;
if(!deliveryPage.GetProperty("accepted").GetBoolean()||deliveryPage.GetProperty("rows").GetArrayLength()!=100)throw new Exception("Bounded copied outbox rows were lost.");
var ids=pendingRows.Select(row=>row.TransitionId.ToString("N")).ToArray();
foreach(var invalid in new[]{Array.Empty<string>(),new[]{ids[0],ids[0]},new[]{"invalid-guid"},new[]{Guid.Empty.ToString("N")},Enumerable.Repeat(ids[0],101).ToArray()}){
 try{await Transfer(Query(new{action="ack",authority="IFM",dataset="GLBX.MDP3",revision,digest,ids=invalid}));throw new Exception("Invalid acknowledgement accepted.");}catch(InvalidDataException){}
}
if(acknowledged!=0)throw new Exception("Invalid input partially acknowledged business state.");
rejectAcknowledgement=true;try{await Transfer(Query(new{action="ack",authority="IFM",dataset="GLBX.MDP3",revision,digest,ids}));throw new Exception("Unconfirmed acknowledgement accepted.");}catch(IOException){}
if(pendingRows.Count!=100)throw new Exception("A failed storage write lost business delivery state.");rejectAcknowledgement=false;
var receipt=JsonDocument.Parse(await Transfer(Query(new{action="ack",authority="IFM",dataset="GLBX.MDP3",revision,digest,ids}))).RootElement;
if(!receipt.GetProperty("accepted").GetBoolean()||acknowledged!=100||pendingRows.Count!=0)throw new Exception("Explicit conditional acknowledgement batch failed.");
var drained=JsonDocument.Parse(await Transfer(Query(new{action="pending",authority="IFM",dataset="GLBX.MDP3",revision,digest}))).RootElement;
if(drained.GetProperty("rows").GetArrayLength()!=0)throw new Exception("Acknowledged business rows were replayed.");
Console.WriteLine("PASS raw conditional delivery: stale revision rejection, 100 copied rows over actual raw pages, empty/duplicate/invalid/101-ID rejection before writes, unconfirmed-write failure retained, successful batch acknowledgement and empty catch-up.");
if(typeof(NativeJournalFacts).GetFields(BindingFlags.Instance|BindingFlags.NonPublic).Any(field=>field.FieldType.Name.Contains("NativeMarketDataRuntime")))throw new Exception("Raw journal source retained a native resource.");
Console.WriteLine("PASS raw committed journal: 10000 immutable authorities through actual 4096-byte wrapper; 32768-byte source pages; full SHA-256; mutation isolation; exact two-minute expiry; empty/partial/invalid/oversized/cancelled/concurrent boundaries; no native resource or ownership realization.");
var statisticsRows=System.Collections.Immutable.ImmutableArray.Create(new TomasAI.IFM.Shared.MarketData.Contracts.OptionChainStatisticsSnapshot("put",long.MaxValue,new(2026,10,27),-1,new(2026,10,26)));
var statisticsStore=Proxy.Create<TomasAI.IFM.Application.MarketData.Contracts.IOptionChainStatisticsStore>((method,arguments)=>{((CancellationToken)arguments![3]!).ThrowIfCancellationRequested();return Task.FromResult(statisticsRows);});
var statisticsSource=new NativeStatisticsFacts(statisticsStore);var statisticsInput=Query(new{contract="ES20261218",date=new DateOnly(2026,10,27).DayNumber,expiry=new DateOnly(2026,11,10).DayNumber});
var statisticsBytes=await statisticsSource.ReadAsync(statisticsInput,default);using(var document=JsonDocument.Parse(statisticsBytes)){var row=document.RootElement[0];if(row[1].GetInt64()!=long.MaxValue||row[3].GetInt64()!=-1)throw new Exception("Managed raw statistics changed source values.");}
statisticsRows=Enumerable.Range(0,100001).Select(index=>new TomasAI.IFM.Shared.MarketData.Contracts.OptionChainStatisticsSnapshot(index.ToString(),null,null,null,null)).ToImmutableArray();
try{await statisticsSource.ReadAsync(statisticsInput,default);throw new Exception("Oversized statistics rows accepted.");}catch(InvalidDataException){}
statisticsRows=Enumerable.Range(0,10000).Select(index=>new TomasAI.IFM.Shared.MarketData.Contracts.OptionChainStatisticsSnapshot(new string('a',1024)+index,long.MaxValue,null,long.MaxValue,null)).ToImmutableArray();
try{await statisticsSource.ReadAsync(statisticsInput,default);throw new Exception("Oversized statistics bytes accepted.");}catch(InvalidDataException){}
using(var cancelled=new CancellationTokenSource()){cancelled.Cancel();try{await statisticsSource.ReadAsync(statisticsInput,cancelled.Token);throw new Exception("Cancelled statistics source accepted.");}catch(OperationCanceledException){}}
Console.WriteLine("PASS raw option statistics: unchanged signed extremes/nullable dates; 100001-row rejection; bounded pre-growth JSON encoding; cancellation; no native resources.");
sealed class MonotonicClock:TimeProvider{long timestamp;public override long TimestampFrequency=>1000000;public override long GetTimestamp()=>timestamp;public void Advance(int seconds)=>timestamp+=seconds*TimestampFrequency;}
public class Proxy:DispatchProxy{
 public Func<MethodInfo,object?[]?,object?> Handler=null!;
 public static T Create<T>(Func<MethodInfo,object?[]?,object?> handler)where T:class{var proxy=DispatchProxy.Create<T,Proxy>();((Proxy)(object)proxy).Handler=handler;return proxy;}
 protected override object? Invoke(MethodInfo? method,object?[]? args)=>Handler(method!,args);
}
sealed class Facts(NativeJournalFacts source):INativeReferenceFacts{
 public async Task<byte[]> ReadAsync(byte kind,ReadOnlyMemory<byte> arguments,CancellationToken token){if(kind!=9)throw new Exception("Unexpected raw journal kind.");using var input=JsonDocument.Parse(arguments);return await source.ReadAsync(input.RootElement,token);}
}
