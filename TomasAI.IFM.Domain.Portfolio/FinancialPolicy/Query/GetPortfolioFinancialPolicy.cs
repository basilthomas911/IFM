using TomasAI.IFM.Domain.Portfolio.Query;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using GetPortfolioFinancialPolicyQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetPortfolioFinancialPolicyQuery;

namespace TomasAI.IFM.Domain.Portfolio.FinancialPolicy.Query;

/// <summary>Reads the projection for <see cref="GetPortfolioFinancialPolicyQuery"/>.</summary>
public static class GetPortfolioFinancialPolicy
{
    extension(GetPortfolioFinancialPolicyQuery query)
    {
        /// <summary>Returns the typed result from the Portfolio read model.</summary>
        public Task<ServiceResult<PortfolioFinancialPolicyReadModel>> ExecuteAsync(PortfolioQueryParameters parameters, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);
            ArgumentNullException.ThrowIfNull(parameters);
            return parameters.Service.GetPolicyAsync(query.PolicyId, query.PolicyVersion, cancellationToken);
        }
    }
}
