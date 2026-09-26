using TomasAI.IFM.Domain.Portfolio.Query.Actor;
using TomasAI.IFM.Domain.Portfolio.Query.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Validation;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using GetPortfolioFundStrategySnapshotQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetPortfolioFundStrategySnapshotQuery;

using TomasAI.IFM.Domain.Portfolio.Query;

namespace TomasAI.IFM.Domain.Portfolio.Fund.Query;

/// <summary>Handles <see cref="GetPortfolioFundStrategySnapshotQuery"/>.</summary>
public static class GetPortfolioFundStrategySnapshot
{
    /// <summary>Executes the mapped Portfolio query and replies with its typed result.</summary>
    /// <typeparam name="TActor">The owning Query actor type.</typeparam>
    /// <param name="query">The concrete query message.</param>
    /// <param name="context">The owning actor reply context.</param>
    /// <param name="parameters">The Portfolio projection query dependencies.</param>
    /// <param name="cancellationToken">Cancellation for the projection read.</param>
    /// <returns>The asynchronous reply operation.</returns>
    public static ValueTask ExecuteAsync<TActor>(this GetPortfolioFundStrategySnapshotQuery query, IQueryActorContext<TActor> context, PortfolioQueryParameters parameters, CancellationToken cancellationToken)
        where TActor : IActor
        => PortfolioQueryHandlerModel.ReplyAsync(context, query, parameters.Service.GetStrategySnapshotAsync(query.PortfolioId, query.TradingYear, query.DecisionHorizon, query.UnderlyingRoot, query.AssetType, query.AsOfUtc, query.WorkflowId, query.WorkflowRevision, query.CorrelationId, cancellationToken));
}
