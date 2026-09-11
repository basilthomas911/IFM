using System.Text.Json;
using FluentAssertions;
using MessagePack;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Domain.Reference.ParameterSets.Command.Actor;
using TomasAI.IFM.Domain.Reference.ParameterSets.Command.Extensions;
using TomasAI.IFM.Domain.Reference.ParameterSets.Command.State;
using TomasAI.IFM.Domain.Reference.ParameterSets.Model;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Reference.UnitTests.ParameterSets;
public sealed class ParameterAssignmentActorTests
{
 [Fact] public async Task Assignment_uses_authoritative_version_and_replays_duplicate_commands_without_advancing()
 {
  var id=Guid.NewGuid();var payload=new RegimeDiscoveryParameterModel().CreateDraftPayload(id);
  var version=new ParameterSetVersion(new(id,1,RegimeDiscoveryParameterModel.ComponentCode,ParameterCanonicalPayloadModel.Hash(payload)),"Daily","",2,ParameterVersionStatus.Published,payload,DateTime.UtcNow,"test",DateTime.UtcNow);
  var set=new ParameterSetCommandState();set.Versions.Add(1,version);
  var context=Substitute.For<IParameterAssignmentCommandContext>();var logger=Substitute.For<ILogger<ParameterAssignmentCommandActor>>();
  context.ParameterSets.LoadStateAsync(Arg.Any<ICommand>()).Returns(new ValueTask<ParameterSetCommandState>(set));
  var scope=WorkflowParameterScopeModel.Create(IntrinsicTimeStrategyWorkflowDefinition.Id,TimeFrameType.Daily);
  var command=new AssignParameterVersionCommand{CommandId=Guid.NewGuid(),EntityId=new(WorkflowParameterScopeModel.AssignmentId(scope)),Scope=scope,Reference=version.Reference};
  command=MessagePackSerializer.Deserialize<AssignParameterVersionCommand>(MessagePackSerializer.Serialize(command));
  var state=new ParameterAssignmentCommandState();
  (await command.ExecuteAsync(context,state,logger)).Success.Should().BeTrue();state.Revision.Should().Be(1);state.Assignment!.Enabled.Should().BeTrue();
  (await command.ExecuteAsync(context,state,logger)).Success.Should().BeTrue();state.Revision.Should().Be(1);
  var disable=new DisableParameterAssignmentCommand{CommandId=Guid.NewGuid(),EntityId=command.EntityId,Scope=scope,Reference=version.Reference,ExpectedRevision=1};
  (await disable.ExecuteAsync(context,state,logger)).Success.Should().BeTrue();state.Revision.Should().Be(2);state.Assignment!.Enabled.Should().BeFalse();
  (await disable.ExecuteAsync(context,state,logger)).Success.Should().BeTrue();state.Revision.Should().Be(2);
 }
}
