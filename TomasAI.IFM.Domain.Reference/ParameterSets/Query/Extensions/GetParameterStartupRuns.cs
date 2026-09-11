using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.Reference.ParameterSets.Query.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Query.Extensions;
public static class GetParameterStartupRuns
{
 public static async ValueTask ExecuteAsync(this GetParameterStartupRunsQuery query,IParameterSetQueryContext context,ILogger<ParameterSetQueryActor> logger,CancellationToken token)
 {
  context.AccessPolicy.Demand(ParameterCapability.Read);token.ThrowIfCancellationRequested();
  var id=ParameterStartupEntityId.Registry;
  var address=new ApplySignalStartupPlanCommand{EntityId=id,Subject=new ActorSubject(ActorType.Command,ApplySignalStartupPlanCommand.Actor,ApplySignalStartupPlanCommand.Verb,id.Format())};
  var state=await context.Startups.LoadStateAsync(address);token.ThrowIfCancellationRequested();
  await context.ReplyAsync(query.Subject.ThreadId,query.Subject.Verb,new ServiceOk<ParameterStartupRun[]>(state.ActiveRuns.Values.OrderBy(x=>x.CreatedAtUtc).ToArray()));
 }
}
