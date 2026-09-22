using TomasAI.IFM.Domain.Portfolio.Query.Actor;
using TomasAI.IFM.Domain.Portfolio.Query.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Validation;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using GetPortfolioRevisionQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetPortfolioRevisionQuery;

namespace TomasAI.IFM.Domain.Portfolio.Query;

/// <summary>Handles <see cref="GetPortfolioRevisionQuery"/>.</summary>
public static class GetPortfolioRevision
{
    /// <summary>Executes the mapped Portfolio query and replies with its typed result.</summary>
    public static ValueTask ExecuteAsync(this GetPortfolioRevisionQuery query, IQueryActorContext<PortfolioQueryActor> context, PortfolioQueryParameters parameters, CancellationToken cancellationToken)
        => PortfolioQueryHandlerModel.ReplyAsync(context, query, parameters.Service.GetPortfolioRevisionAsync(query.PortfolioId, cancellationToken));
}
