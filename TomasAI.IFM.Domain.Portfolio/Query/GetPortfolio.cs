using TomasAI.IFM.Domain.Portfolio.Query.Actor;
using TomasAI.IFM.Domain.Portfolio.Query.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Validation;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using GetPortfolioQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetPortfolioQuery;

namespace TomasAI.IFM.Domain.Portfolio.Query;

/// <summary>Handles <see cref="GetPortfolioQuery"/>.</summary>
public static class GetPortfolio
{
    /// <summary>Executes the mapped Portfolio query and replies with its typed result.</summary>
    /// <typeparam name="TActor">The owning Query actor type.</typeparam>
    /// <param name="query">The concrete query message.</param>
    /// <param name="context">The owning actor reply context.</param>
    /// <param name="parameters">The Portfolio projection query dependencies.</param>
    /// <param name="cancellationToken">Cancellation for the projection read.</param>
    /// <returns>The asynchronous reply operation.</returns>
    public static ValueTask ExecuteAsync<TActor>(this GetPortfolioQuery query, IQueryActorContext<TActor> context, PortfolioQueryParameters parameters, CancellationToken cancellationToken)
        where TActor : IActor
        => PortfolioQueryHandlerModel.ReplyAsync(context, query, parameters.Service.GetPortfolioAsync(query.PortfolioId, query.Version, cancellationToken));
}
