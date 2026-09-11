using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.Reference.ParameterSets.Model;
using TomasAI.IFM.Domain.Reference.ParameterSets.Query.Actor;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Query.Extensions;
public static class ValidateParameterCandidate
{
 public static async ValueTask ExecuteAsync(this ValidateParameterCandidateQuery query,IParameterSetQueryContext context,ILogger<ParameterSetQueryActor> logger,CancellationToken token)
 {
  ArgumentNullException.ThrowIfNull(context);ArgumentNullException.ThrowIfNull(logger);
  context.AccessPolicy.Demand(ParameterCapability.Read);token.ThrowIfCancellationRequested();
  if(query.ComponentCode!=RegimeDiscoveryParameterModel.ComponentCode)throw new ArgumentException("PARAM.COMPONENT_UNSUPPORTED");
  var issues=new RegimeDiscoveryParameterModel().Validate(query.PayloadJson,query.SchemaVersion);
  var hash=string.Empty;try{hash=ParameterCanonicalPayloadModel.Hash(query.PayloadJson);}catch(Exception error) when(error is ArgumentException or System.Text.Json.JsonException){}
  var result=new ParameterValidationReport(hash,issues);
  await context.ReplyAsync(query.Subject.ThreadId,query.Subject.Verb,new ServiceOk<ParameterValidationReport>(result));
 }
}
