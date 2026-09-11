using System.Text.Json;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.Reference.ParameterSets.Command.Actor;
using TomasAI.IFM.Domain.Reference.ParameterSets.Command.State;
using TomasAI.IFM.Domain.Reference.ParameterSets.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Command.Extensions;
public static class ApplySignalStartupPlan
{
 public static async Task<ServiceResult<GuidResult>> ExecuteAsync(this ApplySignalStartupPlanCommand command,IParameterStartupCommandContext context,ParameterStartupCommandState state,ILogger<ParameterStartupCommandActor> logger)
 {
  context.AccessPolicy.Demand(ParameterCapability.Assign);
  if(state.ActiveRuns.ContainsKey(command.RunId))return new ServiceOk<GuidResult>(new(command.CommandId));
  var snapshot=await ParameterStartupReader.ReadUnderLeaseAsync(command.RunId,context.Assignments,context.ParameterSets,CancellationToken.None);
  var plan=SignalStartupPlanModel.Create(snapshot,SignalStartupPlanModel.ExistingIntradayConsumers());
  if(command.ExpectedFingerprint.Length!=0&&command.ExpectedFingerprint!=plan.Fingerprint)throw new InvalidOperationException("PARAM.STARTUP_PLAN_CHANGED");
  var run=new ParameterStartupRun(command.RunId,snapshot.Scopes.ToArray(),snapshot.Assignments.Select(x=>x.Version).DistinctBy(x=>x.Reference).ToArray(),plan,DateTime.UtcNow,command.OriginatedBy);
  var fact=new ParameterStartupChangedEvent{Subject=new ActorSubject(ActorType.Event,ParameterStartupChangedEvent.Actor,ParameterStartupChangedEvent.Verb,command.EntityId.Format()),EntityId=command.EntityId,RunId=command.RunId,Revision=state.Revision+1,RunJson=JsonSerializer.Serialize(run)};
  if(!state.Update(fact,command))throw new InvalidOperationException("PARAM.STARTUP_TRANSITION_REJECTED");
  return new ServiceOk<GuidResult>(new(command.CommandId));
 }
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
