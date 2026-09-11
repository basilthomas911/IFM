using System.Text.Json;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.Reference.ParameterSets.Model;
using TomasAI.IFM.Domain.Reference.ParameterSets.Command.Actor;
using TomasAI.IFM.Domain.Reference.ParameterSets.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Command.Extensions;
public static class DisableParameterAssignment
{
 public static async Task<ServiceResult<GuidResult>> ExecuteAsync(this DisableParameterAssignmentCommand command,IParameterAssignmentCommandContext context,ParameterAssignmentCommandState state,ILogger<ParameterAssignmentCommandActor> logger)
 {
  ArgumentNullException.ThrowIfNull(context);ArgumentNullException.ThrowIfNull(logger);
  context.AccessPolicy.Demand(ParameterCapability.Assign);
  if(command.Scope is null||command.Reference is null||command.EntityId.AssignmentId!=WorkflowParameterScopeModel.AssignmentId(command.Scope))throw new ArgumentException("PARAM.IDENTITY_INVALID");
  var hash=ParameterCanonicalPayloadModel.Hash(JsonSerializer.Serialize(new{command.CommandName,command.EntityId,command.ExpectedRevision,command.Scope,command.Reference}));
  if(state.Operations.TryGetValue(command.CommandId,out var prior))
  {if(prior!=hash)throw new InvalidOperationException("PARAM.OPERATION_IDENTITY_MISMATCH");return new ServiceOk<GuidResult>(new(command.CommandId));}
  await Task.CompletedTask;
  var current=state.Assignment??throw new InvalidOperationException("PARAM.ASSIGNMENT_NOT_FOUND");
  if(current.Scope!=command.Scope||current.Reference!=command.Reference)throw new InvalidOperationException("PARAM.ASSIGNMENT_CONFLICT");
  var value=ParameterAssignmentModel.Disable(current,command.ExpectedRevision,DateTime.UtcNow,command.OriginatedBy);
  var fact=new ParameterAssignmentChangedEvent{Subject=new ActorSubject(ActorType.Event,ParameterAssignmentChangedEvent.Actor,ParameterAssignmentChangedEvent.Verb,command.EntityId.Format()),EntityId=command.EntityId,Revision=value.Revision,RequestHash=hash,AssignmentJson=JsonSerializer.Serialize(value),AuditJson=ParameterAuditModel.ForAssignment(command.CommandId,command.CommandName,command.OriginatedBy,state.Assignment,value)};
  if(!state.Update(fact,command))throw new InvalidOperationException("PARAM.TRANSITION_REJECTED");
  return new ServiceOk<GuidResult>(new(command.CommandId));
 }
}
