using TomasAI.IFM.Domain.Portfolio.Financial.Repository;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Query.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Portfolio.GeneralLedger.Query;

public static class GetFundPerformanceMetrics
{

    extension(GetFundPerformanceMetricsQuery query)
    {
        public ValueTask ExecuteAsync<TActor>(IFinancialQueryStore store,
            IQueryActorContext<TActor> context, CancellationToken token) where TActor : IActor =>
            FinancialQueryReply.ExecuteAsync(query, context, GetFundPerformanceMetricsQuery.Actor, GetFundPerformanceMetricsQuery.Verb,
                () => store.ReadAsync(query.Scope, query.Parameters, token), token);
    }
}
