using TomasAI.IFM.Domain.Portfolio.Query.Actor;
using TomasAI.IFM.Domain.Portfolio.Query.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Validation;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using GetPortfolioFundStrategySnapshotQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.PortfolioQuery<TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetStrategySnapshotRequest, TomasAI.IFM.Domain.Portfolio.Shared.Contracts.PortfolioFundStrategySnapshot>;

namespace TomasAI.IFM.Domain.Portfolio.Query;

/// <summary>Handles <see cref="GetPortfolioFundStrategySnapshotQuery"/>.</summary>
public static class GetPortfolioFundStrategySnapshot
{
    /// <summary>Executes the mapped Portfolio query and replies with its typed result.</summary>
    public static ValueTask ExecuteAsync(this GetPortfolioFundStrategySnapshotQuery query, IQueryActorContext<PortfolioQueryActor> context, PortfolioQueryParameters parameters, CancellationToken cancellationToken)
        => PortfolioQueryHandlerModel.ReplyAsync(context, query, parameters.Service.GetStrategySnapshotAsync(query.Parameters.PortfolioId, query.Parameters.TradingYear, query.Parameters.DecisionHorizon, query.Parameters.UnderlyingRoot, query.Parameters.AssetType, query.Parameters.AsOfUtc, query.Parameters.WorkflowId, query.Parameters.WorkflowRevision, query.Parameters.CorrelationId, cancellationToken));
}
