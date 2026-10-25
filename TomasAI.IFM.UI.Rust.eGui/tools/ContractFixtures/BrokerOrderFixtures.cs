using MessagePack;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Order.Broker;
using TomasAI.IFM.UI.Rust.Bridge.Net;
/// <summary>Synthetic immutable execution evidence, never dispatched to a broker.</summary>
internal static class BrokerOrderFixtures {
 internal static readonly Guid Attempt=Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");
 internal static readonly Guid Component=Guid.Parse("721dd687-7a5a-4fa6-a404-01545cbab900");
 internal static readonly TradeOrderId Scope=new(1,2,123);
 internal static BrokerOrderDefinition Order=>new(){Id=new(new(Scope,Attempt),Component),Status=BrokerOrderStatus.Working,Revision=5,BrokerRevision=2,CurrentSignedNetDebitLimit=6050.2500m,ChangedAtUtc=StrategyFixtures.At,Order=new(){Id=Scope,ValidUntilUtc=StrategyFixtures.At.AddMinutes(5),Components=[new(){ComponentId=Component,StrategyKind=TradeStrategyKind.FuturesOutright,ReservedTradeId=456,TickIncrement=.25m,MinimumSignedNetDebitLimit=6000m,MaximumSignedNetDebitLimit=6100m,SignedNetDebitLimit=6050.2500m,Legs=[new(){TradeLegId=Component,AssetFamily=TradeAssetFamily.Futures,SignedQuantity=3,ContractId="ESZ6",CashMultiplier=50m}]}]}};
 internal static OrderExecutionDefinition Execution=>new(){TradeOrderId=Scope,ExecutionAttemptId=Attempt,Status=OrderExecutionStatus.PartiallyFilled,OrderQuantity=3,CumulativeFilledQuantity=1,Components=Order.Order.Components,Order=Order.Order,StartedAtUtc=StrategyFixtures.At,Fills=[new(){ExecutionFillId=Guid.Parse("90112233-4455-6677-8899-aabbccddeeff"),ExecutionAttemptId=Attempt,ComponentId=Component,TradeLegId=Component,SignedQuantity=1,Price=6050.2500m,Commission=2.50m,FilledAtUtc=StrategyFixtures.At,ContractId="ESZ6"}]};
 internal static void Export(string output,object formatter){
  var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;var type=formatter.GetType();
  void Field(string name,object? value)=>type.GetField(name,flags)!.SetValue(formatter,value);
  var orders=new[]{Order};var executions=new[]{Execution};Field("brokerOrders",orders);Field("executions",executions);Field("evidenceOrders",null);Field("evidenceExecutions",null);
  var rows=(ContractEditorRow[])type.GetMethod("EvidenceRows",flags)!.Invoke(formatter,null)!;
  FixtureFiles.WriteAllBytes(Path.Combine(output,"broker-evidence-rows.msgpack"),MessagePackSerializer.Serialize(rows,MessagePackBinarySerializer.ContentOptions));
  FixtureFiles.WriteAllBytes(Path.Combine(output,"broker-evidence-order.msgpack"),MessagePackSerializer.Serialize(Order,MessagePackBinarySerializer.ContentOptions));
  FixtureFiles.WriteAllBytes(Path.Combine(output,"broker-evidence-execution.msgpack"),MessagePackSerializer.Serialize(Execution,MessagePackBinarySerializer.ContentOptions));
 }
}
