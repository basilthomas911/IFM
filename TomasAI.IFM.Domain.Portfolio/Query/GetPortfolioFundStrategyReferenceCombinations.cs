using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using TomasAI.IFM.Domain.Portfolio.Shared.Queries;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Query;

/// <summary>Compatibility extension for the Portfolio query contract.</summary>
public static class GetPortfolioFundStrategyReferenceCombinations
{
    extension(GetPortfolioFundStrategyReferenceCombinationsQuery query)
    {
        /// <summary>Returns the projection result through the owning query extension.</summary>
        public Task<ServiceResult<PortfolioFundStrategyReferenceCombination[]>> ExecuteAsync(PortfolioQueryParameters parameters, CancellationToken cancellationToken)
            => TomasAI.IFM.Domain.Portfolio.Fund.Query.GetPortfolioFundStrategyReferenceCombinations.ExecuteAsync(query, parameters, cancellationToken);
    }
}
