using MessagePack;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Position;
using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
/// <summary>Isolated reduce-only contract oracle; no real actor or storage.</summary>
internal static class CloseOrderResponder{
 static string mode="Disabled";static int writes;static readonly Dictionary<Guid,byte[]> receipts=[];
 internal static readonly StrategyPositionId Id=StrategyPositionId.Create(new(1,2,700,455),TradeStrategyKind.FuturesOutright);
 static readonly Guid Leg=Guid.Parse("22223333-4455-6677-8899-aabbccddeeff");
 public static bool Handles(string s)=>s.StartsWith("Query.NativeCompatibility.CloseOrder",StringComparison.Ordinal)||mode!="Disabled"&&(s.StartsWith("Query.FuturesTradePositionQuery.GetFuturesTradePosition.",StringComparison.Ordinal)||s.StartsWith("Function.PortfolioCloseOrderCompositionFunction.EvaluateClose.",StringComparison.Ordinal));
 public static byte[]? Handle(string s,byte[] payload){
  var o=MessagePackBinarySerializer.Options;byte[] Result<T>(T v)=>MessagePackSerializer.Serialize(new ServiceResult<T>{Success=true,Value=v,ErrorMessage=""},o);
  if(s=="Query.NativeCompatibility.CloseOrderMode.none"){mode=MessagePackSerializer.Deserialize<string>(payload,o);writes=0;receipts.Clear();return Result(new ScalarReadModel<bool>(true));}
  if(s=="Query.NativeCompatibility.CloseOrderRelease.none"){mode="Normal";return Result(new ScalarReadModel<bool>(true));}
  if(s=="Query.NativeCompatibility.CloseOrderCount.none")return Result(new ScalarReadModel<int>(writes));
  if(s.StartsWith("Query.",StringComparison.Ordinal)){
   var q=MessagePackSerializer.Deserialize<GetFuturesTradePositionQuery>(payload,o);if(q.PositionId!=Id||q.Subject.ToString()!=s)throw new InvalidDataException("Closing current-position scope changed");
   return Result(new StrategyPositionSnapshot{Id=mode=="WrongScope"?StrategyPositionId.Create(new(1,3,700,455),TradeStrategyKind.FuturesOutright):Id,StrategyKind=TradeStrategyKind.FuturesOutright,IsOpen=mode!="Closed",Legs=[new(){TradeLegId=Leg,ContractId="ESZ6",AssetFamily=TradeAssetFamily.Futures,SignedQuantity=mode=="Quantity"?2:1}]});
  }
  var c=MessagePackSerializer.Deserialize<EvaluatePortfolioCloseOrderCompositionCommand>(payload,o);
  if(c.Subject.ToString()!=s||c.CommandId!=c.OperationId||c.CorrelationId!=c.OperationId||c.CausationId!=c.Body.CompositionId||c.PortfolioId!=1||!DesktopFixtureMode.Clock(c.RequestedAtUtc,StrategyFixtures.At)||c.ExpiresAtUtc!=StrategyFixtures.At.AddMinutes(5)||c.InputSha256!=FinancialCanonicalHash.Request(c)||c.Access.Roles is not ["OrderCompositionClose"]||!c.Body.WorkflowId.IsValid||c.Body.Position.Id.PositionId!=Id.PositionId||c.Body.Component.ReservedTradeId!=455||c.Body.Component.PermitBalancedPartialAcceptance||c.Body.Component.Legs is not [{TradeLegId:var leg,SignedQuantity:-1,ContractId:"ESZ6"}]||leg!=Leg)throw new InvalidDataException("Closing submission changed identity/hash/reduce-only intent");
  if(receipts.TryGetValue(c.OperationId,out var prior))return prior;writes++;
  var instruction=new PortfolioExecutionOrderInstruction{Id=new(1,2,701),Status=PortfolioExecutionOrderStatus.Ready,ValueDate=c.Body.ValueDate,ValidUntilUtc=c.Body.ValidUntilUtc,Origin=c.Body.Origin,Components=[c.Body.Component],PositionType=PortfolioExecutionPositionType.Closing,TargetPosition=c.Body.Position.Id,SetupTrade=c.Body.SetupTrade};
  OrderPreparationResponder.Accepted=TomasAI.IFM.Domain.Trade.Shared.Portfolio.PortfolioExecutionContractMapper.ToTradeOrder(instruction);
  var terminal=new PortfolioCloseOrderCompositionCompletedEvent{Id=Guid.Parse("95112233-4455-6677-8899-aabbccddeeff"),OperationId=c.OperationId,PortfolioId=1,InputHash=c.InputSha256,Receipt=new(){CompositionId=c.Body.CompositionId,WorkflowId=c.Body.WorkflowId,PortfolioId=1,Status=PortfolioCloseOrderCompositionStatus.ExecuteTradeOrder,TradeOrder=instruction}};
  var reply=Result(FunctionResult<PortfolioCloseOrderCompositionCompletedEvent,PortfolioCloseOrderCompositionFailedEvent>.Complete(terminal));receipts[c.OperationId]=reply;return mode=="LostAck"?null:reply;
 }
}
