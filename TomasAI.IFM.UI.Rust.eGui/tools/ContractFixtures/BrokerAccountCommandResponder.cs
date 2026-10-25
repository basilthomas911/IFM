using System.Reflection;
using MessagePack;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.BrokerAccount.Contracts;
using NATS.Client.Core;
using TomasAI.IFM.Shared.EventModelActor;
internal static class BrokerAccountCommandResponder {
 static string mode="Normal";static int count;
 static Guid? pending;
 public static bool Handles(string subject)=>subject.StartsWith("Query.NativeCompatibility.BrokerAccountMutation",StringComparison.Ordinal)||subject.StartsWith("Command.BrokerAccountCommand.",StringComparison.Ordinal);
 public static async Task<byte[]?> Handle(NatsConnection connection,string subject,byte[] payload,CancellationToken token){
  var options=MessagePackBinarySerializer.Options;
  byte[] Result<T>(T value)=>MessagePackSerializer.Serialize(new ServiceResult<T>{Success=true,ErrorMessage="",Value=value},options);
  async Task Event(Guid id){var route=new ActorSubject(ActorType.Event,"BrokerAccountEvent","BrokerAccountChanged","IFM-EMULATOR-PAPER");await connection.PublishAsync(route.ToString(),MessagePackSerializer.Serialize(new BrokerAccountChangedEvent{Subject=route,Id=Guid.NewGuid(),CommandId=id,EventId=17,EntityId=new("IFM-EMULATOR-PAPER"),BrokerAccountDefinition=new(){Id=new("IFM-EMULATOR-PAPER")}},options),cancellationToken:token);}
  if(subject=="Query.NativeCompatibility.BrokerAccountMutationMode.none"){mode=MessagePackSerializer.Deserialize<string>(payload,options);count=0;pending=null;return Result(new ScalarReadModel<bool>(true));}
  if(subject=="Query.NativeCompatibility.BrokerAccountMutationCount.none")return Result(new ScalarReadModel<int>(count));
  if(subject=="Query.NativeCompatibility.BrokerAccountMutationReleaseReceipt.none"){if(pending is {} id){await Event(id);pending=null;}return Result(new ScalarReadModel<bool>(true));}
  count++;
  var verb=subject.Split('.')[2];
  var type=typeof(BrokerAccountCommand).Assembly.GetTypes().Single(t=>!t.IsAbstract&&typeof(BrokerAccountCommand).IsAssignableFrom(t)&&t.GetField("Verb",BindingFlags.Public|BindingFlags.Static)?.GetRawConstantValue() as string==verb);
  var command=(BrokerAccountCommand)MessagePackSerializer.Deserialize(type,payload,options)!;
  if(command.CommandId==Guid.Empty||!command.PostEvents||command.Subject.ToString()!=subject||command.EntityId.AccountAlias!="IFM-EMULATOR-PAPER")throw new InvalidDataException("Incorrect qualification command identity");
  object? Get(string name)=>type.GetProperty(name)?.GetValue(command);
  var time=type.GetProperties().Where(p=>p.PropertyType==typeof(DateTime)).Select(p=>(DateTime)p.GetValue(command)!).Single();
  if(!DesktopFixtureMode.Clock(time,StrategyFixtures.At))throw new InvalidDataException("Qualification command used a workstation date");
  if(Get("ManifestHash") is string hash&&hash!=new string('a',64)||Get("AuthorizedBy") is string reviewer&&reviewer!="Reviewer"||Get("Reason") is string reason&&reason!="Offline qualification"||Get("EvidenceReference") is string reference&&reference!="offline://report"||Get("ApprovalId") is Guid approval&&approval==Guid.Empty)throw new InvalidDataException("Qualification command input differs");
  if(mode=="Reject")return MessagePackSerializer.Serialize(new ServiceResult<GuidResult>{Success=false,ErrorCode=25104,ErrorMessage="Fixture qualification rejection"},options);
  if(mode=="LostAck"){pending=command.CommandId;return null;}
  if(mode=="EventBeforeAck"){await Event(command.CommandId);return null;}
  if(mode=="WrongEvent"){await Event(Guid.NewGuid());return null;}
  return Result(new GuidResult(mode=="WrongReceipt"?Guid.NewGuid():command.CommandId));
 }
}
