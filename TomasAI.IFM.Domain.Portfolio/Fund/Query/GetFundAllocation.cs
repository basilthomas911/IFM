using TomasAI.IFM.Domain.Portfolio.Query.Actor;
using TomasAI.IFM.Domain.Portfolio.Query.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Validation;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using GetFundAllocationQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetFundAllocationQuery;

using TomasAI.IFM.Domain.Portfolio.Query;

namespace TomasAI.IFM.Domain.Portfolio.Fund.Query;

/// <summary>Handles <see cref="GetFundAllocationQuery"/>.</summary>
public static class GetFundAllocation
{

    /// <param name="query">
    /// The concrete query message.
    /// </param>
    extension(GetFundAllocationQuery query)
    {
        /// <summary>Executes the mapped Portfolio query and replies with its typed result.</summary>
        /// <typeparam name="TActor">The owning Query actor type.</typeparam>
        ///
        /// <param name="context">The owning actor reply context.</param>
        /// <param name="parameters">The Portfolio projection query dependencies.</param>
        /// <param name="cancellationToken">Cancellation for the projection read.</param>
        /// <returns>The asynchronous reply operation.</returns>
        public ValueTask ExecuteAsync<TActor>(IQueryActorContext<TActor> context, PortfolioQueryParameters parameters, CancellationToken cancellationToken)
            where TActor : IActor

        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(parameters);
            ArgumentNullException.ThrowIfNull(query);
            return PortfolioQueryHandlerModel.ReplyAsync(context, query, parameters.Service.GetFundAllocationAsync(query.PortfolioId, query.FundId, cancellationToken));
        }
    }
}
