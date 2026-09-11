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
public static class RetireParameterVersion
{
 public static async Task<ServiceResult<GuidResult>> ExecuteAsync(this RetireParameterVersionCommand command,IParameterSetCommandContext context,ParameterSetCommandState state,ILogger<ParameterSetCommandActor> logger)
 {
  ArgumentNullException.ThrowIfNull(context);ArgumentNullException.ThrowIfNull(logger);
  context.AccessPolicy.Demand(ParameterCapability.Retire);
  var hash=ParameterMutationModel.RequestHash(command);
  if(state.Operations.TryGetValue(command.CommandId,out var previous))
  {
   if(previous!=hash)throw new InvalidOperationException("PARAM.OPERATION_IDENTITY_MISMATCH");
   return new ServiceOk<GuidResult>(new(command.CommandId));
  }
  if(!state.Versions.TryGetValue(command.Version,out var selected))throw new InvalidOperationException("PARAM.NOT_FOUND");
  var assigned=false;
  // All currently registered assignment scopes are checked under the shared writer lease held by the actor.
  foreach(var horizon in new[]{TomasAI.IFM.Domain.MarketData.Analytics.Shared.TimeFrameType.Daily,TomasAI.IFM.Domain.MarketData.Analytics.Shared.TimeFrameType.Weekly,TomasAI.IFM.Domain.MarketData.Analytics.Shared.TimeFrameType.Monthly})
  {
   var scope=WorkflowParameterScopeModel.Create(TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity.IntrinsicTimeStrategyWorkflowDefinition.Id,horizon);
   var identity=new ParameterAssignmentEntityId(WorkflowParameterScopeModel.AssignmentId(scope));
   var address=new AssignParameterVersionCommand{EntityId=identity,Scope=scope,Subject=new ActorSubject(ActorType.Command,AssignParameterVersionCommand.Actor,AssignParameterVersionCommand.Verb,identity.Format())};
   var usage=await context.Assignments.LoadStateAsync(address);
   assigned|=usage.Assignment is {Enabled:true} current&&current.Reference==selected.Reference;
  }
  var version=ParameterMutationModel.Decide(command,state.CatalogRevision,state.Versions,DateTime.UtcNow,assigned);
  var fact=new ParameterVersionRetiredEvent {Subject=new ActorSubject(ActorType.Event,ParameterVersionRetiredEvent.Actor,ParameterVersionRetiredEvent.Verb,command.EntityId.Format()),EntityId=command.EntityId,Revision=state.CatalogRevision+1,RequestHash=hash,VersionJson=JsonSerializer.Serialize(version),AuditJson=ParameterAuditModel.ForSet(command,state.Versions,version)};
  if(!state.Update(fact,command))throw new InvalidOperationException("PARAM.TRANSITION_REJECTED");
  return new ServiceOk<GuidResult>(new(command.CommandId));
 }
}
