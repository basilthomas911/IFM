using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.Reference.ParameterSets.Model;
using TomasAI.IFM.Domain.Reference.ParameterSets.Query.Actor;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Query.Extensions;
public static class ListParameterVersions
{
 public static async ValueTask ExecuteAsync(this ListParameterVersionsQuery query,IParameterSetQueryContext context,ILogger<ParameterSetQueryActor> logger,CancellationToken token)
 {
  ArgumentNullException.ThrowIfNull(context);ArgumentNullException.ThrowIfNull(logger);
  context.AccessPolicy.Demand(ParameterCapability.Read);token.ThrowIfCancellationRequested();
  var components=await context.ConfigurationDb.ReadParameterComponentsAsync(token);
  if(!components.Any(x=>x.ComponentCode==query.ComponentCode))throw new ArgumentException("PARAM.COMPONENT_UNSUPPORTED");
  var result=await context.ConfigurationDb.ReadParameterSetsAsync(query.ComponentCode,query.SetId==Guid.Empty?null:query.SetId,token,query.Limit,query.AfterName,query.AfterSetId,query.AfterVersion);
  await context.ReplyAsync(query.Subject.ThreadId,query.Subject.Verb,new ServiceOk<ParameterSetVersion[]>(result));
 }
}
