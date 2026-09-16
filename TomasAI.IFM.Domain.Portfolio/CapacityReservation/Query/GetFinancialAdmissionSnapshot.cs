using TomasAI.IFM.Application.Storage.PortfolioFinancial;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Query.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Portfolio.CapacityReservation.Query;

/// <summary>Handles the GetFinancialAdmissionSnapshot financial query.</summary>
public static class GetFinancialAdmissionSnapshot
{
    /// <summary>Validates, executes, and replies to the mapped financial query.</summary>
    public static ValueTask ExecuteAsync<TActor>(this FinancialQuery<GetFinancialAdmissionSnapshotRequest, FinancialAdmissionSnapshot> query, IFinancialQueryStore dependency, IQueryActorContext<TActor> context, CancellationToken cancellationToken) where TActor : IActor
        => FinancialQueryReply.ExecuteAsync(query, context, "CapacityReservationQuery", "GetFinancialAdmissionSnapshot", () => dependency.ReadAsync(query.Scope, query.Parameters, cancellationToken), cancellationToken);
}
