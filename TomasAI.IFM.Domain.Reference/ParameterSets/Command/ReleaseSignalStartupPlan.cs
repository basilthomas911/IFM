using System.Text.Json;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.Reference.ParameterSets.Command.Actor;
using TomasAI.IFM.Domain.Reference.ParameterSets.Command.State;
using TomasAI.IFM.Domain.Reference.ParameterSets.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Command;
public static class ReleaseSignalStartupPlan
{
 public static Task<ServiceResult<GuidResult>> ExecuteAsync(this ReleaseSignalStartupPlanCommand command,IParameterStartupCommandContext context,ParameterStartupCommandState state,ILogger<ParameterStartupCommandActor> logger)
 {
  context.AccessPolicy.Demand(ParameterCapability.Assign);
  if(state.ActiveRuns.ContainsKey(command.RunId))
  {
   var fact=new ParameterStartupChangedEvent{Subject=new ActorSubject(ActorType.Event,ParameterStartupChangedEvent.Actor,ParameterStartupChangedEvent.Verb,command.EntityId.Format()),EntityId=command.EntityId,RunId=command.RunId,Revision=state.Revision+1,Released=true};
   if(!state.Update(fact,command))throw new InvalidOperationException("PARAM.STARTUP_TRANSITION_REJECTED");
  }
  return Task.FromResult<ServiceResult<GuidResult>>(new ServiceOk<GuidResult>(new(command.CommandId)));
 }

}
