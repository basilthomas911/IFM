using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using TomasAI.IFM.Domain.Portfolio.Query;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using GetFundsQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetFundsQuery;

namespace TomasAI.IFM.Domain.Portfolio.Fund.Query;

/// <summary>Reads the projection for <see cref="GetFundsQuery"/>.</summary>
public static class GetFunds
{
    extension(GetFundsQuery query)
    {
        /// <summary>Returns the typed result from the Portfolio read model.</summary>
        public Task<ServiceResult<PortfolioPage<FundMandateReadModel>>> ExecuteAsync(PortfolioQueryParameters parameters, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);
            ArgumentNullException.ThrowIfNull(parameters);
            return parameters.Service.GetFundsAsync(query.PortfolioId, query.State is null ? null : (FundOperatingState)query.State, query.PageSize, query.PageToken, cancellationToken);
        }
    }
}
