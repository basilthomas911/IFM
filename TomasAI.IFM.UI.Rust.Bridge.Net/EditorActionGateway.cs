using System.Text.Json;
using TomasAI.IFM.UI.Net.Models.Reference;
namespace TomasAI.IFM.UI.Rust.Bridge.Net;
[MessagePack.MessagePackObject]
public sealed record EditorIntent([property: MessagePack.Key("schema")] int Schema, [property: MessagePack.Key("requestId")] Guid RequestId, [property: MessagePack.Key("view")] string View, [property: MessagePack.Key("intent")] string Intent, [property: MessagePack.Key("expectedGeneration")] long ExpectedGeneration, [property: MessagePack.Key("lookup")] LookupTypeUiModel? Lookup);
[MessagePack.MessagePackObject]
public sealed record EditorPublication([property: MessagePack.Key("schema")] int Schema, [property: MessagePack.Key("generation")] long Generation, [property: MessagePack.Key("status")] string Status, [property: MessagePack.Key("busy")] bool Busy, [property: MessagePack.Key("rows")] LookupTypeUiModel[] Rows, [property: MessagePack.Key("outcomes")] EditorOutcome[] Outcomes);
public sealed class EditorOutcomeUnknownException(string message):Exception(message);
[MessagePack.MessagePackObject]
public sealed record EditorOutcome([property: MessagePack.Key("requestId")] Guid RequestId, [property: MessagePack.Key("state")] string State, [property: MessagePack.Key("message")] string Message);
/// <summary>Bounded acknowledged editor boundary. Exact duplicate requests share their outcome; conflicting reuse is rejected.</summary>
public sealed class EditorActionGateway : IAsyncDisposable
{
 readonly object gate=new();
 readonly Func<EditorIntent,Task<LookupTypeUiModel[]>> execute;
 readonly Dictionary<Guid,(EditorIntent Intent,EditorOutcome Outcome)> requests=[];
 readonly Queue<Guid> completedReads=new();
 LookupTypeUiModel[] rows=[]; long generation=1; bool busy; string status="Not loaded"; Task pending=Task.CompletedTask;
 public EditorActionGateway(Func<EditorIntent,Task<LookupTypeUiModel[]>> execute)=>this.execute=execute;
 readonly AsyncSessionOwner owner=new();
 readonly PublicationCache<EditorPublication> wire=new();
 EditorPublication? cached;
 public EditorPublication Snapshot(){lock(gate){if(cached?.Generation!=generation)cached=new(1,generation,status,busy,rows.ToArray(),requests.Values.Select(x=>x.Outcome).ToArray());return cached;}}
 public byte[] WireSnapshot()=>wire.Read(Snapshot());
 public ValueTask DisposeAsync()=>owner.DisposeAsync();
 public int Submit(EditorIntent intent)
 {
  lock(gate)
  {
   if(intent.Schema!=1||intent.RequestId==Guid.Empty||intent.View!="lookup"||intent.Intent is not ("load" or "add" or "change" or "remove"))return -1;
   if(requests.TryGetValue(intent.RequestId,out var old))return old.Intent==intent?0:-1;
   if(busy||requests.Count>=256)return -4;
   if(intent.Intent!="load"&&(intent.ExpectedGeneration!=generation||intent.Lookup is not {IsValid:true}||intent.Lookup.OrderId<0))return -5;
   requests.Add(intent.RequestId,(intent,new(intent.RequestId,"Pending","Awaiting backend completion")));
   busy=true;status="Pending";generation++;
   pending=owner.Enqueue(async()=>
   {
    try{var loaded=await execute(intent);lock(gate){rows=loaded;status="Completed";requests[intent.RequestId]=(intent,new(intent.RequestId,"Completed","Backend completion received"));}}
    catch(Exception e){lock(gate){status=e.Message;requests[intent.RequestId]=(intent,new(intent.RequestId,e is EditorOutcomeUnknownException?"Unknown":"Failed",e.Message));}}
    finally{lock(gate){if(intent.Intent=="load"){completedReads.Enqueue(intent.RequestId);while(completedReads.Count>8)requests.Remove(completedReads.Dequeue());}busy=false;generation++;}}
   });
   return 0;
  }
 }
 public Task DrainAsync(){lock(gate)return pending;}
}
