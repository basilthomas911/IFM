using TomasAI.IFM.Domain.Portfolio.Query;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using GetActivePortfolioFinancialPolicyQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetActivePortfolioFinancialPolicyQuery;

namespace TomasAI.IFM.Domain.Portfolio.FinancialPolicy.Query;

/// <summary>Reads the projection for <see cref="GetActivePortfolioFinancialPolicyQuery"/>.</summary>
public static class GetActivePortfolioFinancialPolicy
{
    extension(GetActivePortfolioFinancialPolicyQuery query)
    {
        /// <summary>Returns the typed result from the Portfolio read model.</summary>
        public Task<ServiceResult<PortfolioFinancialPolicyReadModel>> ExecuteAsync(PortfolioQueryParameters parameters, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);
            ArgumentNullException.ThrowIfNull(parameters);
            return parameters.Service.GetActivePolicyAsync(query.PortfolioId, cancellationToken);
        }
    }
}
