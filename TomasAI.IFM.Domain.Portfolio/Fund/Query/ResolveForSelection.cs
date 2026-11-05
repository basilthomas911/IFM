using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Query;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using ResolveForSelectionQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.ResolveForSelectionQuery;

namespace TomasAI.IFM.Domain.Portfolio.Fund.Query;

/// <summary>Reads the projection for <see cref="ResolveForSelectionQuery"/>.</summary>
public static class ResolveForSelection
{
    extension(ResolveForSelectionQuery query)
    {
        /// <summary>Returns the typed result from the Portfolio read model.</summary>
        public Task<ServiceResult<PortfolioFundStrategySnapshot>> ExecuteAsync(PortfolioQueryParameters parameters, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);
            ArgumentNullException.ThrowIfNull(parameters);
            return parameters.Service.ResolveForSelectionAsync(query.PortfolioId, query.FundId, query.TradingYear, query.DecisionHorizon, query.UnderlyingRoot, query.AsOfUtc, query.WorkflowId, query.WorkflowRevision, query.CorrelationId, cancellationToken);
        }
    }
}
