using MessagePack;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.BrokerAccount.Contracts;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Order.Broker;
using TomasAI.IFM.Domain.Trade.Shared.Order.Execution;
internal static class BrokerReadResponder {
 static string mode="Normal";static int executionReads;
 static readonly Guid attempt=Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");
 static readonly Guid component=Guid.Parse("721dd687-7a5a-4fa6-a404-01545cbab900");
 static readonly TradeOrderId scope=new(1,2,123);
 public static bool Handles(string subject)=>subject.StartsWith("Query.NativeCompatibility.BrokerRead",StringComparison.Ordinal)||subject.StartsWith("Query.BrokerAccountQuery.GetBrokerAccount.",StringComparison.Ordinal)||subject.StartsWith("Query.BrokerOrderQuery.GetBrokerOrdersForTradeOrder.",StringComparison.Ordinal)||subject.StartsWith("Query.OrderExecutionQuery.GetOrderExecution.",StringComparison.Ordinal);
 public static byte[] Handle(string subject,byte[] payload){
  var options=MessagePackBinarySerializer.Options;
  byte[] Result<T>(T value)=>MessagePackSerializer.Serialize(new ServiceResult<T>{Success=true,ErrorMessage="",Value=value},options);
  byte[] Error()=>MessagePackSerializer.Serialize(new ServiceResult<object>{Success=false,ErrorCode=25210,ErrorMessage="Fixture evidence unavailable"},options);
  bool EntityMatches(TomasAI.IFM.Shared.EventModelActor.Contracts.IActorEntityId? entity,string key)=>entity?.Format()==key||DesktopFixtureMode.Enabled&&entity is null;
  if(subject=="Query.NativeCompatibility.BrokerReadMode.none"){mode=MessagePackSerializer.Deserialize<string>(payload,options);executionReads=0;return Result(new ScalarReadModel<bool>(true));}
  if(subject=="Query.NativeCompatibility.BrokerReadCount.none")return Result(new ScalarReadModel<int>(executionReads));
  if(mode=="Unavailable")return Error();
  if(subject=="Query.BrokerAccountQuery.GetBrokerAccount.IFM-EMULATOR-PAPER"){
   var q=MessagePackSerializer.Deserialize<GetBrokerAccountQuery>(payload,options);
   // The existing C# API's concrete BrokerAccountId is not an entity-union
   // variant and arrives as nil. The backend reads the typed account field.
   var entityMatches=EntityMatches(q.EntityId,q.BrokerAccountId.Format());
   if(q.Subject.ToString()!=subject||q.BrokerAccountId.AccountAlias!="IFM-EMULATOR-PAPER"||!entityMatches)throw new InvalidDataException("Incorrect account read identity: "+MessagePackSerializer.ConvertToJson(payload,options));
   return Result(new BrokerAccountDefinition{Id=new(mode=="WrongAccount"?"OTHER":"IFM-EMULATOR-PAPER"),Gate=BrokerAccountOperationalGate.Open,Revision=3,DevelopmentQualificationsExempt=true});
  }
  if(mode=="Empty"&&subject.StartsWith("Query.BrokerOrderQuery.GetBrokerOrdersForTradeOrder.",StringComparison.Ordinal)){
   var q=MessagePackSerializer.Deserialize<GetBrokerOrdersForTradeOrderQuery>(payload,options);
   if(q.Subject.ToString()!=subject||q.TradeOrderId.PortfolioId!=1||q.TradeOrderId.FundId!=2||q.TradeOrderId.OrderId is not (123 or 701))throw new InvalidDataException("Empty evidence crossed fixture scope");
   return Result(Array.Empty<BrokerOrderDefinition>());
  }
  if(mode=="Submitted"&&subject.StartsWith("Query.BrokerOrderQuery.GetBrokerOrdersForTradeOrder.",StringComparison.Ordinal)){
   var q=MessagePackSerializer.Deserialize<GetBrokerOrdersForTradeOrderQuery>(payload,options);
   return Result(OrderPreparationResponder.Accepted is {} accepted&&q.TradeOrderId==accepted.Id&&TradeLifecycleResponder.Attempt is {} submittedAttempt?accepted.Components.Select(c=>new BrokerOrderDefinition{Id=new(new(accepted.Id,submittedAttempt),c.ComponentId),Order=accepted,Status=BrokerOrderStatus.Working,ChangedAtUtc=StrategyFixtures.At,CurrentSignedNetDebitLimit=c.SignedNetDebitLimit??0m}).ToArray():[]);
  }
  if(subject=="Query.BrokerOrderQuery.GetBrokerOrdersForTradeOrder.1.2.123"){
   var q=MessagePackSerializer.Deserialize<GetBrokerOrdersForTradeOrderQuery>(payload,options);
   if(q.Subject.ToString()!=subject||q.TradeOrderId!=scope||!EntityMatches(q.EntityId,scope.Format()))throw new InvalidDataException("Incorrect broker-order read scope");
   if(mode=="Empty")return Result(Array.Empty<BrokerOrderDefinition>());
   if(mode=="Working")return Result(new[]{BrokerOrderFixtures.Order});
   var order=new BrokerOrderDefinition{Id=new(new(mode=="WrongScope"?new(1,3,123):scope,attempt),component),Status=BrokerOrderStatus.Working,Revision=5,BrokerRevision=2,CurrentSignedNetDebitLimit=-1.25m,ChangedAtUtc=new(2026,10,26,12,30,0,DateTimeKind.Utc)};
   return Result(mode=="Duplicate"?new[]{order,order}:new[]{order,order with{Id=new(new(scope,attempt),Guid.Parse("821dd687-7a5a-4fa6-a404-01545cbab900"))}});
  }
  if(mode=="Submitted"&&subject.StartsWith("Query.OrderExecutionQuery.GetOrderExecution.",StringComparison.Ordinal)){
   var q=MessagePackSerializer.Deserialize<GetOrderExecutionQuery>(payload,options);var accepted=OrderPreparationResponder.Accepted!;
   if(q.TradeOrderId!=accepted.Id||q.ExecutionAttemptId!=TradeLifecycleResponder.Attempt)throw new InvalidDataException("Submitted execution scope differs");
   return Result(new OrderExecutionDefinition{TradeOrderId=accepted.Id,ExecutionAttemptId=q.ExecutionAttemptId,Status=OrderExecutionStatus.Submitted,Order=accepted,Components=accepted.Components,OrderQuantity=1,StartedAtUtc=StrategyFixtures.At});
  }
  if(subject=="Query.OrderExecutionQuery.GetOrderExecution.1.2.123."+attempt.ToString("N")){
   var q=MessagePackSerializer.Deserialize<GetOrderExecutionQuery>(payload,options);
   if(q.Subject.ToString()!=subject||q.TradeOrderId!=scope||q.ExecutionAttemptId!=attempt||!EntityMatches(q.EntityId,new OrderExecutionId(scope,attempt).Format()))throw new InvalidDataException("Incorrect execution read identity");
   executionReads++;
   if(mode=="Working")return Result(BrokerOrderFixtures.Execution);
   if(mode=="MissingExecution")return Result<OrderExecutionDefinition?>(null);
   return Result(new OrderExecutionDefinition{TradeOrderId=scope,ExecutionAttemptId=mode=="WrongAttempt"?component:attempt,Status=OrderExecutionStatus.PartiallyFilled,OrderQuantity=3,CumulativeFilledQuantity=1});
  }
  throw new InvalidDataException("Unsupported broker evidence route: "+subject);
 }
}
