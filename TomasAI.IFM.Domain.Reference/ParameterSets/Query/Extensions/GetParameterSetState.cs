using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.Reference.ParameterSets.Query.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Query.Extensions;

public static class GetParameterSetState
{
 public static async ValueTask ExecuteAsync(this GetParameterSetStateQuery query,IParameterSetQueryContext context,ILogger<ParameterSetQueryActor> logger,CancellationToken token)
 {
  ArgumentNullException.ThrowIfNull(context);ArgumentNullException.ThrowIfNull(logger);
  context.AccessPolicy.Demand(ParameterCapability.Read);token.ThrowIfCancellationRequested();
  if(query.SetId==Guid.Empty)throw new ArgumentException("PARAM.IDENTITY_INVALID");
  var identity=new ParameterSetEntityId(query.SetId);
  // This command is only a stream-address envelope for the existing state repository; it is never dispatched.
  var address=new CreateParameterSetCommand{EntityId=identity,Subject=new ActorSubject(ActorType.Command,CreateParameterSetCommand.Actor,CreateParameterSetCommand.Verb,identity.Format())};
  var state=await context.StateRepository.LoadStateAsync(address);
  token.ThrowIfCancellationRequested();
  var result=new ParameterSetSnapshot(query.SetId,state.CatalogRevision,state.Versions.Values.OrderByDescending(x=>x.Reference.Version).ToArray(),state.Receipts.Values.OrderBy(x=>x.Revision).ToArray(),state.Audit.Values.OrderBy(x=>x.Revision).ToArray());
  await context.ReplyAsync(query.Subject.ThreadId,query.Subject.Verb,new ServiceOk<ParameterSetSnapshot>(result));
 }
}
