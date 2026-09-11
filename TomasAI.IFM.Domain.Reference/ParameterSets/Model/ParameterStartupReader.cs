using TomasAI.IFM.Application.Storage.ConfigurationDb;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.Reference.ParameterSets.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Model;

/// <summary>Reads one coherent generation from authoritative streams under the existing writer lease.</summary>
public static class ParameterStartupReader
{
 public static async Task<ParameterStartupSnapshotModel> ReadAsync(Guid runId,IConfigurationDbContext db,
  IEventSourceActorStateRepository<ParameterAssignmentCommandState> assignmentRepository,
  IEventSourceActorStateRepository<ParameterSetCommandState> setRepository,CancellationToken token)
 {
  await using var lease=await db.AcquireParameterWriteLeaseAsync(token);
  return await ReadUnderLeaseAsync(runId,assignmentRepository,setRepository,token);
 }
 public static async Task<ParameterStartupSnapshotModel> ReadUnderLeaseAsync(Guid runId,
  IEventSourceActorStateRepository<ParameterAssignmentCommandState> assignmentRepository,
  IEventSourceActorStateRepository<ParameterSetCommandState> setRepository,CancellationToken token)
 {
  var assignments=new List<ParameterAssignmentRevision>();var versions=new Dictionary<ParameterVersionRef,ParameterSetVersion>();
  foreach(var horizon in new[]{TimeFrameType.Daily,TimeFrameType.Weekly,TimeFrameType.Monthly})
  {
   token.ThrowIfCancellationRequested();
   var scope=WorkflowParameterScopeModel.Create(IntrinsicTimeStrategyWorkflowDefinition.Id,horizon);
   var id=new ParameterAssignmentEntityId(WorkflowParameterScopeModel.AssignmentId(scope));
   var address=new AssignParameterVersionCommand{EntityId=id,Scope=scope,Subject=new ActorSubject(ActorType.Command,AssignParameterVersionCommand.Actor,AssignParameterVersionCommand.Verb,id.Format())};
   var state=await assignmentRepository.LoadStateAsync(address);
   if(state.Assignment is not {} assignment)continue;
   assignments.Add(assignment);
   if(!assignment.Enabled||versions.ContainsKey(assignment.Reference))continue;
   var setId=new ParameterSetEntityId(assignment.Reference.SetId);
   var setAddress=new CreateParameterSetCommand{EntityId=setId,Subject=new ActorSubject(ActorType.Command,CreateParameterSetCommand.Actor,CreateParameterSetCommand.Verb,setId.Format())};
   var set=await setRepository.LoadStateAsync(setAddress);
   if(!set.Versions.TryGetValue(assignment.Reference.Version,out var version))throw new InvalidOperationException("PARAM.EXACT_VERSION_MISSING");
   versions.Add(assignment.Reference,version);
  }
  token.ThrowIfCancellationRequested();return new(runId,assignments,versions);
 }
}
