using TomasAI.IFM.Domain.Portfolio.Query.Actor;
using TomasAI.IFM.Domain.Portfolio.Query.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Validation;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using GetFundAllocationQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetFundAllocationQuery;

namespace TomasAI.IFM.Domain.Portfolio.Query;

/// <summary>Handles <see cref="GetFundAllocationQuery"/>.</summary>
public static class GetFundAllocation
{
    /// <summary>Executes the mapped Portfolio query and replies with its typed result.</summary>
    public static ValueTask ExecuteAsync(this GetFundAllocationQuery query, IQueryActorContext<PortfolioQueryActor> context, PortfolioQueryParameters parameters, CancellationToken cancellationToken)
        => PortfolioQueryHandlerModel.ReplyAsync(context, query, parameters.Service.GetFundAllocationAsync(query.PortfolioId, query.FundId, cancellationToken));
}
