using System.Text.Json;
using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.Reference.ParameterSets.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
namespace TomasAI.IFM.Domain.Reference.UnitTests.ParameterSets;
public sealed class SignalStartupPlanTests
{
 static ParameterStartupSnapshotModel Snapshot()
 {
  var payload=JsonSerializer.Serialize(RegimeDiscoveryParameterModel.CreateExplicitSeed(Guid.NewGuid()));
  var value=JsonSerializer.Deserialize<TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery.RegimeDiscoveryParameterSet>(payload)!;
  var version=new ParameterSetVersion(new(value.ParameterSetId,value.Version,RegimeDiscoveryParameterModel.ComponentCode,ParameterCanonicalPayloadModel.Hash(payload)),"Daily","",value.SchemaVersion,ParameterVersionStatus.Published,payload,DateTime.UtcNow,"test");
  var assignment=ParameterAssignmentModel.Assign(WorkflowParameterScopeModel.Create(IntrinsicTimeStrategyWorkflowDefinition.Id,TimeFrameType.Daily),version,null,0,DateTime.UtcNow,"test");
  return new(Guid.NewGuid(),[assignment],new Dictionary<ParameterVersionRef,ParameterSetVersion>{{version.Reference,version}});
 }
 [Fact]public void Reduced_horizon_retains_other_consumers_and_keeps_rsi_periods_distinct()
 {
  var snapshot=Snapshot();var plan=SignalStartupPlanModel.Create(snapshot,SignalStartupPlanModel.ExistingIntradayConsumers());
  plan.Steps.Should().Contain(x=>x.Key==new ParameterSignalProducerKey(ParameterSignalProducer.Rsi,TimeFrameType.FourHours,13));
  plan.Steps.Should().Contain(x=>x.Key==new ParameterSignalProducerKey(ParameterSignalProducer.Rsi,TimeFrameType.FifteenSeconds,14));
  plan.Steps.Should().NotContain(x=>x.Key==new ParameterSignalProducerKey(ParameterSignalProducer.Rsi,TimeFrameType.FourHours,14));
  plan.Steps.Select(x=>x.Key).Should().OnlyHaveUniqueItems();
  var reordered=SignalStartupPlanModel.Create(snapshot,SignalStartupPlanModel.ExistingIntradayConsumers().Reverse());
  reordered.Fingerprint.Should().Be(plan.Fingerprint);
 }
 [Fact]public void Unsupported_observation_cannot_be_enabled_for_regime_discovery()
 {
  var seed=RegimeDiscoveryParameterModel.CreateExplicitSeed(Guid.NewGuid());
  seed=seed with {ObservationMetrics=seed.ObservationMetrics!.Select(row=>row.Metric==ObservationMetricsType.Vwap
   ?row with {Enabled=true}:row).ToArray()};
  new RegimeDiscoveryParameterModel().Validate(JsonSerializer.Serialize(seed),ParameterSchemaRegistry.CurrentRegimeSchemaVersion)
   .Should().Contain(x=>x.Code=="PARAM.METRIC_UNSUPPORTED");
 }
 [Fact]public void Tdi_requires_its_own_rsi13_producer()
 {
  SignalStartupPlanModel.Dependencies(new(ParameterSignalProducer.Tdi,TimeFrameType.OneHour)).Should()
   .Contain(new ParameterSignalProducerKey(ParameterSignalProducer.Rsi,TimeFrameType.OneHour,13));
 }
 [Fact]public void Disabled_scope_is_distinct_from_absent_and_never_falls_back_implicitly()
 {
  var current=Snapshot();var assignment=current.Scopes.Single();
  var disabled=ParameterAssignmentModel.Disable(assignment,assignment.Revision,DateTime.UtcNow,"test");
  var snapshot=new ParameterStartupSnapshotModel(Guid.NewGuid(),[disabled],new Dictionary<ParameterVersionRef,ParameterSetVersion>());
  snapshot.HasScope(assignment.Scope).Should().BeTrue();snapshot.Assignments.Should().BeEmpty();
  var absent=new ParameterStartupSnapshotModel(Guid.NewGuid(),[],new Dictionary<ParameterVersionRef,ParameterSetVersion>());
  absent.Fingerprint.Should().NotBe(snapshot.Fingerprint);
 }
}

