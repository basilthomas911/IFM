using System.Globalization;
using TomasAI.IFM.UI.Net.Models.Operations;
using TomasAI.IFM.UI.Net.Services.Operations;
using TomasAI.IFM.UI.Net.ViewModels.App;
using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;
namespace TomasAI.IFM.UI.Rust.Bridge.Net;
/// <summary>Immutable actor explorer snapshots; lifecycle commands share the WinForms generation/authority model.</summary>
public sealed class ActorHealthSession : IAsyncDisposable
{
    readonly RustClientServiceScope? scope;
    readonly IActorHealthQueryService service;
    readonly ActorHealthControlModel controls;
    ActorHealthSnapshot? snapshot;
    Dictionary<string,(ActorHealthThreadId Target,long Generation)> targets=[];
    bool started;
    DateTime from=DateTime.UtcNow.AddHours(-1),nextRefresh;
    public bool RefreshDue => DateTime.UtcNow>=nextRefresh;
    public ActorHealthSession(){scope=new(Environment.GetEnvironmentVariable("IFM_UI_NATS_URL")??"nats://localhost:4222");service=scope.Container.GetInstance<IActorHealthQueryService>();controls=new(scope.Container.GetInstance<ISupervisorCommandApi>(),scope.Container.GetInstance<SupervisorControlOptions>());}
    public ActorHealthSession(IActorHealthQueryService service,ISupervisorCommandApi commands,SupervisorControlOptions options){this.service=service;controls=new(commands,options);}
    public async Task<ContractEditorResult> ExecuteAsync(ContractEditorIntent input)
    {
        string? outcome=null;
        if(input.Intent=="load"){if(scope is not null&&!started){await scope.StartAsync();started=true;}if(input.Fields.TryGetValue("fromUtc",out var value)&&!string.IsNullOrWhiteSpace(value)){if(!DateTime.TryParse(value,CultureInfo.InvariantCulture,DateTimeStyles.AdjustToUniversal|DateTimeStyles.AssumeUniversal,out from))throw new ArgumentException("From must be a UTC date/time.");}}
        else if(input.Intent=="supervise"){
            if(snapshot is null||input.OriginalId is null||!targets.TryGetValue(input.OriginalId,out var target))throw new InvalidOperationException("Select a current mailbox or incident.");
            if(!Enum.TryParse<SupervisorActorOperationKind>(input.Fields.GetValueOrDefault("operation"),out var operation)||!Enum.IsDefined(operation))throw new ArgumentException("Unsupported Supervisor operation.");
            using var deadline=new CancellationTokenSource(TimeSpan.FromMinutes(10));outcome=await controls.ExecuteAsync(snapshot,target.Target,target.Generation,operation,input.Fields.GetValueOrDefault("reason")??"",deadline.Token);
        }else throw new ArgumentException("Unsupported actor health action.");
        var result=await RefreshAsync();if(outcome is not null)result.Context!["outcome"]=outcome;return result;
    }
    public async Task<ContractEditorResult> RefreshAsync()
    {
        using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(10));var result=await service.GetAsync(from,DateTime.UtcNow,deadline.Token);nextRefresh=DateTime.UtcNow.AddSeconds(15);snapshot=result.IsSuccess?result.Value:null;targets.Clear();
        if(snapshot is null)return new("actor-health",[],[],Context:new(){["summary"]="Actor health unavailable: "+(result.Error?.Message??"No snapshot returned"),["status"]="Red",["controls"]="false"});
        var rows=new List<ContractEditorRow>();
        foreach(var actor in snapshot.Actors.OrderBy(x=>x.Domain).ThenBy(x=>x.ActorId.Name)){
            var parent=$"actor:{actor.ActorId.ActorType}:{actor.ActorId.Name}";Add(parent,actor.ActorId.Name,actor.Domain,actor,"actor",actor.Status.ToString());
            foreach(var mailbox in actor.Mailboxes.OrderBy(x=>x.ThreadId.EntityId)){var id=$"mailbox:{actor.ActorId.ActorType}:{actor.ActorId.Name}:{mailbox.ThreadId.EntityId}";Add(id,mailbox.ThreadId.EntityId,parent,mailbox,"mailbox",mailbox.IsProcessing||mailbox.QueueDepth>0?"Yellow":mailbox.LastFailedUtc is not null&&(mailbox.LastCompletedUtc is null||mailbox.LastFailedUtc>=mailbox.LastCompletedUtc)?"Red":"Green");targets[id]=(mailbox.ThreadId,mailbox.Generation);}}
        foreach(var x in snapshot.Failures)Add("failure:"+x.FailureId,x.ActorId.Name+"."+x.Verb,"Failure history",x,"failure","Red");
        foreach(var x in snapshot.Projectors)Add("projector:"+x.ActorName+":"+x.ProjectorName,x.ActorName+" / "+x.ProjectorName,"Event projectors",x,"projector",x.IsReady?"Green":"Red");
        foreach(var x in snapshot.Incidents){var id=$"incident:{x.ThreadId.ActorType}:{x.ThreadId.Name}:{x.ThreadId.EntityId}";Add(id,x.ThreadId.Name+"."+x.ThreadId.EntityId,"Active incidents",x,"incident",x.Health=="Degraded"?"Yellow":"Red");targets[id]=(x.ThreadId,x.Generation);}
        foreach(var x in snapshot.Operations)Add("operation:"+x.OperationId,x.StartedUtc.ToString("u")+" "+x.ThreadId.Name,"Lifecycle operations",x,"operation",x.Outcome=="Succeeded"?"Green":"Yellow");
        foreach(var x in snapshot.History)Add("history:"+x.Revision,x.ObservedUtc.ToString("u"),"Health history",x,"history",x.CriticalActors>0?"Red":x.DegradedActors>0?"Yellow":"Green");
        foreach(var x in snapshot.Workers)Add("worker:"+x.WorkerId,"Worker "+x.WorkerId,"Shared workers",x,"worker",x.IsFaulted?"Red":"Green");
        Add("collection","Collection","Health collection",snapshot.Collection,"collection",snapshot.OverallStatus.ToString());Add("polling","Polling","Health collection",snapshot.Polling,"polling",snapshot.Polling.IsAlive?"Green":"Red");
        return new("actor-health",rows.ToArray(),new(){["operations"]=ActorHealthControlModel.Operations.Select(x=>x.ToString()).ToArray()},Context:new(){["summary"]=$"{snapshot.OverallStatus} | Actors {snapshot.RunningActorCount}/{snapshot.ActorCount} running | Messages waiting {snapshot.QueuedMessageCount} | Observed {snapshot.ObservedUtc:u}",["status"]=snapshot.OverallStatus.ToString(),["controls"]=controls.AuthorityAvailable(snapshot)?"true":"false",["fromUtc"]=from.ToString("O"),["observation"]=controls.AuthorityAvailable(snapshot)?"Controlled; refreshes every 15 seconds.":"Observation only; refreshes every 15 seconds."});
        void Add(string id,string name,string parent,object value,string kind,string status){var fields=new Dictionary<string,string>();BridgeDocument.Flatten(value,fields,enumNames:true);fields["name"]=name;fields["parent"]=parent;fields["kind"]=kind;fields["status"]=status;fields["canExecute"]=kind is "mailbox" or "incident" ? "true":"false";rows.Add(new(id,fields));}
    }
    public async ValueTask DisposeAsync(){if(service is IDisposable disposable)disposable.Dispose();if(scope is not null)await scope.DisposeAsync();}
}
