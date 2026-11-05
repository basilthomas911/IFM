using TomasAI.IFM.Domain.Portfolio.Financial.Repository;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Query.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;

namespace TomasAI.IFM.Domain.Portfolio.CapacityReservation.Query;

/// <summary>Handles the GetFinancialAdmissionSnapshot financial query.</summary>
public static class GetFinancialAdmissionSnapshot
{

    extension(GetFinancialAdmissionSnapshotQuery query)
    {
        /// <summary>Returns the bounded typed financial read result.</summary>
        public ValueTask<FinancialRead<FinancialAdmissionSnapshot>> ExecuteAsync(IFinancialQueryStore dependency, CancellationToken cancellationToken)

        {
            ArgumentNullException.ThrowIfNull(query);
            return FinancialQueryReply.ReadAsync(query, "CapacityReservationQuery", "GetFinancialAdmissionSnapshot", () => dependency.ReadAsync(query.Scope, query.Parameters, cancellationToken), cancellationToken);
        }
    }
}
