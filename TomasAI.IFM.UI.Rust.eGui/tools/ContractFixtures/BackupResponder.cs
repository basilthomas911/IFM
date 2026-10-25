using System.Reflection;
using MessagePack;
using NATS.Client.Core;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Queries;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;
/// <summary>Isolated in-memory backup API. Commands only change fixture state; no database/host call exists.</summary>
internal sealed class BackupResponder {
 public string Mode{get;set;}="Normal";
 readonly HashSet<Guid> commands=[];
 DatabaseBackupPolicyReadModel policy=BackupFixtures.Policy;
 readonly Dictionary<Guid,DatabaseBackupOperationReadModel> operations=new(){{BackupFixtures.Id,BackupFixtures.Operation}};
 public async Task<byte[]?> Handle(NatsConnection connection,string subject,byte[] payload,CancellationToken token){
  var options=MessagePackBinarySerializer.Options;var pieces=subject.Split('.');var verb=pieces[2];
  byte[] Result<T>(T value)=>MessagePackSerializer.Serialize(new ServiceResult<T>{Success=true,ErrorMessage="",Value=value},options);
  try {
   var assembly=typeof(GetDatabaseBackupSetupQuery).Assembly;
   if(pieces[0]=="Query"){
    var type=assembly.GetTypes().Single(t=>!t.IsAbstract&&typeof(IDatabaseBackupQuery).IsAssignableFrom(t)&&((IDatabaseBackupQuery)Activator.CreateInstance(t)!).Verb==verb);
    var q=(IDatabaseBackupQuery)MessagePackSerializer.Deserialize(type,payload,options);q.Validate();
    if(q.Subject.ToString()!=subject||q.EntityId.Value!=q.Request.RequestId||pieces[3]!=q.EntityId.Format()||!DesktopFixtureMode.Clock(q.Request.CreatedUtc,new DateTimeOffset(2026,10,26,12,30,0,TimeSpan.Zero)))throw new InvalidDataException("Backup query did not use API clock or canonical identity");
    if(Mode=="SlowRefresh"&&verb=="GetProtectionSets")return null;
    switch(verb){
     case "GetProtectionSets":if(q.PageSize!=200)throw new InvalidDataException("Incorrect protection set page size");return Result(new[]{BackupFixtures.Protection with{Source=q.Source}});
     case "GetBackupSetup":return Result(BackupFixtures.Setup);
     case "ListBackupOperations":
      if(q.PageSize!=50)throw new InvalidDataException("Incorrect history page size");
      var rows=operations.Values.Where(v=>v.Source==q.Source).ToArray();
      if(Mode=="RepeatHistory"){rows=Enumerable.Range(0,50).Select(n=>BackupFixtures.Operation with{Source=q.Source,OperationId=new(Guid.Parse($"d256f7a9-b41c-4fb9-9ec3-eab3497b{n:0000}"))}).ToArray();}
      if(Mode=="WrongSource")rows=[BackupFixtures.Operation with{Source=q.Source==BackupSource.AwsCloud?BackupSource.LocalWorkstation:BackupSource.AwsCloud}];
      return Result(rows);
     case "GetLatestVerifiedBackup":case "GetLatestRestoreTestedBackup":return Result<DatabaseRestorePointReadModel?>(null);
     case "GetServiceHealth":return Result(new[]{new DatabaseBackupHealthReadModel{Source=q.Source,HostId=new("fixture-host"),Ready=true,ObservedUtc=new(2026,10,26,12,30,0,TimeSpan.Zero)}});
     case "GetPolicy":if(q.PolicyId?.Value!=policy.PolicyId.Value)throw new InvalidDataException("Unknown policy");return Result(policy);
     case "GetBackupLog":
      var log=(GetDatabaseBackupLogQuery)q;if(log.PageSize!=100||log.OperationId is null||!operations.ContainsKey(log.OperationId.Value.Value))throw new InvalidDataException("Invalid output scope");
      return Result(new DatabaseBackupLogReadModel{Phases=log.AfterPhaseRevision==0?[new(){Revision=1,ObservedUtc=new(2026,10,26,12,30,0,TimeSpan.Zero),Phase=DatabaseRecoveryPhase.Capturing,ProgressPercent=35}]:[],Output=log.OutputOffset==0?"output-1":"output-2",NextOutputOffset=Mode=="NoOutputAdvance"?log.OutputOffset:log.OutputOffset+8,EndOfOutput=Mode!="NoOutputAdvance"&&log.OutputOffset>0,OutputAvailable=true});
     default:throw new InvalidDataException("Unknown backup query");
    }
   }
   var commandType=assembly.GetTypes().Single(t=>!t.IsAbstract&&typeof(IDatabaseBackupCommand).IsAssignableFrom(t)&&((IDatabaseBackupCommand)Activator.CreateInstance(t)!).Verb==verb);
   var c=(IDatabaseBackupCommand)MessagePackSerializer.Deserialize(commandType,payload,options);c.Validate();
   if(c.Subject.ToString()!=subject||pieces[3]!=c.EntityId.Format()||c.CommandId!=c.Request.RequestId||!commands.Add(c.CommandId)||commandType.GetProperty("PostEvents")!.GetValue(c) is not true||c.RouteTo!=BoundedContextName.SystemAdminBoundedContext)throw new InvalidDataException("Invalid or duplicate backup command");
   var allowed=new HashSet<string>{"CommandId","Subject","EntityId","Request","Source","ProtectionSetId","ExpectedPolicyRevision","ConsistencyMode","RequiredDestinations","RequestedBackupMode","SafeReason","RestorePointId","FreshTarget","RestoreClass","DisposableTargetProfile","ValidationProfile","PolicyId","Policy"};
   var defaults=Activator.CreateInstance(commandType)!;
   foreach(var property in commandType.GetProperties().Where(p=>Attribute.IsDefined(p,typeof(KeyAttribute))&&!allowed.Contains(p.Name))){var actual=MessagePackSerializer.Serialize(property.PropertyType,property.GetValue(c),MessagePackBinarySerializer.ContentOptions);var expected=MessagePackSerializer.Serialize(property.PropertyType,property.GetValue(defaults),MessagePackBinarySerializer.ContentOptions);if(!actual.AsSpan().SequenceEqual(expected))throw new InvalidDataException($"Backup {verb}.{property.Name} differs from CLR defaults");}
   if(Mode=="Reject")throw new InvalidDataException("Backup fixture rejected mutation");
   switch(verb){
    case "RequestBackup":if(c.RequiredDestinations is not [{Name:"online-vault",Required:true}]||c.ConsistencyMode!=DatabaseConsistencyMode.CoordinatedProtectionSet||c.ExpectedPolicyRevision!=7)throw new InvalidDataException("Incorrect backup request");operations[c.EntityId.Value]=BackupFixtures.Operation with{OperationId=c.EntityId,Source=c.Source};break;
    case "CancelBackup":var old=operations[c.EntityId.Value];operations[c.EntityId.Value]=old with{Phase=DatabaseRecoveryPhase.Cancelled,Outcome=DatabaseRecoveryOutcome.Cancelled};break;
    case "RequestRestoreDrill":if(c.FreshTarget?.Profile!=c.DisposableTargetProfile||c.FreshTarget?.LogicalTarget!=c.ValidationProfile||c.RestoreClass!=DatabaseRestoreClass.Drill)throw new InvalidDataException("Invalid restore drill");break;
    case "UpdatePolicy":if(c.ExpectedPolicyRevision!=policy.Revision||c.PolicyId!=policy.PolicyId)throw new InvalidDataException("Stale policy");policy=policy with{Definition=c.Policy!,Revision=policy.Revision+1};break;
    default:throw new InvalidDataException("Unsupported backup mutation");
   }
   // A notification invalidates read state; its body is not treated as a command receipt.
   await connection.PublishAsync($"Event.DatabaseBackupEvent.ProgressRecorded.{c.EntityId.Format()}",MessagePackSerializer.Serialize(c.EntityId,options),cancellationToken:token);
   if(Mode=="MalformedAck")return MessagePackSerializer.Serialize("invalid",options);
   if(Mode=="EmptyReceipt")return Result(new GuidResult(Guid.Empty));
   return Mode=="DropReply"?null:Result(new GuidResult(c.CommandId));
  }catch(Exception error){return MessagePackSerializer.Serialize(new ServiceResult<GuidResult>{Success=false,ErrorCode=9200,ErrorMessage=error.Message},options);}
 }
}
