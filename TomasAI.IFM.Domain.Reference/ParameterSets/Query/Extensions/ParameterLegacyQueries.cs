using System.Text.Json;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.Reference.ParameterSets.Model;
using TomasAI.IFM.Domain.Reference.ParameterSets.Query.Actor;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Query.Extensions;
public static class ParameterLegacyQueries
{
 public static async ValueTask ExecuteAsync(this ListLegacyParameterVersionsQuery query,IParameterSetQueryContext context,ILogger<ParameterSetQueryActor> logger,CancellationToken token)
 {
  context.AccessPolicy.Demand(ParameterCapability.Read);
  var rows=await context.ConfigurationDb.ReadLegacyParameterVersionsAsync(offset:query.Offset,token:token);
  await context.ReplyAsync(query.Subject.ThreadId,query.Subject.Verb,new ServiceOk<ParameterLegacyVersion[]>(rows));
 }
 public static async ValueTask ExecuteAsync(this PreviewLegacyParameterMigrationQuery query,IParameterSetQueryContext context,ILogger<ParameterSetQueryActor> logger,CancellationToken token)
 {
  context.AccessPolicy.Demand(ParameterCapability.Read);
  if(query.SetId==Guid.Empty||query.Version<=0)throw new ArgumentException("PARAM.LEGACY_IDENTITY_INVALID");
  var row=(await context.ConfigurationDb.ReadLegacyParameterVersionsAsync(query.SetId,query.Version,token:token)).SingleOrDefault()??throw new InvalidOperationException("PARAM.LEGACY_NOT_FOUND");
  var json=ParameterLegacyMigrationModel.Expand(row);using var doc=JsonDocument.Parse(json);var id=ParameterLegacyMigrationModel.TargetId(row.Reference);
  var command=new CreateParameterSetCommand{CommandId=id,EntityId=new(id),Name=$"Legacy Regime {row.Reference.SetId:N} v{row.Reference.Version}",Description=row.Description.Length<=4000?row.Description:row.Description[..4000],
   SchemaVersion=doc.RootElement.GetProperty("SchemaVersion").GetInt32(),PayloadJson=json,LegacySource=row.Reference};
  await context.ReplyAsync(query.Subject.ThreadId,query.Subject.Verb,new ServiceOk<CreateParameterSetCommand>(command));
 }
}
