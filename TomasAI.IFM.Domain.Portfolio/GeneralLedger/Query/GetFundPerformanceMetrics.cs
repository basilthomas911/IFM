using TomasAI.IFM.Domain.Portfolio.Financial.Repository;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Query.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Portfolio.GeneralLedger.Query;

public static class GetFundPerformanceMetrics
{
    public static ValueTask ExecuteAsync<TActor>(this GetFundPerformanceMetricsQuery query, IFinancialQueryStore store,
        IQueryActorContext<TActor> context, CancellationToken token) where TActor : IActor =>
        FinancialQueryReply.ExecuteAsync(query, context, GetFundPerformanceMetricsQuery.Actor, GetFundPerformanceMetricsQuery.Verb,
            () => store.ReadAsync(query.Scope, query.Parameters, token), token);
}
