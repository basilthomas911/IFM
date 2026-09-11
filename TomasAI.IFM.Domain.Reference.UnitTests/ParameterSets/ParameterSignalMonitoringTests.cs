using System.Text.Json;
using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Domain.Reference.ParameterSets.Model;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
namespace TomasAI.IFM.Domain.Reference.UnitTests.ParameterSets;
public sealed class ParameterSignalMonitoringTests
{
 [Fact]public async Task Monitoring_uses_exact_enabled_rows_ages_and_keeps_missing_observations_visible()
 {
  var value=RegimeDiscoveryParameterModel.CreateExplicitSeed(Guid.NewGuid());
  var selectedMetric=value.SignalMetrics!.First(x=>x.Enabled&&x.Metric==SignalMetricsType.Macd);
  var signals=value.SignalMetrics.Select(x=>x with {Monitor=x.RequirementId==selectedMetric.RequirementId,
   MaximumAgeSeconds=x.RequirementId==selectedMetric.RequirementId?17:x.MaximumAgeSeconds}).ToArray();
  value=value with {SignalMetrics=signals,SignalRequirements=RegimeDiscoveryMetricConfigurationProjection.ToLegacyRequirements(
   value.ParameterSetId,signals,value.ObservationMetrics!.Select(x=>x with {Monitor=false}).ToArray(),value.Horizon),ObservationMetrics=value.ObservationMetrics.Select(x=>x with {Monitor=false}).ToArray()};
  var selected=value.SignalRequirements!.First(x=>x.MaximumAgeSeconds==17);
  var json=JsonSerializer.Serialize(value);
  var version=new ParameterSetVersion(new(value.ParameterSetId,1,RegimeDiscoveryParameterModel.ComponentCode,ParameterCanonicalPayloadModel.Hash(json)),"Test","",value.SchemaVersion,ParameterVersionStatus.Published,json,DateTime.UtcNow,"test");
  var scope=WorkflowParameterScopeModel.Create(IntrinsicTimeStrategyWorkflowDefinition.Id,value.TargetHorizon);
  var assignment=ParameterAssignmentModel.Assign(scope,version,null,0,DateTime.UtcNow,"test");
  var id=Guid.NewGuid();var frozen=new ParameterStartupSnapshotModel(id,[assignment],new Dictionary<ParameterVersionRef,ParameterSetVersion>{{version.Reference,version}});
  var run=new ParameterStartupRun(id,[assignment],[version],SignalStartupPlanModel.Create(frozen,[]),DateTime.UtcNow,"test");
  var provider=Substitute.For<IRegimeDiscoveryMarketSignalSnapshotProvider>();
  RegimeDiscoveryMarketSignalSnapshotRequest? request=null;
  provider.CaptureAsync(Arg.Any<RegimeDiscoveryMarketSignalSnapshotRequest>(),Arg.Any<CancellationToken>()).Returns(call=>
  {
   request=call.Arg<RegimeDiscoveryMarketSignalSnapshotRequest>();
   return ValueTask.FromResult(new RegimeDiscoveryMarketSignalSnapshotResult{IsSuccess=true,Snapshot=new(){Observations=request.Requirements.Select(x=>new RegimeDiscoverySignalObservation{
    Metric=x.Metric,SignalKey=new MarketAnalyticsSignalKey{TimeFrame=x.TimeFrame},Availability=RegimeDiscoverySignalAvailability.Missing}).ToArray()}});
  });
  var result=await ParameterSignalMonitoringModel.CaptureAsync(run,"ES-test",provider,CancellationToken.None);
  request!.Requirements.Should().ContainSingle().Which.MaximumAgeSeconds.Should().Be(17);
  result.Rows.Should().ContainSingle().Which.Observation.Availability.Should().Be(RegimeDiscoverySignalAvailability.Missing);
  result.Rows[0].RequirementId.Should().Be(selected.RequirementId);
  result.Rows[0].AssignmentRevision.Should().Be(assignment.Revision);
  result.Rows[0].IsRequired.Should().Be(selected.IsRequired);
 }
}




