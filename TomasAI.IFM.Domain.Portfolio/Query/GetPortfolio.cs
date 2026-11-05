using TomasAI.IFM.Domain.Portfolio.Query;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using GetPortfolioQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetPortfolioQuery;

namespace TomasAI.IFM.Domain.Portfolio.Query;

/// <summary>Reads the projection for <see cref="GetPortfolioQuery"/>.</summary>
public static class GetPortfolio
{
    extension(GetPortfolioQuery query)
    {
        /// <summary>Returns the typed result from the Portfolio read model.</summary>
        public Task<ServiceResult<PortfolioReadModel>> ExecuteAsync(PortfolioQueryParameters parameters, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);
            ArgumentNullException.ThrowIfNull(parameters);
            return parameters.Service.GetPortfolioAsync(query.PortfolioId, query.Version, cancellationToken);
        }
    }
}
