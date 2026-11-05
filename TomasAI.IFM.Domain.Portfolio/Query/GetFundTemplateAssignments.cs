using TomasAI.IFM.Domain.Portfolio.Shared.Queries;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Query;

/// <summary>Compatibility extension for the Portfolio query contract.</summary>
public static class GetFundTemplateAssignments
{
    extension(GetFundTemplateAssignmentsQuery query)
    {
        /// <summary>Returns the projection result through the owning query extension.</summary>
        public Task<ServiceResult<FundTradeTemplateAssignmentReadModel[]>> ExecuteAsync(PortfolioQueryParameters parameters, CancellationToken cancellationToken)
            => TomasAI.IFM.Domain.Portfolio.Fund.Query.GetFundTemplateAssignments.ExecuteAsync(query, parameters, cancellationToken);
    }
}
