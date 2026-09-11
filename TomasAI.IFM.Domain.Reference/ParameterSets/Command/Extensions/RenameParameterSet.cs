using System.Text.Json;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.Reference.ParameterSets.Model;
using TomasAI.IFM.Domain.Reference.ParameterSets.Command.State;
using TomasAI.IFM.Domain.Reference.ParameterSets.Command.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Command.Extensions;
public static class RenameParameterSet
{
 public static Task<ServiceResult<GuidResult>> ExecuteAsync(this RenameParameterSetCommand command,IParameterSetCommandContext context,ParameterSetCommandState state,ILogger<ParameterSetCommandActor> logger)
 {
  ArgumentNullException.ThrowIfNull(context);ArgumentNullException.ThrowIfNull(logger);
  context.AccessPolicy.Demand(ParameterCapability.Author);
  var hash=ParameterMutationModel.RequestHash(command);
  if(state.Operations.TryGetValue(command.CommandId,out var previous))
  {
   if(previous!=hash)throw new InvalidOperationException("PARAM.OPERATION_IDENTITY_MISMATCH");
   return Task.FromResult<ServiceResult<GuidResult>>(new ServiceOk<GuidResult>(new(command.CommandId)));
  }
  var version=ParameterMutationModel.Decide(command,state.CatalogRevision,state.Versions,DateTime.UtcNow);
  var fact=new ParameterSetRenamedEvent {Subject=new ActorSubject(ActorType.Event,ParameterSetRenamedEvent.Actor,ParameterSetRenamedEvent.Verb,command.EntityId.Format()),EntityId=command.EntityId,Revision=state.CatalogRevision+1,RequestHash=hash,VersionJson=JsonSerializer.Serialize(version),AuditJson=ParameterAuditModel.ForSet(command,state.Versions,version)};
  if(!state.Update(fact,command))throw new InvalidOperationException("PARAM.TRANSITION_REJECTED");
  return Task.FromResult<ServiceResult<GuidResult>>(new ServiceOk<GuidResult>(new(command.CommandId)));
 }
}
