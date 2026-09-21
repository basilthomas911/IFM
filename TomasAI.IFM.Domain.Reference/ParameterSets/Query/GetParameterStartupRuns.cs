using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.Reference.ParameterSets.Query.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Query;
public static class GetParameterStartupRuns
{
 public static async ValueTask ExecuteAsync(this GetParameterStartupRunsQuery query,IParameterSetQueryContext context,ILogger<ParameterSetQueryActor> logger,CancellationToken token)
 {
  context.AccessPolicy.Demand(ParameterCapability.Read);token.ThrowIfCancellationRequested();
  var id=ParameterStartupEntityId.Registry;
  var address=new ApplySignalStartupPlanCommand{EntityId=id,Subject=new ActorSubject(ActorType.Command,ApplySignalStartupPlanCommand.Actor,ApplySignalStartupPlanCommand.Verb,id.Format())};
  var state=await context.Startups.LoadStateAsync(address);token.ThrowIfCancellationRequested();
  var limit=Math.Clamp(query.Limit,1,25);
  var runs=state.ActiveRuns.Values
   .Where(x=>query.AfterCreatedAtUtc is null
    || x.CreatedAtUtc>query.AfterCreatedAtUtc.Value
    || (x.CreatedAtUtc==query.AfterCreatedAtUtc.Value
     && x.RunId.CompareTo(query.AfterRunId??Guid.Empty)>0))
   .OrderBy(x=>x.CreatedAtUtc)
   .ThenBy(x=>x.RunId)
   .Take(limit)
   .ToArray();
  await context.ReplyAsync(query.Subject.ThreadId,query.Subject.Verb,new ServiceOk<ParameterStartupRun[]>(runs));
 }
}
