using TomasAI.IFM.Domain.Portfolio.Financial.Repository;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Query.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Portfolio.GeneralLedger.Query;

/// <summary>Handles the PrepareFinancialAuthority financial query.</summary>
public static class PrepareFinancialAuthority
{

    extension(PrepareFinancialAuthorityQuery query)
    {
        /// <summary>Validates, executes, and replies to the mapped financial query.</summary>
        public ValueTask ExecuteAsync<TActor>(FinancialAuthorityPreparation preparation, IQueryActorContext<TActor> context, CancellationToken cancellationToken) where TActor : IActor

        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(query);
            return FinancialQueryReply.ExecuteAsync(query, context, "GeneralLedgerQuery", "PrepareFinancialAuthority", () => preparation.PrepareAsync(query.Scope, query.Parameters, cancellationToken), cancellationToken);
        }
    }
}
