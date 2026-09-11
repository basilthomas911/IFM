using System.Collections;
using System.Reflection;
using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Domain.Reference.ParameterSets.Command.Actor;
using TomasAI.IFM.Domain.Reference.ParameterSets.Command.Extensions;
using TomasAI.IFM.Domain.Reference.ParameterSets.Command.State;
using TomasAI.IFM.Domain.Reference.ParameterSets.Model;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
namespace TomasAI.IFM.Domain.Reference.UnitTests.ParameterSets;
public sealed class ParameterSetActorVerificationTests
{
 [Fact] public void Command_maps_cover_every_supported_contract()
 {
  var type=typeof(ParameterSetCommandActor);
  var receive=(IDictionary)type.GetField("_receiveMap",BindingFlags.NonPublic|BindingFlags.Static)!.GetValue(null)!;
  var validate=(IDictionary)type.GetField("_validationMap",BindingFlags.NonPublic|BindingFlags.Static)!.GetValue(null)!;
  var parse=(IDictionary)type.GetField("_parseMap",BindingFlags.NonPublic|BindingFlags.Static)!.GetValue(null)!;
  receive.Keys.Cast<Type>().Should().BeEquivalentTo(validate.Keys.Cast<Type>());
  foreach(var contract in receive.Keys.Cast<Type>())parse.Contains(contract.GetField("Verb")!.GetValue(null)!).Should().BeTrue();
 }
 [Fact] public async Task Serialized_command_maps_to_extension_and_duplicate_identity_is_idempotent()
 {
  var id=Guid.NewGuid();var command=new CreateParameterSetCommand{CommandId=Guid.NewGuid(),EntityId=new(id),Name="Daily",PayloadJson=new RegimeDiscoveryParameterModel().CreateDraftPayload(id),Subject=new(ActorType.Command,CreateParameterSetCommand.Actor,CreateParameterSetCommand.Verb,id.ToString("N"))};
  command=MessagePackSerializer.Deserialize<CreateParameterSetCommand>(MessagePackSerializer.Serialize(command));
  var context=Substitute.For<IParameterSetCommandContext>();var logger=Substitute.For<ILogger<ParameterSetCommandActor>>();
  var state=new ParameterSetCommandState();
  (await command.ExecuteAsync(context,state,logger)).Success.Should().BeTrue();
  state.CatalogRevision.Should().Be(1);state.Versions.Should().ContainSingle();
  state.Audit.Should().ContainSingle();
  state.Audit[command.CommandId].ActorIdentity.Should().Be(command.OriginatedBy);
  state.Audit[command.CommandId].BeforeJson.Should().Be("null");
  state.Audit[command.CommandId].AfterJson.Should().Contain(state.Versions[1].Reference.PayloadSha256).And.NotContain("PayloadJson");
  state.Receipts[command.CommandId].Reference.Should().Be(state.Versions[1].Reference);
  (await command.ExecuteAsync(context,state,logger)).Success.Should().BeTrue();state.CatalogRevision.Should().Be(1);state.Audit.Should().ContainSingle();
  var changed=command with {Name="Different"};
  Func<Task> action=async()=>await changed.ExecuteAsync(context,state,logger);
  await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("PARAM.OPERATION_IDENTITY_MISMATCH");
 }
}
