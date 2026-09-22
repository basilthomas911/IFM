using TomasAI.IFM.Application.Storage.PortfolioFinancial;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Query.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Portfolio.GeneralLedger.Query;

/// <summary>Handles the PrepareFinancialBook financial query.</summary>
public static class PrepareFinancialBook
{
    /// <summary>Validates, executes, and replies to the mapped financial query.</summary>
    public static ValueTask ExecuteAsync<TActor>(this PrepareFinancialBookQuery query, FinancialBookPreparation preparation, IQueryActorContext<TActor> context, CancellationToken cancellationToken) where TActor : IActor
        => FinancialQueryReply.ExecuteAsync(query, context, "GeneralLedgerQuery", "PrepareFinancialBook", () => preparation.PrepareAsync(query.Scope, query.Parameters, cancellationToken), cancellationToken);
}
