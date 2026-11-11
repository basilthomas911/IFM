using TomasAI.IFM.Domain.Portfolio.Query;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using GetFundBaseContractsQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetFundBaseContractsQuery;

namespace TomasAI.IFM.Domain.Portfolio.Fund.Query;

/// <summary>Reads the projection for <see cref="GetFundBaseContractsQuery"/>.</summary>
public static class GetFundBaseContracts
{
    extension(GetFundBaseContractsQuery query)
    {
        /// <summary>Returns the typed result from the Portfolio read model.</summary>
        public Task<ServiceResult<FundBaseContractsReadModel>> ExecuteAsync(PortfolioQueryParameters parameters, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);
            ArgumentNullException.ThrowIfNull(parameters);
            return parameters.BaseContracts.GetAsync(query.PortfolioId, query.FundId, query.Version, cancellationToken);
        }
    }
}
