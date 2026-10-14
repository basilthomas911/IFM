using NSubstitute;
using TomasAI.IFM.UI.Rust.Bridge.Net;
using TomasAI.IFM.UI.Net.Models.MarketData;
using TomasAI.IFM.UI.Net.Services.MarketData;
using TomasAI.IFM.UI.Net.Services.Operations;
public class OperationsHealthBridgeTests
{
 [Fact]public async Task Shared_projection_exposes_all_metrics_and_clears_previous_green_values_on_failure()
 {
  var service=Substitute.For<IMarketDataOperationsHealthQueryService>();service.GetAsync(Arg.Any<CancellationToken>()).Returns(
   UiOperationResult<MarketDataOperationsHealthSnapshot>.Success(new(){OverallStatus="Green",ObservedOnUtc=DateTime.UtcNow,Stages=[new(){Stage="TickAggregation",Status="Green",Pending=4,HighWater=18,Reason="Fixture"}],Datasets=[new(){Dataset="GLBX.MDP3",ProcessId=123,GenerationId=Guid.NewGuid(),Status="Green",Running=true}]}),
   UiOperationResult<MarketDataOperationsHealthSnapshot>.Failure(1,"Fixture endpoint unavailable"));
  using var session=new OperationsHealthSession(service);var ready=await session.RefreshAsync();Assert.Equal(2,ready.Rows.Length);Assert.Equal("4",ready.Rows[0].Fields["Pending"]);Assert.Equal("18",ready.Rows[0].Fields["HighWater"]);Assert.Equal("123",ready.Rows[1].Fields["ProcessId"]);
  var failed=await session.RefreshAsync();Assert.Empty(failed.Rows);Assert.Equal("Unavailable",failed.Context!["status"]);Assert.Contains("unavailable",failed.Context["summary"]);
 }
 [Fact]public async Task Readonly_health_rejects_commands_and_refreshes_without_a_synthetic_user_request()
 {
  var gate=new ContractEditorGateway(_=>Task.FromResult(new ContractEditorResult("operations-health",[],[])));var load=new ContractEditorIntent(1,Guid.NewGuid(),"operations-health","load",0,null,[]);
  Assert.Equal(-1,gate.Submit(load with{Intent="run"}));Assert.Equal(0,gate.Submit(load));await gate.DrainAsync();var before=gate.Snapshot();
  Assert.False(gate.TryRefresh("scheduled-tasks",()=>throw new Exception("Wrong view")));Assert.True(gate.TryRefresh("operations-health",()=>Task.FromResult(new ContractEditorResult("operations-health",[new("stage:Updated",[])],[]))));await gate.DrainAsync();var after=gate.Snapshot();Assert.True(after.Generation>before.Generation);Assert.Single(after.Outcomes);Assert.Single(after.Result.Rows);Assert.False(after.Busy);
 }
}
