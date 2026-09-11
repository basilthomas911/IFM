using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.Reference.ParameterSets.Query.Actor;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Query.Extensions;
public static class GetParameterSchema
{
 public static async ValueTask ExecuteAsync(this GetParameterSchemaQuery query,IParameterSetQueryContext context,ILogger<ParameterSetQueryActor> logger,CancellationToken token)
 {
  ArgumentNullException.ThrowIfNull(context);ArgumentNullException.ThrowIfNull(logger);
  context.AccessPolicy.Demand(ParameterCapability.Read);
  var result=await context.ConfigurationDb.ReadParameterSchemaAsync(query.ComponentCode,query.SchemaVersion,token)
   ??throw new KeyNotFoundException("PARAM.SCHEMA_UNSUPPORTED");
  await context.ReplyAsync(query.Subject.ThreadId,query.Subject.Verb,new ServiceOk<ParameterSchemaDefinition>(result));
 }
}
