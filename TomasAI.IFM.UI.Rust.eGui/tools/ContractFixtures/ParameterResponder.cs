using System.Reflection;
using MessagePack;
using NATS.Client.Core;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
/// <summary>Typed parameter protocol state machine; no live actor, startup plan, or database is invoked.</summary>
internal sealed class ParameterResponder {
 public string Mode{get;set;}="Normal";
 public static readonly DateTime Now=new(2026,10,26,12,30,0,DateTimeKind.Utc);
 public readonly ParameterComponentSummary[] Components;
 readonly Dictionary<Guid,List<ParameterSetVersion>> versions=[];
 readonly Dictionary<Guid,List<ParameterOperationReceipt>> receipts=[];
 readonly HashSet<Guid> commands=[];
 readonly ParameterAssignmentScope scope=new("Workflow","IntrinsicTimeStrategy","RegimeDiscovery",ParameterSchemaRegistry.RegimeComponent,"{}","fixture"){TargetHorizon=TimeFrameType.Daily};
 readonly Guid assignmentId=Guid.Parse("ac56f7a9-b41c-4fb9-9ec3-eab3497bc86a");
 readonly List<ParameterAuditEntry> assignmentAudit=[];
 ParameterAssignmentRevision? assignment;long assignmentRevision;
 public ParameterResponder(){
  var values=ParameterFixtures.Values();Components=values.Select(v=>new ParameterComponentSummary("Fixture","Fixture",v.ComponentCode,v.ComponentCode,[v.SchemaVersion],true)).OrderBy(v=>v.ComponentCode==ParameterSchemaRegistry.FuturesItiSignalComponent?0:1).ToArray();
  foreach(var (value,index) in values.Select((v,i)=>(v,i))){var id=Guid.Parse($"f256f7a9-b41c-4fb9-9ec3-eab3497b{index:0000}");var typed=Preview(id,value.ComponentCode,TimeFrameType.Daily,null);typed=typed.WithIdentity(id,1);versions[id]=[Version(typed,1,"Fixture "+value.ComponentCode,1)];receipts[id]=[];}
 }
 public static ParameterSetValue Preview(Guid id,string component,TimeFrameType horizon,ParameterSetValue? source){
  var value=source??ParameterFixtures.Values().Single(v=>v.ComponentCode==component).WithIdentity(id,0);
  if(value.SetId!=id||value.ComponentCode!=component)throw new InvalidDataException("Preview crossed parameter identity");
  if(value is RegimeDiscoveryParameterValue regime){var p=regime.Value;var mapping=p.Horizon.TimeFrames.Length==0?RegimeDiscoveryHorizonConfiguration.CreateDefault(horizon):p.Horizon;var legacy=p.SignalRequirements??[];var signals=p.SignalMetrics??RegimeDiscoveryMetricConfigurationProjection.FromLegacySignals(id,legacy);var observations=p.ObservationMetrics??RegimeDiscoveryMetricConfigurationProjection.FromLegacyObservations(id,legacy);value=ParameterSetValue.From(p with{SchemaVersion=5,TargetHorizon=horizon,Horizon=mapping,SignalMetrics=signals,ObservationMetrics=observations,SignalRequirements=RegimeDiscoveryMetricConfigurationProjection.ToLegacyRequirements(id,signals,observations,mapping)});}
  return ParameterValueCodec.Snapshot(value);
 }
 static ParameterSetVersion Version(ParameterSetValue value,int number,string name,long revision)=>new(new(value.SetId,number,value.ComponentCode,ParameterValueCodec.Hash(value)),name,"Fixture description",value.SchemaVersion,ParameterVersionStatus.Draft,"",Now,"Fixture",CatalogRevision:revision){Parameters=value};
 public ParameterSetVersion[] Versions(string component)=>versions.Values.SelectMany(v=>v).Where(v=>v.Reference.ComponentCode==component).ToArray();
 public async Task<byte[]?> Handle(NatsConnection connection,string subject,byte[] payload,CancellationToken token){
  var options=MessagePackBinarySerializer.Options;var pieces=subject.Split('.');var verb=pieces[2];var assembly=typeof(ParameterSetValue).Assembly;
  byte[] Result<T>(T value)=>MessagePackSerializer.Serialize(new ServiceResult<T>{Success=true,ErrorMessage="",Value=value},options);
  try{
   var type=assembly.GetTypes().Single(t=>t.Namespace==typeof(ParameterSetValue).Namespace&&t.GetField("Actor")?.GetRawConstantValue() as string==pieces[1]&&t.GetField("Verb")?.GetRawConstantValue() as string==verb);
   var value=MessagePackSerializer.Deserialize(type,payload,options);
   T Get<T>(string key)=>(T)type.GetProperty(key)!.GetValue(value)!;
   if(Get<ActorSubject>("Subject").ToString()!=subject)throw new InvalidDataException("Parameter subject mismatch");
   if(pieces[0]=="Query"){
    if(pieces[3]!="none"||Get<IActorEntityId>("EntityId").Format()!="none")throw new InvalidDataException("Incorrect parameter query entity");
    switch(verb){
     case "ListParameterComponents":return Result(Components);
     case "ListParameterVersions":
      var q=(ListParameterVersionsQuery)value;if(q.Limit!=100||q.PayloadJson!="{}"||q.SchemaVersion!=2)throw new InvalidDataException("Parameter query default changed");var rows=Versions(q.ComponentCode).Where(v=>q.SetId==Guid.Empty||v.Reference.SetId==q.SetId).ToArray();return Result(Mode=="WrongComponent"?Versions(ParameterSchemaRegistry.RegimeComponent):rows);
     case "PreviewTypedParameterDraft":var p=(PreviewTypedParameterDraftQuery)value;return Result(Preview(p.SetId,p.ComponentCode,(TimeFrameType)p.TargetHorizon,p.Source));
     case "ValidateTypedParameterCandidate":var validation=(ValidateTypedParameterCandidateQuery)value;return Result(new ParameterValidationReport(ParameterValueCodec.Hash(validation.Parameters!),[]));
     case "GetParameterSetState":var state=(GetParameterSetStateQuery)value;if(Mode=="SlowState")return null;return Result(new ParameterSetSnapshot(state.SetId,versions[state.SetId].Max(v=>v.CatalogRevision),versions[state.SetId].ToArray(),receipts[state.SetId].ToArray(),[]));
     case "GetParameterAssignment":return Result(new ParameterAssignmentSnapshot(new(assignmentId),scope,assignmentRevision,assignment,[..assignmentAudit]));
     case "PreviewSignalStartupPlan":return Result(new ParameterSignalStartupPlan(Get<Guid>("StartupRunId"),"assignments","plan",[],[]));
     case "GetParameterStartupRuns":return Result(Array.Empty<ParameterStartupRun>());
     case "GetParameterStartupReport":return Result(new ParameterSignalStartupReport(Get<Guid>("RunId"),"plan",new(2026,10,26),"ESZ6",Now,[]));
     case "GetParameterSignalMonitoring":return Result(new ParameterSignalMonitoringSnapshot(Get<Guid>("RunId"),"ESZ6",Now,[]));
     case "ListLegacyParameterVersions":
      var offset=Get<int>("Offset");return Result(Mode=="LegacyPages"?Enumerable.Range(offset+1,offset==0?100:1).Select(number=>new ParameterLegacyVersion(Legacy with{Version=number},1,"{}","Legacy fixture",1)).ToArray():offset==0?new[]{new ParameterLegacyVersion(Legacy,1,"{}","Legacy fixture",1)}:[]);
     case "PreviewLegacyParameterMigration":if(Get<Guid>("SetId")!=Legacy.SetId||Get<int>("Version")!=Legacy.Version)throw new InvalidDataException("Unknown legacy version");return Result(new CreateParameterSetCommand{CommandId=Guid.Parse("b0125491-6a62-4c77-a9de-88aa8ad785aa"),EntityId=new(Guid.Parse("e0125491-6a62-4c77-a9de-88aa8ad785aa")),Name="Imported fixture",ComponentCode=ParameterSchemaRegistry.RegimeComponent,SchemaVersion=5,LegacySource=Legacy,Parameters=Preview(Guid.Parse("e0125491-6a62-4c77-a9de-88aa8ad785aa"),ParameterSchemaRegistry.RegimeComponent,TimeFrameType.Daily,null)});
     default:throw new InvalidDataException("Unsupported parameter fixture query");
    }
   }
   var command=Get<Guid>("CommandId");if(command==Guid.Empty||!commands.Add(command))throw new InvalidDataException("Duplicate parameter submission");
   if(Mode=="Reject")throw new InvalidDataException("Fixture rejected parameter mutation");
   if(value is IParameterSetMutation c){
    var set=c.EntityId.SetId;if(pieces[3]!=set.ToString("N"))throw new InvalidDataException("Incorrect parameter command entity");
    if(!versions.TryGetValue(set,out var rows)){if(value is not CreateParameterSetCommand||c.ExpectedRevision!=0)throw new InvalidDataException("Unknown parameter set");versions[set]=rows=[];receipts[set]=[];}
    var revision=rows.Select(v=>v.CatalogRevision).DefaultIfEmpty().Max();if(c.ExpectedRevision!=revision)throw new InvalidDataException("Stale parameter revision");
    ParameterSetVersion updated;
    if(value is CreateParameterSetCommand or SaveParameterDraftCommand){var typed=c.Parameters??throw new InvalidDataException("Typed values required");if(typed.SetId!=set||typed.ComponentCode!=c.ComponentCode||typed.SchemaVersion!=c.SchemaVersion||c.PayloadJson!=""||c.Name.Length==0)throw new InvalidDataException("Typed parameter identity or metadata mismatch");var number=rows.Select(v=>v.Reference.Version).DefaultIfEmpty().Max()+1;updated=Version(typed.WithIdentity(set,number),number,c.Name,revision+1) with{Description=c.Description};rows.Add(updated);}
    else{var old=rows.Single(v=>v.Reference.Version==c.Version);updated=value switch{PublishParameterVersionCommand when old.Status==ParameterVersionStatus.Draft=>old with{Status=ParameterVersionStatus.Published,PublishedAtUtc=Now,CatalogRevision=revision+1},RetireParameterVersionCommand when old.Status==ParameterVersionStatus.Published=>old with{Status=ParameterVersionStatus.Retired,RetiredAtUtc=Now,CatalogRevision=revision+1},RenameParameterSetCommand=>old with{Name=c.Name,Description=c.Description,CatalogRevision=revision+1},_=>throw new InvalidDataException("Invalid parameter transition")};rows[rows.IndexOf(old)]=updated;}
    receipts[set].Add(new(command,"fixture",revision+1,updated.Reference));
   }else if(value is AssignParameterVersionCommand assign){if(assign.EntityId.AssignmentId!=assignmentId||assign.ExpectedRevision!=assignmentRevision)throw new InvalidDataException("Stale assignment");assignment=new(assignmentId,scope,++assignmentRevision,assign.Reference,true,default,Now,"Fixture");}
   else if(value is DisableParameterAssignmentCommand disable){if(disable.EntityId.AssignmentId!=assignmentId||disable.ExpectedRevision!=assignmentRevision||assignment is null)throw new InvalidDataException("Stale assignment disable");assignment=assignment with{Enabled=false,Revision=++assignmentRevision};}
   else throw new InvalidDataException("Unsupported parameter fixture mutation");
   if(value is AssignParameterVersionCommand or DisableParameterAssignmentCommand)
    assignmentAudit.Add(new(command,assignmentId,assignmentRevision,verb,"Fixture",Now,"",""));
   await connection.PublishAsync($"Event.ParameterSetEvent.ChangedComplete.{pieces[3]}",MessagePackSerializer.Serialize(command,options),cancellationToken:token);
   if(Mode=="MalformedAck")return MessagePackSerializer.Serialize("invalid",options);
   return Mode=="DropReply"?null:Result(new GuidResult(command));
  }catch(Exception error){return MessagePackSerializer.Serialize(new ServiceResult<GuidResult>{Success=false,ErrorCode=33101,ErrorMessage=error.Message},options);}
 }
 static readonly ParameterLegacyReference Legacy=new("RegimeDiscoveryParameterSet",Guid.Parse("a0125491-6a62-4c77-a9de-88aa8ad785aa"),1,"fixture","sorted-json-v1");
}
