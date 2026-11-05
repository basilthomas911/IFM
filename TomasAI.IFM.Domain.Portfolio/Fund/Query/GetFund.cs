using TomasAI.IFM.Domain.Portfolio.Query;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using GetFundQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetFundQuery;

namespace TomasAI.IFM.Domain.Portfolio.Fund.Query;

/// <summary>Reads the projection for <see cref="GetFundQuery"/>.</summary>
public static class GetFund
{
    extension(GetFundQuery query)
    {
        /// <summary>Returns the typed result from the Portfolio read model.</summary>
        public Task<ServiceResult<FundMandateReadModel>> ExecuteAsync(PortfolioQueryParameters parameters, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);
            ArgumentNullException.ThrowIfNull(parameters);
            return parameters.Service.GetFundAsync(query.PortfolioId, query.FundId, query.Version, cancellationToken);
        }
    }
}
