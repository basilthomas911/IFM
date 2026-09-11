using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.Reference.ParameterSets.Model;
using TomasAI.IFM.Domain.Reference.ParameterSets.Query.Actor;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Query.Extensions;
public static class CreateParameterDraftPreview
{
 public static async ValueTask ExecuteAsync(this CreateParameterDraftPreviewQuery query,IParameterSetQueryContext context,ILogger<ParameterSetQueryActor> logger,CancellationToken token)
 {
  ArgumentNullException.ThrowIfNull(context);ArgumentNullException.ThrowIfNull(logger);
  context.AccessPolicy.Demand(ParameterCapability.Read);token.ThrowIfCancellationRequested();
  if(query.ComponentCode!=RegimeDiscoveryParameterModel.ComponentCode)throw new ArgumentException("PARAM.COMPONENT_UNSUPPORTED");
  if(query.PayloadJson!="{}")
  {
   using var source=System.Text.Json.JsonDocument.Parse(ParameterCanonicalPayloadModel.Canonicalize(query.PayloadJson));
   var version=source.RootElement.GetProperty("SchemaVersion").GetInt32();
   if(!ParameterSchemaRegistry.Default.CanEditLosslessly(query.ComponentCode,version,query.PayloadJson))
    throw new ArgumentException("PARAM.EDITOR_UNSUPPORTED_FIELDS: The original payload is preserved and cannot be edited by this editor.");
  }
  var parameters = query.PayloadJson == "{}"
   ? RegimeDiscoveryParameterModel.CreateExplicitSeed(query.SetId, (TomasAI.IFM.Domain.MarketData.Analytics.Shared.TimeFrameType)query.TargetHorizon)
   : RegimeDiscoveryParameterModel.UpgradeToExplicitSet(System.Text.Json.JsonSerializer.Deserialize<TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery.RegimeDiscoveryParameterSet>(ParameterCanonicalPayloadModel.Canonicalize(query.PayloadJson))
      ?? throw new ArgumentException("Source parameter payload is required."), query.RebuildIntervals);
  if(parameters.ParameterSetId!=query.SetId)throw new ArgumentException("Source parameter identity does not match the preview request.");
  var result=System.Text.Json.JsonSerializer.Serialize(parameters);
  await context.ReplyAsync(query.Subject.ThreadId,query.Subject.Verb,new ServiceOk<string>(result));
 }
}
