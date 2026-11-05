using TomasAI.IFM.Domain.Portfolio.Financial.Repository;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Query.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;

namespace TomasAI.IFM.Domain.Portfolio.GeneralLedger.Query;

/// <summary>Handles the GetTrialBalance financial query.</summary>
public static class GetTrialBalance
{

    extension(GetTrialBalanceQuery query)
    {
        /// <summary>Returns the bounded typed financial read result.</summary>
        public ValueTask<FinancialRead<FinancialTrialBalance>> ExecuteAsync(IFinancialQueryStore dependency, CancellationToken cancellationToken)

        {
            ArgumentNullException.ThrowIfNull(query);
            return FinancialQueryReply.ReadAsync(query, "GeneralLedgerQuery", "GetTrialBalance", () => dependency.ReadAsync(query.Scope, query.Parameters, cancellationToken), cancellationToken);
        }
    }
}
