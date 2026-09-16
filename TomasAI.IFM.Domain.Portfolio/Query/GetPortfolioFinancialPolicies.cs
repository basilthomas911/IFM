using TomasAI.IFM.Domain.Portfolio.Query.Actor;
using TomasAI.IFM.Domain.Portfolio.Query.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Validation;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using GetPortfolioFinancialPoliciesQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.PortfolioQuery<TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetPoliciesRequest, TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi.PortfolioPage<TomasAI.IFM.Domain.Portfolio.Shared.ViewModels.PortfolioFinancialPolicyReadModel>>;

namespace TomasAI.IFM.Domain.Portfolio.Query;

/// <summary>Handles <see cref="GetPortfolioFinancialPoliciesQuery"/>.</summary>
public static class GetPortfolioFinancialPolicies
{
    /// <summary>Executes the mapped Portfolio query and replies with its typed result.</summary>
    public static ValueTask ExecuteAsync(this GetPortfolioFinancialPoliciesQuery query, IQueryActorContext<PortfolioQueryActor> context, PortfolioQueryParameters parameters, CancellationToken cancellationToken)
        => PortfolioQueryHandlerModel.ReplyAsync(context, query, parameters.Service.GetPoliciesAsync(query.Parameters.PortfolioId, query.Parameters.PageSize, cancellationToken));
}
