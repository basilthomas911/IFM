using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.RegimeDiscovery;
public sealed class ParameterProvenanceTests
{
 [Fact]public void Startup_assignment_provenance_survives_every_completion_wire_contract()
 {
  var provenance=new ParameterApplicationProvenance(Guid.NewGuid(),Guid.NewGuid(),4);
  Roundtrip(new ExecuteRegimeDiscoveryPipelineCommand{ParameterApplication=provenance}).ParameterApplication.Should().Be(provenance);
  Roundtrip(new RegimeDiscoveryPipelineCompletedEvent{ParameterApplication=provenance}).ParameterApplication.Should().Be(provenance);
  Roundtrip(new CompleteRegimeDiscoveryCommand{ParameterApplication=provenance}).ParameterApplication.Should().Be(provenance);
  Roundtrip(new IntrinsicTimeStrategyWorkflowView{RegimeDiscoveryParameterApplication=provenance}).RegimeDiscoveryParameterApplication.Should().Be(provenance);
  Roundtrip(new IntrinsicTimeStrategyWorkflowState{RegimeDiscoveryParameterApplication=provenance}).RegimeDiscoveryParameterApplication.Should().Be(provenance);
  Roundtrip(new CompleteRegimeDiscoveryCommand()).ParameterApplication.Should().BeNull();
 }
 static T Roundtrip<T>(T value)=>MessagePackSerializer.Deserialize<T>(MessagePackSerializer.Serialize(value));
}
