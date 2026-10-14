using System.Text.Json;
namespace TomasAI.IFM.UI.Rust.Bridge.Net;
public sealed record InspectionPublication(int Schema,long Generation,string Status,bool Busy,InspectionResult Result,EditorOutcome[] Outcomes);
/// <summary>Single-flight acknowledged read operations. Work is performed off the native/UI thread.</summary>
public sealed class InspectionActionGateway
{
 readonly object gate=new();readonly Func<InspectionIntent,Task<InspectionResult>> execute;
 readonly Dictionary<Guid,(string Payload,EditorOutcome Outcome)> requests=[];
 InspectionResult result=new("",[]);long generation=1;string status="Not loaded";bool busy;Task pending=Task.CompletedTask;
 public InspectionActionGateway(Func<InspectionIntent,Task<InspectionResult>> execute)=>this.execute=execute;
 public InspectionPublication Snapshot(){lock(gate)return new(1,generation,status,busy,result,requests.Values.Select(x=>x.Outcome).ToArray());}
 public int Submit(InspectionIntent input)
 {
  if(input.Schema!=1||input.RequestId==Guid.Empty||input.Fields is null||input.Fields.Count>16||input.Fields.Any(x=>x.Key.Length>64||x.Value is null||x.Value.Length>2048)||input.View is not ("observation" or "risk")||input.Intent is not ("history" or "open" or "next")||(input.View=="observation"&&input.Intent=="next"))return -1;
  var payload=JsonSerializer.Serialize(new{input.View,input.Intent,Fields=input.Fields.OrderBy(x=>x.Key,StringComparer.Ordinal)});
  lock(gate)
  {
   if(requests.TryGetValue(input.RequestId,out var old))return old.Payload==payload?0:-1;
   if(busy||requests.Count>=256)return -4;
   requests.Add(input.RequestId,(payload,new(input.RequestId,"Pending","Loading")));busy=true;status="Loading";generation++;
   pending=Task.Run(async()=>{try{var loaded=await execute(input);lock(gate){if(input.Intent=="open"&&result.View==loaded.View)loaded=loaded with{Rows=result.Rows,PagingState=result.PagingState};result=loaded;status=loaded.Rows.Length==0&&loaded.Detail is null&&loaded.Decision.Length==0?"No history for these filters":"Loaded";requests[input.RequestId]=(payload,new(input.RequestId,"Completed","Loaded"));}}catch(Exception e){lock(gate){status=e.Message;requests[input.RequestId]=(payload,new(input.RequestId,"Failed",e.Message));}}finally{lock(gate){busy=false;generation++;}}});return 0;
  }
 }
 public Task DrainAsync(){lock(gate)return pending;}
}
