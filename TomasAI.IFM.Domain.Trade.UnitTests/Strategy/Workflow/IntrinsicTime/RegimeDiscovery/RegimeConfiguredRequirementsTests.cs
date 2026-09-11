using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Model;
namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.RegimeDiscovery;
public sealed class RegimeConfiguredRequirementsTests
{
 static RegimeDiscoveryParameterSet Explicit()
 {
  var legacy=RegimeDiscoveryParameterSet.CreateDefault(Guid.NewGuid(),Guid.NewGuid(),TimeFrameType.Daily);
  var request=RegimeDiscoverySnapshotRequestFactory.Create(MarketSeriesIdentity.ForContract("ES20260918"),legacy);
  return legacy with {SchemaVersion=2,SignalRequirements=request.Requirements.Select(x=>new RegimeDiscoverySignalConfiguration
  {RequirementId=Guid.NewGuid(),Metric=x.Metric,TimeFrame=x.TimeFrame,IsRequired=x.IsRequired,MaximumAgeSeconds=x.MaximumAgeSeconds,CalculationConfigurationId=x.CalculationConfigurationId}).ToArray()};
 }
 [Fact] public void Explicit_default_preserves_all_69_requests()
 {
  RegimeDiscoverySnapshotRequestFactory.Create(MarketSeriesIdentity.ForContract("ES20260918"),Explicit()).Requirements.Should().HaveCount(69);
 }
 [Fact] public void Disabling_optional_inputs_requests_only_the_35_mandatory_inputs()
 {
  var value=Explicit();value=value with {SignalRequirements=value.SignalRequirements!.Select(x=>x with {Enabled=x.IsRequired}).ToArray()};
  var result=RegimeDiscoverySnapshotRequestFactory.Create(MarketSeriesIdentity.ForContract("ES20260918"),value);
  result.Requirements.Should().HaveCount(35).And.OnlyContain(x=>x.IsRequired);
 }
 [Fact] public void Direct_runtime_request_cannot_bypass_mandatory_validation()
 {
  var value=Explicit();value=value with {SignalRequirements=value.SignalRequirements!.Select(x=>x with {Enabled=false}).ToArray()};
  Action action=()=>RegimeDiscoverySnapshotRequestFactory.Create(MarketSeriesIdentity.ForContract("ES20260918"),value);
  action.Should().Throw<ArgumentException>().WithMessage("Required calculation dependency*");
 }
 [Fact] public void Schema_five_uses_the_editor_projection_for_the_runtime_snapshot_request()
 {
  var value=Explicit();
  value=value with
  {
   Horizon=value.Horizon with {TimeFrames=value.Horizon.TimeFrames.Select(x=>x with {IsRequired=true}).ToArray()}
  };
  var baseline=RegimeDiscoverySnapshotRequestFactory.Create(MarketSeriesIdentity.ForContract("ES20260918"),
   value with {SchemaVersion=1,SignalRequirements=null});
  value=value with
  {
   SchemaVersion=5,
   SignalRequirements=baseline.Requirements.Select(x=>new RegimeDiscoverySignalConfiguration
   {RequirementId=Guid.NewGuid(),Metric=x.Metric,TimeFrame=x.TimeFrame,IsRequired=true,Enabled=true,
    MaximumAgeSeconds=x.MaximumAgeSeconds,CalculationConfigurationId=x.CalculationConfigurationId}).ToArray()
  };
  var result=RegimeDiscoverySnapshotRequestFactory.Create(MarketSeriesIdentity.ForContract("ES20260918"),value);
  result.Requirements.Should().BeEquivalentTo(value.SignalRequirements.Where(x=>x.Enabled),options=>options
   .ExcludingMissingMembers()
   .WithStrictOrdering());
 } [Fact] public void Schema_three_requests_every_included_signal_as_required()
 {
  var value=Explicit();
  value=value with {SchemaVersion=3,
   Horizon=value.Horizon with {TimeFrames=value.Horizon.TimeFrames.Select(x=>x with {IsRequired=true}).ToArray()}};
  // Structure follows the longest included interval, so construct that exact catalogue.
  var baseline=RegimeDiscoverySnapshotRequestFactory.Create(MarketSeriesIdentity.ForContract("ES20260918"),value with {SchemaVersion=1,SignalRequirements=null});
  value=value with {SignalRequirements=baseline.Requirements.Select(x=>new RegimeDiscoverySignalConfiguration
   {RequirementId=Guid.NewGuid(),Metric=x.Metric,TimeFrame=x.TimeFrame,IsRequired=true,Enabled=true,MaximumAgeSeconds=x.MaximumAgeSeconds,CalculationConfigurationId=x.CalculationConfigurationId}).ToArray()};
  var result=RegimeDiscoverySnapshotRequestFactory.Create(MarketSeriesIdentity.ForContract("ES20260918"),value);
  result.Requirements.Should().HaveCount(69).And.OnlyContain(x=>x.IsRequired);
  value=value with {SignalRequirements=value.SignalRequirements.Select(x=>x.Metric==TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery.RegimeDiscoverySignalMetric.Tdi?x with {IsRequired=false}:x).ToArray()};
  Action action=()=>RegimeDiscoverySnapshotRequestFactory.Create(MarketSeriesIdentity.ForContract("ES20260918"),value);
  action.Should().Throw<ArgumentException>().WithMessage("Schemas 3 through 5 require*");
 }
}
