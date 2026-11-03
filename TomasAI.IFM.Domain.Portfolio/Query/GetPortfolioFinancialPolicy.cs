using TomasAI.IFM.Domain.Portfolio.Query.Actor;
using TomasAI.IFM.Domain.Portfolio.Shared.Queries;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Portfolio.Query;

/// <summary>Compatibility handler for the published PortfolioQuery route; FinancialPolicy owns new traffic.</summary>
public static class GetPortfolioFinancialPolicy
{

    /// <param name="query">
    /// The published legacy query message.
    /// </param>
    extension(GetPortfolioFinancialPolicyQuery query)
    {
        /// <summary>Forwards the legacy query to the FinancialPolicy-owned implementation.</summary>
        ///
        /// <param name="context">The legacy Portfolio query actor context.</param>
        /// <param name="parameters">The query services shared with the owning child actor.</param>
        /// <param name="cancellationToken">Cancellation for the projection read.</param>
        /// <returns>The asynchronous reply operation.</returns>
        public ValueTask ExecuteAsync(
            IQueryActorContext<PortfolioQueryActor> context,
            PortfolioQueryParameters parameters,
            CancellationToken cancellationToken) =>
            TomasAI.IFM.Domain.Portfolio.FinancialPolicy.Query.GetPortfolioFinancialPolicy.ExecuteAsync(query, context, parameters, cancellationToken);
    }
}
