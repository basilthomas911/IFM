using TomasAI.IFM.Domain.MarketData.Query.Actor;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Query;

public static class GetEvaluatedOptionChain
{
 public static async ValueTask ExecuteAsync(this GetEvaluatedOptionChainQuery q,IMarketDataQueryContext c,CancellationToken ct)
 {
  var result=await LoadAsync(q,c,ct).ConfigureAwait(false);
  await c.ReplyAsync(q.Subject.ThreadId,q.Subject.Verb,result).ConfigureAwait(false);
 }
 static async Task<ServiceResult<EvaluatedOptionChainReadModel>> LoadAsync(GetEvaluatedOptionChainQuery q,IMarketDataQueryContext c,CancellationToken ct)
 {
  try{return await LoadCoreAsync(q,c,ct).ConfigureAwait(false);}
  catch(Exception e) when(e is not OperationCanceledException){return new ServiceFailed<EvaluatedOptionChainReadModel>(503,e.Message);}
 }
 static Task<ServiceResult<EvaluatedOptionChainReadModel>> LoadCoreAsync(GetEvaluatedOptionChainQuery q,IMarketDataQueryContext c,CancellationToken ct)
  => QualifiedEvaluatedOptionChain.ExecuteAsync(q,c,ct);
}
