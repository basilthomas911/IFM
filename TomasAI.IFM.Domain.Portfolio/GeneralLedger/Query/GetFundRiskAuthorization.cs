using TomasAI.IFM.Application.Storage.PortfolioFinancial;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Query.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Portfolio.GeneralLedger.Query;

/// <summary>Handles the GetFundRiskAuthorization financial query.</summary>
public static class GetFundRiskAuthorization
{
    /// <summary>Validates, executes, and replies to the mapped financial query.</summary>
    public static ValueTask ExecuteAsync<TActor>(this FinancialQuery<GetFundRiskAuthorizationRequest, FundRiskAuthorizationEvidence> query, IFinancialQueryStore dependency, IQueryActorContext<TActor> context, CancellationToken cancellationToken) where TActor : IActor
        => FinancialQueryReply.ExecuteAsync(query, context, "GeneralLedgerQuery", "GetFundRiskAuthorization", () => dependency.ReadAsync(query.Scope, query.Parameters, cancellationToken), cancellationToken);
}
