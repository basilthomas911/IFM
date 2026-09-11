using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Realtime;
namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.RegimeDiscovery;
public sealed class ParameterRuntimeResolutionTests
{
 static IIntrinsicTimeStrategyWorkflowRealtimeContext Context(IParameterRuntimeSnapshot runtime)
 {
  var context=Substitute.For<IIntrinsicTimeStrategyWorkflowRealtimeContext>();
  context.TimeProvider.Returns(TimeProvider.System);context.Logger.Returns(NullLogger<IntrinsicTimeStrategyWorkflowRealtimeActor>.Instance);
  context.ParameterRuntime.Returns(runtime);return context;
 }
 static ExecuteRegimeDiscoveryPipelineCommand Command()=>new(){ExpiresAtUtc=DateTime.UtcNow.AddMinutes(1),RequestedAtUtc=DateTime.UtcNow,TargetHorizon=TimeFrameType.Daily};
 [Fact]public async Task Missing_startup_is_a_visible_terminal_initialization_outcome()
 {
  var runtime=Substitute.For<IParameterRuntimeSnapshot>();runtime.Enabled.Returns(true);
  var result=await Command().StartPipelineAsync(Context(runtime));
  result.Success.Should().BeFalse();result.Error!.ErrorCode.Should().Be("RD.INIT.PARAMETER_STARTUP_PENDING");
 }
 [Fact]public async Task Disabled_assignment_cannot_fall_back_to_legacy_configuration()
 {
  var runtime=Substitute.For<IParameterRuntimeSnapshot>();runtime.Enabled.Returns(true);runtime.RunId.Returns(Guid.NewGuid());
  runtime.Resolve(Arg.Any<string>(),Arg.Any<TimeFrameType>()).Returns(new ParameterRuntimeResolution(true,true,null));
  var context=Context(runtime);var result=await Command().StartPipelineAsync(context);
  result.Error!.ErrorCode.Should().Be("RD.INIT.ASSIGNMENT_DISABLED");context.ConfigurationDb.ReceivedCalls().Should().BeEmpty();
 }
 [Fact]public async Task Generic_runtime_payload_still_requires_its_exact_recorded_hash()
 {
  var parameters=RegimeDiscoveryParameterSet.CreateDefault(Guid.NewGuid(),Guid.NewGuid(),TimeFrameType.Daily);
  var reference=new ParameterVersionRef(parameters.ParameterSetId,parameters.Version,ParameterSchemaRegistry.RegimeComponent,new string('a',64));
  var version=new ParameterSetVersion(reference,"test","",1,ParameterVersionStatus.Published,RegimeDiscoveryParameterPayload.Serialize(parameters),DateTime.UtcNow,"test");
  var scope=new ParameterAssignmentScope("strategy-workflow","test","regime-discovery",reference.ComponentCode,"{}","");
  var assignment=new ParameterAssignmentRevision(Guid.NewGuid(),scope,1,reference,true,ParameterApplicationPolicy.NextStartup,DateTime.UtcNow,"test");
  var run=Guid.NewGuid();var runtime=Substitute.For<IParameterRuntimeSnapshot>();runtime.Enabled.Returns(true);runtime.RunId.Returns(run);
  runtime.Resolve(Arg.Any<string>(),Arg.Any<TimeFrameType>()).Returns(new ParameterRuntimeResolution(true,false,new(run,assignment,version)));
  var result=await Command().StartPipelineAsync(Context(runtime));
  result.Error!.ErrorCode.Should().Be("RD.INIT.CONFIGURATION_HASH");
 }
}
