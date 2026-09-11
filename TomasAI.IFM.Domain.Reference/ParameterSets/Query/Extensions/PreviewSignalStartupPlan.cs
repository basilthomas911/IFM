using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.Reference.ParameterSets.Model;
using TomasAI.IFM.Domain.Reference.ParameterSets.Query.Actor;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Query.Extensions;
public static class PreviewSignalStartupPlan
{
 public static async ValueTask ExecuteAsync(this PreviewSignalStartupPlanQuery query,IParameterSetQueryContext context,ILogger<ParameterSetQueryActor> logger,CancellationToken token)
 {
  ArgumentNullException.ThrowIfNull(context);ArgumentNullException.ThrowIfNull(logger);
  context.AccessPolicy.Demand(ParameterCapability.Read);
  var snapshot=await ParameterStartupReader.ReadAsync(query.StartupRunId,context.ConfigurationDb,context.Assignments,context.StateRepository,token);
  var plan=SignalStartupPlanModel.Create(snapshot,SignalStartupPlanModel.ExistingIntradayConsumers());
  await context.ReplyAsync(query.Subject.ThreadId,query.Subject.Verb,new ServiceOk<ParameterSignalStartupPlan>(plan));
 }
}
