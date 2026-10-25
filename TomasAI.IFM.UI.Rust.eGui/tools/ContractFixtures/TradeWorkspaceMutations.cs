using System.Reflection;
using MessagePack;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.Common;
/// <summary>In-memory command/projection fault injection. All requests are
/// decoded with production contracts; no production command handler is run.</summary>
internal sealed partial class TradeWorkspaceResponder {
 readonly Dictionary<int,FundOrderProjectionReadModel> orders=new(){{123,Order}};
 readonly Dictionary<int,FundOrderTradeProjectionReadModel> tradeRows=new(){{456,Trade}};
 readonly Dictionary<Guid,byte[]> commands=[];
 Action? deferred;
 int nextOrder=700,nextTrade=200;
 public int Dispatches{get;private set;}
 bool ManualMode=>Mode is "Commands" or "LostAck" or "NoProjection" or "RejectCommand" or "ReadUnavailable";
 void ResetCommands(){orders.Clear();orders[123]=Order;tradeRows.Clear();tradeRows[456]=Trade;commands.Clear();deferred=null;nextOrder=700;nextTrade=200;Dispatches=0;}
 static void Same<T>(T actual,T expected){if(!MessagePackSerializer.Serialize(actual,MessagePackBinarySerializer.ContentOptions).SequenceEqual(MessagePackSerializer.Serialize(expected,MessagePackBinarySerializer.ContentOptions)))throw new InvalidDataException("Native manual request differs from the managed DTO: "+typeof(T).Name);}
 byte[]? Command(string subject,byte[] bytes){
  var options=MessagePackBinarySerializer.Options;
  Dispatches++;
  try{
   Guid commandId;Guid correlation;DateTime requested;PortfolioFundId entity;PortfolioAccessContext access;int error;
   Action apply;
   if(subject=="Command.PortfolioFundCommand.CreateManualFundOrder.1.2"){
    var c=MessagePackSerializer.Deserialize<CreateManualFundOrderCommand>(bytes,options);var r=c.Request;
    commandId=c.CommandId;correlation=c.CorrelationId;requested=c.RequestedOnUtc;entity=c.EntityId;access=c.Access;error=c.ErrorCode;
    Same(r,new CreateManualFundOrderRequest{PortfolioId=1,PortfolioVersion=1,FundId=2,FundMandateVersion=1,Reference=r.Reference.Trim(),IdempotencyKey=r.IdempotencyKey,RequestedAtUtc=StrategyFixtures.At,ExpiresAtUtc=StrategyFixtures.At.AddDays(1)});
    var type=typeof(TomasAI.IFM.Application.Api.Nats.Client.PortfolioCommandApi).Assembly.GetType("TomasAI.IFM.Application.Api.Nats.Client.IdempotentCommandId")!;
    var expected=(Guid)type.GetMethod("Create",BindingFlags.Public|BindingFlags.Static)!.MakeGenericMethod(typeof(CreateManualFundOrderRequest)).Invoke(null,[r.IdempotencyKey,r])!;
    if(r.IdempotencyKey==Guid.Empty||commandId!=expected||error!=34002)throw new InvalidDataException("Manual create idempotency/retry identity differs");
    apply=()=>{if(!orders.Values.Any(order=>order.IdempotencyKey==r.IdempotencyKey)){var id=++nextOrder;orders[id]=Order with{OrderId=id,AggregateVersion=1,IdempotencyKey=r.IdempotencyKey,OperatorReference=r.Reference};}};
   }else if(subject=="Command.PortfolioFundCommand.AddManualFundOrderTrade.1.2"){
    var c=MessagePackSerializer.Deserialize<AddManualFundOrderTradeCommand>(bytes,options);var r=c.Request;
    commandId=c.CommandId;correlation=c.CorrelationId;requested=c.RequestedOnUtc;entity=c.EntityId;access=c.Access;error=c.ErrorCode;
    var current=orders[r.OrderId];
    var closing=tradeRows.Values.Any(row=>row.OrderId==r.OrderId&&row.PrimaryTrade);
    Same(r,new AddManualFundOrderTradeRequest{PortfolioId=1,FundId=2,OrderId=current.OrderId,ExpectedOrderVersion=current.AggregateVersion,TradeId=nextTrade,TradeType=closing?"LongIronCondor":"ShortIronCondor",TradeState="NewTrade",TradeAction=closing?"Buy":"Sell",Reference=FundOrderTradeReference.Create("ESZ6",new(2026,10,26),null),PrimaryTrade=true,BaseContractSymbol="ES",BaseContractId="ESZ6",RequestedAtUtc=StrategyFixtures.At,EffectiveDate=new(2026,10,26)});
    apply=()=>{orders[r.OrderId]=current with{AggregateVersion=current.AggregateVersion+1};tradeRows[r.TradeId]=Trade with{OrderId=r.OrderId,TradeId=r.TradeId,TradeType=r.TradeType,TradeAction=r.TradeAction,PrimaryTrade=!closing,AggregateVersion=current.AggregateVersion+1};};
   }else if(subject=="Command.PortfolioFundCommand.RemoveManualFundOrderTrade.1.2"){
    var c=MessagePackSerializer.Deserialize<RemoveManualFundOrderTradeCommand>(bytes,options);var r=c.Request;
    commandId=c.CommandId;correlation=c.CorrelationId;requested=c.RequestedOnUtc;entity=c.EntityId;access=c.Access;error=c.ErrorCode;
    var current=orders[r.OrderId];
    Same(r,new ManualFundOrderTradeMutationRequest{PortfolioId=1,FundId=2,OrderId=current.OrderId,ExpectedOrderVersion=current.AggregateVersion,TradeId=r.TradeId,RequestedAtUtc=StrategyFixtures.At});
    if(!tradeRows.ContainsKey(r.TradeId))throw new InvalidDataException("Missing selected trade");
    apply=()=>{orders[r.OrderId]=current with{AggregateVersion=current.AggregateVersion+1};tradeRows.Remove(r.TradeId);};
   }else if(subject=="Command.PortfolioFundCommand.DeleteManualFundOrder.1.2"){
    var c=MessagePackSerializer.Deserialize<DeleteManualFundOrderCommand>(bytes,options);var r=c.Request;
    commandId=c.CommandId;correlation=c.CorrelationId;requested=c.RequestedOnUtc;entity=c.EntityId;access=c.Access;error=c.ErrorCode;
    var current=orders[r.OrderId];
    Same(r,new ManualFundOrderMutationRequest{PortfolioId=1,FundId=2,OrderId=current.OrderId,ExpectedOrderVersion=current.AggregateVersion,Reason=r.Reason,RequestedAtUtc=StrategyFixtures.At});
    apply=()=>{orders.Remove(r.OrderId);foreach(var id in tradeRows.Where(row=>row.Value.OrderId==r.OrderId).Select(row=>row.Key).ToArray())tradeRows.Remove(id);};
   }else throw new InvalidDataException("Unsupported fixture manual command");
   if(commandId==Guid.Empty||correlation==Guid.Empty||requested!=StrategyFixtures.At||entity!=new PortfolioFundId(1,2)||access.Roles is not ["PortfolioAdministrator"]||error is not (34002 or 34003))throw new InvalidDataException("Incorrect manual command envelope");
   if(commands.TryGetValue(commandId,out var original)&&!original.SequenceEqual(bytes))throw new InvalidDataException("Changed manual command bytes");
   if(Mode=="RejectCommand")return MessagePackSerializer.Serialize(new ServiceResult<Guid>{Success=false,ErrorCode=34003,ErrorMessage="Fixture expected-version rejection"},options);
   if(!commands.ContainsKey(commandId)){commands[commandId]=bytes.ToArray();if(Mode=="NoProjection")deferred=apply;else apply();}
   if(Mode=="LostAck"){Mode="Commands";return null;}
   return MessagePackSerializer.Serialize(new ServiceResult<GuidResult>{Success=true,ErrorMessage="",Value=new(commandId)},options);
  }catch(Exception failure){return MessagePackSerializer.Serialize(new ServiceResult<Guid>{Success=false,ErrorCode=34002,ErrorMessage=failure.Message},options);}
 }
}
