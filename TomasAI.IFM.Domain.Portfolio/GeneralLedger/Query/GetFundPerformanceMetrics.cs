using TomasAI.IFM.Domain.Portfolio.Financial.Repository;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Query.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;

namespace TomasAI.IFM.Domain.Portfolio.GeneralLedger.Query;

public static class GetFundPerformanceMetrics
{

    extension(GetFundPerformanceMetricsQuery query)
    {
        public ValueTask<FinancialRead<FundPerformanceMetrics>> ExecuteAsync(IFinancialQueryStore store, CancellationToken token) =>
            FinancialQueryReply.ReadAsync(query, GetFundPerformanceMetricsQuery.Actor, GetFundPerformanceMetricsQuery.Verb,
                () => store.ReadAsync(query.Scope, query.Parameters, token), token);
    }
}
