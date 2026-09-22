using TomasAI.IFM.Domain.Portfolio.Query.Actor;
using TomasAI.IFM.Domain.Portfolio.Query.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Validation;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using GetPortfolioFinancialPolicyQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetPortfolioFinancialPolicyQuery;

namespace TomasAI.IFM.Domain.Portfolio.Query;

/// <summary>Handles <see cref="GetPortfolioFinancialPolicyQuery"/>.</summary>
public static class GetPortfolioFinancialPolicy
{
    /// <summary>Executes the mapped Portfolio query and replies with its typed result.</summary>
    public static ValueTask ExecuteAsync(this GetPortfolioFinancialPolicyQuery query, IQueryActorContext<PortfolioQueryActor> context, PortfolioQueryParameters parameters, CancellationToken cancellationToken)
        => PortfolioQueryHandlerModel.ReplyAsync(context, query, parameters.Service.GetPolicyAsync(query.PolicyId, query.PolicyVersion, cancellationToken));
}
