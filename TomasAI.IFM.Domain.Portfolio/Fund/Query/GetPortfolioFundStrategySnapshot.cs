using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Query;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using GetPortfolioFundStrategySnapshotQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetPortfolioFundStrategySnapshotQuery;

namespace TomasAI.IFM.Domain.Portfolio.Fund.Query;

/// <summary>Reads the projection for <see cref="GetPortfolioFundStrategySnapshotQuery"/>.</summary>
public static class GetPortfolioFundStrategySnapshot
{
    extension(GetPortfolioFundStrategySnapshotQuery query)
    {
        /// <summary>Returns the typed result from the Portfolio read model.</summary>
        public Task<ServiceResult<PortfolioFundStrategySnapshot>> ExecuteAsync(PortfolioQueryParameters parameters, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);
            ArgumentNullException.ThrowIfNull(parameters);
            return parameters.Service.GetStrategySnapshotAsync(query.PortfolioId, query.TradingYear, query.DecisionHorizon, query.UnderlyingRoot, query.AssetType, query.AsOfUtc, query.WorkflowId, query.WorkflowRevision, query.CorrelationId, cancellationToken);
        }
    }
}
