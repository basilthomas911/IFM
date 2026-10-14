using NSubstitute;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.Rust.Bridge.Net;
public class InspectionTests
{
 [Fact] public async Task Gateway_deduplicates_and_retains_history_while_opening_exact_details()
 {
  int calls=0;var gateway=new InspectionActionGateway(input=>{calls++;return Task.FromResult(input.Intent=="history"?new InspectionResult("risk",[new("id","History row",new())],PagingState:"next-token"):new InspectionResult("risk",[],Decision:"CURRENT AUTHORITY: Not authorized"));});
  var request=new InspectionIntent(1,Guid.NewGuid(),"risk","history",new(){["portfolio"]="1"});
  Assert.Equal(0,gateway.Submit(request));await gateway.DrainAsync();Assert.Equal(0,gateway.Submit(request with{Fields=new(){["portfolio"]="1"}}));Assert.Equal(1,calls);
  Assert.Equal(0,gateway.Submit(request with{RequestId=Guid.NewGuid(),Intent="open"}));await gateway.DrainAsync();var state=gateway.Snapshot();Assert.Single(state.Result.Rows);Assert.Equal("next-token",state.Result.PagingState);Assert.Contains("Not authorized",state.Result.Decision);
  Assert.Equal(-1,gateway.Submit(request with{RequestId=Guid.NewGuid(),Intent="submit-order"}));
 }
 [Fact] public async Task Exact_risk_reads_refresh_authority_and_preserve_the_frozen_calculation()
 {
  var risk=Substitute.For<IRiskQueryApi>();var workflow=new StrategyWorkflowId(Guid.NewGuid());var invocation=Guid.NewGuid();
  RiskObservation Observation(string authority)=>new(new(){State=new(){WorkflowId=workflow}},new(){ResultId=invocation,Outcome=RiskAssessmentOutcome.Approved,StrategyUnits=2},authority,"RiskApproved",DateTime.UtcNow,null,false);
  risk.GetInvocationAsync(workflow,invocation,Arg.Any<CancellationToken>()).Returns(new ServiceOk<RiskObservation>(Observation("Authorized")),new ServiceOk<RiskObservation>(Observation("Expired")));
  using var backend=new StrategyInspectionBackend(Substitute.For<IMarketConditionAssessmentQueryApi>(),Substitute.For<IIntrinsicTimeStrategyWorkflowQueryApi>(),risk);
  var request=new InspectionIntent(1,Guid.NewGuid(),"risk","open",new(){["workflow"]=workflow.Value.ToString("D"),["invocation"]=invocation.ToString("D")});
  var first=await backend.ExecuteAsync(request);var second=await backend.ExecuteAsync(request with{RequestId=Guid.NewGuid()});
  Assert.Contains("CURRENT AUTHORITY: Authorized",first.Decision);Assert.Contains("CURRENT AUTHORITY: Expired",second.Decision);Assert.Contains("Historical calculation: Approved; units: 2",second.Decision);Assert.Contains("RiskApproved",second.Evidence);
  await risk.Received(2).GetInvocationAsync(workflow,invocation,Arg.Any<CancellationToken>());
 }
}
