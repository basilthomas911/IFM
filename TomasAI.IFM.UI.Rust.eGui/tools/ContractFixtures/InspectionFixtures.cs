using MessagePack;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
using TomasAI.IFM.UI.Net.ViewModels.Strategy;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
/// <summary>Frozen read-only risk evidence oracle; no workflow or financial command is executed.</summary>
internal static class InspectionFixtures {
 public static readonly Guid Invocation=Guid.Parse("c154e9a4-bec4-4335-a3a6-7bdc9d370fa2");
 public static readonly RiskObservation Observation=new(new WorkflowStrategyStateUpdatedEvent{State=StrategyFixtures.View,WorkflowId=StrategyFixtures.View.WorkflowId,WorkflowRevision=StrategyFixtures.View.WorkflowRevision,UpdatedAtUtc=StrategyFixtures.At},null,"NotAuthorized","NotRequested",StrategyFixtures.At,null,false);
 public static readonly RiskAssessmentResult Calculation=new(){ResultId=Guid.Parse("d154e9a4-bec4-4335-a3a6-7bdc9d370fa2"),WorkflowId=StrategyFixtures.View.WorkflowId,EntityId=StrategyFixtures.View.EntityId,InvocationId=Invocation,InputWorkflowRevision=9,EvaluatedAtUtc=StrategyFixtures.At.AddTicks(1234567),ProducedAtUtc=StrategyFixtures.At,ValidUntilUtc=StrategyFixtures.At.AddMinutes(2),Outcome=RiskAssessmentOutcome.Rejected,StrategyUnits=2,Requirements=new(){Currency="USD",SettlementCash=1234.5000m,MarginFunding=9000.25m,FeeReserve=12.34m,VariationReserve=0.00001m},Reasons=["Fixture only","No execution authority"],PortfolioId=1,FundId=2,TargetHorizon=TomasAI.IFM.Domain.MarketData.Analytics.Shared.TimeFrameType.Daily};
 public static void Export(string output){
  var bytes=MessagePackSerializer.Serialize(Observation,MessagePackBinarySerializer.Options);
  FixtureFiles.WriteAllBytes(Path.Combine(output,"risk-observation.msgpack"),bytes);
  var transported=MessagePackSerializer.Deserialize<RiskObservation>(bytes,MessagePackBinarySerializer.Options);
  FixtureFiles.WriteAllText(Path.Combine(output,"risk-observation-display.txt"),RiskObservationPresentation.Render(transported));
  FixtureFiles.WriteAllText(Path.Combine(output,"risk-observation-evidence.txt"),TomasAI.IFM.UI.Rust.Bridge.Net.BridgeDocument.Display(TomasAI.IFM.UI.Rust.Bridge.Net.BridgeDocument.Project(transported,enumNames:true)));
  var completed=Observation with{Calculation=Calculation,Snapshot=Observation.Snapshot with{State=StrategyFixtures.View with{RiskExplanation=new(){AvailableCash=12345.67m,EffectiveLossBudget=1000m,MarketMultiplier=0.20m,Conditions=["Frozen historical conditions"]}}}};
  var richBytes=MessagePackSerializer.Serialize(completed,MessagePackBinarySerializer.Options);
  FixtureFiles.WriteAllBytes(Path.Combine(output,"risk-completed.msgpack"),richBytes);
  FixtureFiles.WriteAllText(Path.Combine(output,"risk-completed-display.txt"),RiskObservationPresentation.Render(MessagePackSerializer.Deserialize<RiskObservation>(richBytes,MessagePackBinarySerializer.Options)));
  FixtureFiles.WriteAllText(Path.Combine(output,"risk-completed-evidence.txt"),TomasAI.IFM.UI.Rust.Bridge.Net.BridgeDocument.Display(TomasAI.IFM.UI.Rust.Bridge.Net.BridgeDocument.Project(MessagePackSerializer.Deserialize<RiskObservation>(richBytes,MessagePackBinarySerializer.Options),enumNames:true)));
  InspectionJsonFixtures.Export(output);
 }
}
