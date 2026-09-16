using TomasAI.IFM.Domain.Portfolio.Query.Actor;
using TomasAI.IFM.Domain.Portfolio.Query.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Validation;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using GetActivePortfolioFinancialPolicyQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.PortfolioQuery<TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetActivePolicyRequest, TomasAI.IFM.Domain.Portfolio.Shared.ViewModels.PortfolioFinancialPolicyReadModel>;

namespace TomasAI.IFM.Domain.Portfolio.Query;

/// <summary>Handles <see cref="GetActivePortfolioFinancialPolicyQuery"/>.</summary>
public static class GetActivePortfolioFinancialPolicy
{
    /// <summary>Executes the mapped Portfolio query and replies with its typed result.</summary>
    public static ValueTask ExecuteAsync(this GetActivePortfolioFinancialPolicyQuery query, IQueryActorContext<PortfolioQueryActor> context, PortfolioQueryParameters parameters, CancellationToken cancellationToken)
        => PortfolioQueryHandlerModel.ReplyAsync(context, query, parameters.Service.GetActivePolicyAsync(query.Parameters.PortfolioId, cancellationToken));
}
