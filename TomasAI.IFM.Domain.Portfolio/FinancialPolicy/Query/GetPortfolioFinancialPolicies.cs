using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using TomasAI.IFM.Domain.Portfolio.Query;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using GetPortfolioFinancialPoliciesQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetPortfolioFinancialPoliciesQuery;

namespace TomasAI.IFM.Domain.Portfolio.FinancialPolicy.Query;

/// <summary>Reads the projection for <see cref="GetPortfolioFinancialPoliciesQuery"/>.</summary>
public static class GetPortfolioFinancialPolicies
{
    extension(GetPortfolioFinancialPoliciesQuery query)
    {
        /// <summary>Returns the typed result from the Portfolio read model.</summary>
        public Task<ServiceResult<PortfolioPage<PortfolioFinancialPolicyReadModel>>> ExecuteAsync(PortfolioQueryParameters parameters, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);
            ArgumentNullException.ThrowIfNull(parameters);
            return parameters.Service.GetPoliciesAsync(query.PortfolioId, query.PageSize, cancellationToken);
        }
    }
}
