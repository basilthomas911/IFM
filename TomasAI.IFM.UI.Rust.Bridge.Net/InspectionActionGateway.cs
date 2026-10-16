namespace TomasAI.IFM.UI.Rust.Bridge.Net;
[MessagePack.MessagePackObject]
public sealed record InspectionPublication([property: MessagePack.Key("schema")] int Schema, [property: MessagePack.Key("generation")] long Generation, [property: MessagePack.Key("status")] string Status, [property: MessagePack.Key("busy")] bool Busy, [property: MessagePack.Key("result")] InspectionResult Result, [property: MessagePack.Key("outcomes")] EditorOutcome[] Outcomes);
/// <summary>Single-flight acknowledged read operations. Work is performed off the native/UI thread.</summary>
public sealed class InspectionActionGateway : IAsyncDisposable
{
 readonly object gate=new();readonly Func<InspectionIntent,Task<InspectionResult>> execute;
 readonly Dictionary<Guid,(byte[] Payload,EditorOutcome Outcome)> requests=[];
 readonly Queue<Guid> completedReads=new();
 InspectionResult result=new("",[]);long generation=1;string status="Not loaded";bool busy;Task pending=Task.CompletedTask;
 public InspectionActionGateway(Func<InspectionIntent,Task<InspectionResult>> execute)=>this.execute=execute;
 readonly AsyncSessionOwner owner=new();
 readonly PublicationCache<InspectionPublication> wire=new();
 InspectionPublication? cached;
 public InspectionPublication Snapshot(){lock(gate){if(cached?.Generation!=generation)cached=new(1,generation,status,busy,result,requests.Values.Select(x=>x.Outcome).ToArray());return cached;}}
 public byte[] WireSnapshot()=>wire.Read(Snapshot());
 public ValueTask DisposeAsync()=>owner.DisposeAsync();
 public int Submit(InspectionIntent input)
 {
  if(input.Schema!=1||input.RequestId==Guid.Empty||input.Fields is null||input.Fields.Count>16||input.Fields.Any(x=>x.Key.Length>64||x.Value is null||x.Value.Length>2048)||input.View is not ("observation" or "risk")||input.Intent is not ("history" or "open" or "next")||(input.View=="observation"&&input.Intent=="next"))return -1;
  var payload=BridgeDocument.Encode(new{input.View,input.Intent,Fields=input.Fields.OrderBy(x=>x.Key,StringComparer.Ordinal).ToArray()});
  lock(gate)
  {
   if(requests.TryGetValue(input.RequestId,out var old))return old.Payload.AsSpan().SequenceEqual(payload)?0:-1;
   if(busy||requests.Count>=256)return -4;
   requests.Add(input.RequestId,(payload,new(input.RequestId,"Pending","Loading")));busy=true;status="Loading";generation++;
   pending=owner.Enqueue(async()=>{try{var loaded=await execute(input);lock(gate){if(input.Intent=="open"&&result.View==loaded.View)loaded=loaded with{Rows=result.Rows,PagingState=result.PagingState};result=loaded;status=loaded.Rows.Length==0&&loaded.Detail is null&&loaded.Decision.Length==0?"No history for these filters":"Loaded";requests[input.RequestId]=(payload,new(input.RequestId,"Completed","Loaded"));}}catch(Exception e){lock(gate){status=e.Message;requests[input.RequestId]=(payload,new(input.RequestId,"Failed",e.Message));}}finally{lock(gate){completedReads.Enqueue(input.RequestId);while(completedReads.Count>32)requests.Remove(completedReads.Dequeue());busy=false;generation++;}}});return 0;
  }
 }
 public Task DrainAsync(){lock(gate)return pending;}
}
