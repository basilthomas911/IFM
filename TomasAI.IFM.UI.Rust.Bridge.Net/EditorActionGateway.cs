using System.Text.Json;
using TomasAI.IFM.UI.Net.Models.Reference;
namespace TomasAI.IFM.UI.Rust.Bridge.Net;
public sealed record EditorIntent(int Schema, Guid RequestId, string View, string Intent, long ExpectedGeneration, LookupTypeUiModel? Lookup);
public sealed record EditorPublication(int Schema, long Generation, string Status, bool Busy, LookupTypeUiModel[] Rows, EditorOutcome[] Outcomes);
public sealed class EditorOutcomeUnknownException(string message):Exception(message);
public sealed record EditorOutcome(Guid RequestId, string State, string Message);
/// <summary>Bounded acknowledged editor boundary. Exact duplicate requests share their outcome; conflicting reuse is rejected.</summary>
public sealed class EditorActionGateway
{
 readonly object gate=new();
 readonly Func<EditorIntent,Task<LookupTypeUiModel[]>> execute;
 readonly Dictionary<Guid,(EditorIntent Intent,EditorOutcome Outcome)> requests=[];
 LookupTypeUiModel[] rows=[]; long generation=1; bool busy; string status="Not loaded"; Task pending=Task.CompletedTask;
 public EditorActionGateway(Func<EditorIntent,Task<LookupTypeUiModel[]>> execute)=>this.execute=execute;
 public EditorPublication Snapshot(){lock(gate)return new(1,generation,status,busy,rows.ToArray(),requests.Values.Select(x=>x.Outcome).ToArray());}
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
   pending=Task.Run(async()=>
   {
    try{var loaded=await execute(intent);lock(gate){rows=loaded;status="Completed";requests[intent.RequestId]=(intent,new(intent.RequestId,"Completed","Backend completion received"));}}
    catch(Exception e){lock(gate){status=e.Message;requests[intent.RequestId]=(intent,new(intent.RequestId,e is EditorOutcomeUnknownException?"Unknown":"Failed",e.Message));}}
    finally{lock(gate){busy=false;generation++;}}
   });
   return 0;
  }
 }
 public Task DrainAsync(){lock(gate)return pending;}
}
