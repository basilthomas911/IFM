using MessagePack;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Order;
using TomasAI.IFM.Domain.Trade.Order.Query.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
internal static class OrderPreparationResponder {
 static string mode="Normal";static int count;static int writes;
 static readonly Dictionary<Guid,byte[]> receipts=[];
 internal static TradeOrderDefinition? Accepted;
 public static bool Handles(string subject)=>subject.StartsWith("Query.NativeCompatibility.OrderPreparation",StringComparison.Ordinal)||subject.StartsWith("Query.TradeOrderQuery.PrepareTradeOrder.",StringComparison.Ordinal)||subject.StartsWith("Function.PortfolioOrderCompositionFunction.Evaluate.",StringComparison.Ordinal);
 public static byte[]? Handle(string subject,byte[] payload){
  var options=MessagePackBinarySerializer.Options;
  byte[] Result<T>(T value)=>MessagePackSerializer.Serialize(new ServiceResult<T>{Success=true,ErrorMessage="",Value=value},options);
  if(subject=="Query.NativeCompatibility.OrderPreparationMode.none"){mode=MessagePackSerializer.Deserialize<string>(payload,options);count=0;writes=0;receipts.Clear();Accepted=null;return Result(new ScalarReadModel<bool>(true));}
  if(subject=="Query.NativeCompatibility.OrderPreparationReleaseReceipt.none"){mode="Normal";return Result(new ScalarReadModel<bool>(true));}
  if(subject=="Query.NativeCompatibility.OrderPreparationCount.none")return Result(new[]{count,writes});
  if(subject.StartsWith("Function.",StringComparison.Ordinal)){
   var c=MessagePackSerializer.Deserialize<EvaluatePortfolioOrderCompositionCommand>(payload,options);
   if(c.CommandId!=c.OperationId||c.CorrelationId!=c.OperationId||c.CausationId!=c.Body.CompositionId||c.EntityId.Format()!=subject[(subject.LastIndexOf("Evaluate.",StringComparison.Ordinal)+9)..]||c.Subject.ToString()!=subject||c.PortfolioId!=1||!DesktopFixtureMode.Clock(c.RequestedAtUtc,StrategyFixtures.At)||c.ExpiresAtUtc!=StrategyFixtures.At.AddMinutes(5)||c.InputSha256!=FinancialCanonicalHash.Request(c)||!c.Access.Roles.SequenceEqual(new[]{"OrderCompositionEvaluate"}))throw new InvalidDataException("Invalid native Portfolio submission identity, hash or API clock");
   if(receipts.TryGetValue(c.OperationId,out var prior))return prior;
   writes++;
   var instruction=new PortfolioExecutionOrderInstruction{Id=new(1,2,701),Status=PortfolioExecutionOrderStatus.Ready,ValueDate=c.Body.ValueDate,ValidUntilUtc=c.Body.ValidUntilUtc,Origin=c.Body.Origin,Components=c.Body.Components,PositionType=PortfolioExecutionPositionType.Opening,BrokerAccountAlias=c.Body.BrokerAccountAlias,BrokerEnvironment=c.Body.BrokerEnvironment,SetupTrade=c.Body.SetupTrade,TimeInForce=c.Body.TimeInForce,AlgorithmPace=c.Body.AlgorithmPace};
   var terminal=new PortfolioOrderCompositionCompletedEvent{Id=Guid.Parse("95112233-4455-6677-8899-aabbccddeeff"),OperationId=c.OperationId,PortfolioId=c.PortfolioId,InputHash=c.InputSha256,Receipt=new(){CompositionId=c.Body.CompositionId,WorkflowId=c.Body.WorkflowId,Status=mode=="Decline"?PortfolioOrderCompositionStatus.NoTradeOrders:PortfolioOrderCompositionStatus.ExecuteTradeOrders,TradeOrders=mode=="Decline"?[]:[instruction]}};
   Accepted=TomasAI.IFM.Domain.Trade.Shared.Portfolio.PortfolioExecutionContractMapper.ToTradeOrder(instruction);
   var receipt=Result(FunctionResult<PortfolioOrderCompositionCompletedEvent,PortfolioOrderCompositionFailedEvent>.Complete(terminal));receipts[c.OperationId]=receipt;
   if(mode=="LostAck")return null;return receipt;
  }
  count++;var q=MessagePackSerializer.Deserialize<PrepareTradeOrderQuery>(payload,options);
  if(q.Subject.ToString()!=subject||q.Request.PortfolioId!=1||q.Request.Draft.SetupTrade is not{FundId:2,OrderId:123,TradeId:456}||q.Request.Draft.BrokerAccountAlias!="IFM-EMULATOR-PAPER"||q.Request.Draft.BrokerEnvironment!=PortfolioBrokerEnvironment.Emulator||q.Request.Quantity!=1)throw new InvalidDataException("Incorrect native order preparation scope");
  var underlying=new TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesContractV3ReadModel{ContractId="ESZ6",Symbol="ES",Multiplier="50",Exchange="CME",Currency="USD"};
  var prepared=TradeOrderPreparationCompute.Calculate(q.Request,underlying,new(2026,10,26),StrategyFixtures.At,q.Request.Draft.StrategyKind==PortfolioExecutionStrategyKind.FuturesOutright?.25m:.05m,2.50m,new(TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog.StrategyCatalogKind.Deployment,Guid.Parse("11112233-4455-6677-8899-aabbccddeeff"),1),"Intraday","Synthetic offline fee schedule");
  if(mode=="WrongScope")prepared=prepared with{Candidate=prepared.Candidate with{SetupTrade=prepared.Candidate.SetupTrade! with{FundId=3}}};
  if(mode=="WrongLeg")prepared=prepared with{Candidate=prepared.Candidate with{Components=[prepared.Candidate.Components[0] with{Legs=[prepared.Candidate.Components[0].Legs[0] with{ContractId="OTHER"}]}]}};
  return Result(prepared);
 }
}
