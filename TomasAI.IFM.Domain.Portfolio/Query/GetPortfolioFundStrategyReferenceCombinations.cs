using TomasAI.IFM.Domain.Portfolio.Query.Actor;
using TomasAI.IFM.Domain.Portfolio.Query.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Validation;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using GetPortfolioFundStrategyReferenceCombinationsQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.PortfolioQuery<TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetStrategyReferenceCombinationsRequest, TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi.PortfolioFundStrategyReferenceCombination[]>;

namespace TomasAI.IFM.Domain.Portfolio.Query;

/// <summary>Handles <see cref="GetPortfolioFundStrategyReferenceCombinationsQuery"/>.</summary>
public static class GetPortfolioFundStrategyReferenceCombinations
{
    /// <summary>Executes the mapped Portfolio query and replies with its typed result.</summary>
    public static ValueTask ExecuteAsync(this GetPortfolioFundStrategyReferenceCombinationsQuery query, IQueryActorContext<PortfolioQueryActor> context, PortfolioQueryParameters parameters, CancellationToken cancellationToken)
        => PortfolioQueryHandlerModel.ReplyAsync(context, query, parameters.Service.GetStrategyReferenceCombinationsAsync(query.Parameters.PortfolioId, query.Parameters.AsOfUtc, cancellationToken));
}
