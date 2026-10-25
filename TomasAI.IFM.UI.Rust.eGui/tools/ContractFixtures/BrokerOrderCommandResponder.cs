using MessagePack;
using NATS.Client.Core;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Domain.Trade.Shared.Order.Broker;
internal static class BrokerOrderCommandResponder {
 static string mode="Normal";static int count;static Guid? pending;
 public static bool Handles(string subject)=>subject.StartsWith("Query.NativeCompatibility.BrokerOrderMutation",StringComparison.Ordinal)||subject.StartsWith("Command.BrokerOrderCommand.",StringComparison.Ordinal);
 public static async Task<byte[]?> Handle(NatsConnection connection,string subject,byte[] payload,CancellationToken token){
  var options=MessagePackBinarySerializer.Options;
  byte[] Result<T>(T value)=>MessagePackSerializer.Serialize(new ServiceResult<T>{Success=true,ErrorMessage="",Value=value},options);
  async Task Event(Guid id){var order=BrokerOrderFixtures.Order;var route=new ActorSubject(ActorType.Event,"BrokerOrderEvent","BrokerOrderChanged",order.Id.Format());await connection.PublishAsync(route.ToString(),MessagePackSerializer.Serialize(new BrokerOrderChangedEvent{Subject=route,Id=Guid.NewGuid(),CommandId=id,EventId=17,EntityId=order.Id,BrokerOrderDefinition=order},options),cancellationToken:token);}
  if(subject=="Query.NativeCompatibility.BrokerOrderMutationMode.none"){mode=MessagePackSerializer.Deserialize<string>(payload,options);count=0;pending=null;return Result(new ScalarReadModel<bool>(true));}
  if(subject=="Query.NativeCompatibility.BrokerOrderMutationCount.none")return Result(new ScalarReadModel<int>(count));
  if(subject=="Query.NativeCompatibility.BrokerOrderMutationReleaseReceipt.none"){if(pending is {} id){await Event(id);pending=null;}return Result(new ScalarReadModel<bool>(true));}
  count++;Guid commandId;Guid operation;DateTime time;bool post;string routeText;TomasAI.IFM.Domain.Trade.Shared.BrokerOrderId entity;
  if(subject.StartsWith("Command.BrokerOrderCommand.RequestBrokerOrderLimitUpdate.",StringComparison.Ordinal)){
   var c=MessagePackSerializer.Deserialize<RequestBrokerOrderLimitUpdateCommand>(payload,options);commandId=c.CommandId;operation=c.OperationId;time=c.EffectiveAtUtc;post=c.PostEvents;routeText=c.Subject.ToString();entity=c.EntityId;
   if(c.NewSignedNetDebitLimit!=6050.50m)throw new InvalidDataException("Incorrect broker limit");
  }else{
   var c=MessagePackSerializer.Deserialize<RequestBrokerOrderCancelCommand>(payload,options);commandId=c.CommandId;operation=c.OperationId;time=c.EffectiveAtUtc;post=c.PostEvents;routeText=c.Subject.ToString();entity=c.EntityId;
  }
  if(commandId==Guid.Empty||commandId!=operation||!post||routeText!=subject||entity!=BrokerOrderFixtures.Order.Id||!DesktopFixtureMode.Clock(time,StrategyFixtures.At))throw new InvalidDataException("Incorrect broker mutation identity or API clock");
  if(mode=="Reject")return MessagePackSerializer.Serialize(new ServiceResult<GuidResult>{Success=false,ErrorCode=25103,ErrorMessage="Fixture broker rejection"},options);
  if(mode=="LostAck"){pending=commandId;return null;}
  if(mode=="EventBeforeAck"){await Event(commandId);return null;}
  if(mode=="WrongEvent"){await Event(Guid.NewGuid());return null;}
  return Result(new GuidResult(mode=="WrongReceipt"?Guid.NewGuid():commandId));
 }
}
