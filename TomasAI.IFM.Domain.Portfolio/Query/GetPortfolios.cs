using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using TomasAI.IFM.Domain.Portfolio.Query;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using GetPortfoliosQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetPortfoliosQuery;

namespace TomasAI.IFM.Domain.Portfolio.Query;

/// <summary>Reads the projection for <see cref="GetPortfoliosQuery"/>.</summary>
public static class GetPortfolios
{
    extension(GetPortfoliosQuery query)
    {
        /// <summary>Returns the typed result from the Portfolio read model.</summary>
        public Task<ServiceResult<PortfolioPage<PortfolioReadModel>>> ExecuteAsync(PortfolioQueryParameters parameters, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);
            ArgumentNullException.ThrowIfNull(parameters);
            return parameters.Service.GetPortfoliosAsync(query.State is null ? null : (PortfolioOperatingState)query.State, query.PageSize, query.PageToken, cancellationToken);
        }
    }
}
