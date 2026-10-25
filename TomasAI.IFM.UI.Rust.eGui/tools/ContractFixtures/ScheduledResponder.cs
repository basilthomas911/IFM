using MessagePack;
using NATS.Client.Core;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Queries;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Events;
/// <summary>Typed in-memory admin endpoint. It never dispatches a task or calls a host.</summary>
internal sealed class ScheduledResponder{
 readonly Dictionary<Guid,ScheduledTaskDefinition> definitions=new(){{ScheduledFixtures.Id,ScheduledFixtures.Definition}};
 readonly Dictionary<Guid,ScheduledTaskRun> runs=new(){{ScheduledFixtures.RunId,ScheduledFixtures.Run},{ScheduledFixtures.UncertainId,ScheduledFixtures.Uncertain}};
 readonly HashSet<Guid> commands=[];
 public string Mode{get;set;}="Normal";
 public async Task<byte[]?> Handle(NatsConnection connection,string subject,byte[] payload,CancellationToken token){
  var options=MessagePackBinarySerializer.Options;var pieces=subject.Split('.');var verb=pieces[2];
  byte[] Result<T>(T value)=>MessagePackSerializer.Serialize(new ServiceResult<T>{Success=true,ErrorMessage="",Value=value},options);
  try{
   if(pieces[0]=="Query"){
    if(verb=="GetScheduledTasksDashboard"){
     var query=MessagePackSerializer.Deserialize<GetScheduledTasksDashboardQuery>(payload,options);Validate(query.Subject,query.EntityId,query.Environment,query.HostId);
     if(Mode=="SlowRefresh")return null;
     var rows=definitions.Values.ToArray();if(Mode=="BadHost")rows=rows.Select(row=>row with{Schedule=row.Schedule with{HostId="other-host"}}).ToArray();
     return Result(ScheduledFixtures.Dashboard with{Schedules=rows});
    }
    if(verb=="PreviewScheduledTaskSchedule"){
     var query=MessagePackSerializer.Deserialize<PreviewScheduledTaskScheduleQuery>(payload,options);Validate(query.Subject,query.EntityId,query.Environment,query.HostId);
     if(query.PageSize!=50)throw new InvalidDataException("Preview must preserve the C# query default page size");
     return Result(new ScheduledTaskSchedulePreview{Valid=query.Schedule.Expression!="invalid",Errors=query.Schedule.Expression=="invalid"?["Invalid cron"]:[],NextFireTimesUtc=query.Schedule.Expression=="invalid"?[]:[ScheduledFixtures.Fire]});
    }
    if(verb=="GetScheduledTaskRunHistory"){
     var query=MessagePackSerializer.Deserialize<GetScheduledTaskRunHistoryQuery>(payload,options);Validate(query.Subject,query.EntityId,query.Environment,query.HostId);
     if(query.PageSize!=100)throw new InvalidDataException("Incorrect bounded task history size");
     var rows=runs.Values.Where(run=>run.ScheduleId==query.ScheduleId).OrderBy(run=>run.IntendedFireTimeUtc).ToArray();
     var page=query.PagingState is null?rows.Take(1).ToArray():rows.Skip(1).ToArray();
     return Result(new ScheduledTaskRunPage{Runs=page,PagingState=query.PagingState is null&&rows.Length>1||Mode=="RepeatHistory"?[1]:null});
    }
    if(verb=="GetScheduledTaskOutput"){
     var query=MessagePackSerializer.Deserialize<GetScheduledTaskOutputQuery>(payload,options);Validate(query.Subject,query.EntityId,query.Environment,query.HostId);
     if(query.PageSize!=50)throw new InvalidDataException("Output must preserve the C# query default page size");
     if(!runs.TryGetValue(query.EntityId.Value,out var run)||query.ScheduleId!=run.ScheduleId||query.IntendedFireTimeUtc!=run.IntendedFireTimeUtc)throw new InvalidDataException("Output identity crossed a run");
     return Result(new ScheduledTaskOutputPage{Text=query.Offset==0?"stdout-1\n":"stdout-2",NextOffset=query.Offset==0?9:Mode=="NoOutputAdvance"?query.Offset:17,EndOfOutput=query.Offset!=0&&Mode!="NoOutputAdvance",Available=Mode!="MissingOutput"});
    }
    throw new InvalidDataException("Unknown scheduled query");
   }
   var type=typeof(GetScheduledTasksDashboardQuery).Assembly.GetType($"TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Commands.{verb}Command")??throw new InvalidDataException("Unknown task command");
   var value=MessagePackSerializer.Deserialize(type,payload,options);var command=(ICommand<ScheduledTaskId>)value;
   T Get<T>(string name)=>(T)type.GetProperty(name)!.GetValue(value)!;
   if(command.Subject.ToString()!=subject||pieces[3]!=command.EntityId.Format()||command.CommandId==Guid.Empty||Get<string>("Operator").Length==0||!commands.Add(command.CommandId))throw new InvalidDataException("Invalid or duplicate scheduled submission");
   // Compare omitted fields with the actual C# type's initializers. A typed
   // deserialize alone permits null strings/zero defaults that the hybrid
   // builder never sends, so it is not sufficient compatibility evidence.
   var explicitFields=new HashSet<string>{"CommandId","Subject","EntityId","ExpectedRevision","Operator","Reason","OperationCommandId"};
   foreach(var name in verb switch {
    "CreateScheduledTask" or "ChangeScheduledTaskSchedule"=>new[]{"Schedule"},
    "RequestScheduledTaskRun"=>new[]{"ScheduleId","DefinitionRevision","IntendedFireTimeUtc","Manual","TaskKey","HostId","Environment"},
    "FailScheduledTaskRun"=>new[]{"FinishedAtUtc","Stage","Detail"},_=>Array.Empty<string>()})explicitFields.Add(name);
   var defaults=Activator.CreateInstance(type)!;
   foreach(var property in type.GetProperties().Where(p=>Attribute.IsDefined(p,typeof(KeyAttribute))&&!explicitFields.Contains(p.Name))){
    var actual=MessagePackSerializer.Serialize(property.PropertyType,property.GetValue(value),MessagePackBinarySerializer.ContentOptions);
    var expected=MessagePackSerializer.Serialize(property.PropertyType,property.GetValue(defaults),MessagePackBinarySerializer.ContentOptions);
    if(!actual.AsSpan().SequenceEqual(expected))throw new InvalidDataException($"Scheduled {verb}.{property.Name} differs from C# defaults");
   }
   if(Mode=="Reject")throw new InvalidDataException("Scheduled fixture rejected mutation");
   var identity=command.EntityId.Value;
   if(verb=="RequestScheduledTaskRun"){
    if(Get<long>("ExpectedRevision")!=0||!Get<bool>("Manual")||Get<DateTimeOffset>("IntendedFireTimeUtc")!=new DateTimeOffset(2026,10,26,12,30,0,TimeSpan.Zero))throw new InvalidDataException("Manual run must use API time and a fresh aggregate");
    var schedule=definitions[Get<ScheduledTaskId>("ScheduleId").Value];if(Get<long>("DefinitionRevision")!=schedule.DesiredRevision)throw new InvalidDataException("Wrong desired schedule revision");
    runs[identity]=ScheduledFixtures.Run with{Id=new(identity),ScheduleId=schedule.Id,Status=ScheduledTaskRunStatus.Requested,IntendedFireTimeUtc=Get<DateTimeOffset>("IntendedFireTimeUtc"),Revision=1};
   }else if(verb=="FailScheduledTaskRun"){
    var run=runs[identity];if(run.Status!=ScheduledTaskRunStatus.Uncertain||Get<long>("ExpectedRevision")!=run.Revision||Get<string>("Reason").Length<3||Get<DateTimeOffset>("FinishedAtUtc")!=new DateTimeOffset(2026,10,26,12,30,0,TimeSpan.Zero))throw new InvalidDataException("Invalid uncertain-run resolution");
    runs[identity]=run with{Status=ScheduledTaskRunStatus.Failed,Revision=run.Revision+1,Stage=Get<string>("Stage"),Detail=Get<string>("Detail"),FinishedAtUtc=Get<DateTimeOffset>("FinishedAtUtc")};
   }else if(verb=="CreateScheduledTask"){
    if(Get<long>("ExpectedRevision")!=0)throw new InvalidDataException("Wrong create revision");definitions[identity]=ScheduledFixtures.Definition with{Id=new(identity),Revision=1,DesiredRevision=1,Schedule=Get<ScheduledTaskSchedule>("Schedule"),Enabled=false};
   }else{
    var old=definitions[identity];if(Get<long>("ExpectedRevision")!=old.Revision)throw new InvalidDataException("Stale schedule revision");
    definitions[identity]=verb switch{
     "ChangeScheduledTaskSchedule"=>old with{Schedule=Get<ScheduledTaskSchedule>("Schedule"),Revision=old.Revision+1,DesiredRevision=old.DesiredRevision+1},
     "EnableScheduledTask"=>old with{Enabled=true,Revision=old.Revision+1,DesiredRevision=old.DesiredRevision+1},
     "DisableScheduledTask"=>old with{Enabled=false,Revision=old.Revision+1,DesiredRevision=old.DesiredRevision+1},
     "RemoveScheduledTask" when !old.Enabled=>old with{Removed=true,Revision=old.Revision+1},
     _=>throw new InvalidDataException("Unsupported scheduled state change")};
   }
   var noticeSubject=new ActorSubject(ActorType.Event,"ScheduledTaskEvent",ScheduledTaskCreatedCompleteEvent.Verb,command.EntityId.Format());
   await connection.PublishAsync(noticeSubject.ToString(),MessagePackSerializer.Serialize(new ScheduledTaskCreatedCompleteEvent{Subject=noticeSubject,CommandId=command.CommandId,EntityId=command.EntityId,ReceivedOn=new(2026,10,26,12,30,0,DateTimeKind.Utc)},options),cancellationToken:token);
   if(Mode=="MalformedAck")return MessagePackSerializer.Serialize("invalid acknowledgement",options);
   if(Mode=="EmptyReceipt")return Result(new GuidResult(Guid.Empty));
   return Mode=="DropReply"?null:Result(new GuidResult(command.CommandId));
  }catch(Exception error){return MessagePackSerializer.Serialize(new ServiceResult<GuidResult>{Success=false,ErrorCode=9300,ErrorMessage=error.Message},options);}
  void Validate(ActorSubject actual,ScheduledTaskId entity,string environment,string host){if(actual.ToString()!=subject||pieces[3]!=entity.Format()||!entity.IsValid||environment!="Development"||host!="development")throw new InvalidDataException("Invalid scheduled query scope/route");}
 }
}
