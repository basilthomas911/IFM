using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.Reference.ParameterSets.Query.Actor;
using TomasAI.IFM.Domain.Reference.ParameterSets.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Query.Extensions;
public static class GetParameterAssignment
{
 public static async ValueTask ExecuteAsync(this GetParameterAssignmentQuery query,IParameterSetQueryContext context,ILogger<ParameterSetQueryActor> logger,CancellationToken token)
 {
  ArgumentNullException.ThrowIfNull(context);ArgumentNullException.ThrowIfNull(logger);
  context.AccessPolicy.Demand(ParameterCapability.Read);token.ThrowIfCancellationRequested();
  var scope=WorkflowParameterScopeModel.Create(query.WorkflowDefinitionId,(TimeFrameType)query.TargetHorizon);
  var identity=new ParameterAssignmentEntityId(WorkflowParameterScopeModel.AssignmentId(scope));
  var address=new AssignParameterVersionCommand{EntityId=identity,Scope=scope,Subject=new ActorSubject(ActorType.Command,AssignParameterVersionCommand.Actor,AssignParameterVersionCommand.Verb,identity.Format())};
  var state=await context.Assignments.LoadStateAsync(address);token.ThrowIfCancellationRequested();
  await context.ReplyAsync(query.Subject.ThreadId,query.Subject.Verb,new ServiceOk<ParameterAssignmentSnapshot>(new(identity,scope,state.Revision,state.Assignment,state.Audit.Values.OrderBy(x=>x.Revision).ToArray())));
 }
}
