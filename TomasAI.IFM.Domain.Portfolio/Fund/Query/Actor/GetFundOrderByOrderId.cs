using TomasAI.IFM.Domain.Portfolio.Query.Actor;
using TomasAI.IFM.Domain.Portfolio.Query.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Validation;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using GetFundOrderByOrderIdQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetFundOrderByOrderIdQuery;

namespace TomasAI.IFM.Domain.Portfolio.Query;

/// <summary>Handles <see cref="GetFundOrderByOrderIdQuery"/>.</summary>
public static class GetFundOrderByOrderId
{
    /// <summary>Executes the mapped Portfolio query and replies with its typed result.</summary>
    public static ValueTask ExecuteAsync(this GetFundOrderByOrderIdQuery query, IQueryActorContext<PortfolioQueryActor> context, PortfolioQueryParameters parameters, CancellationToken cancellationToken)
        => PortfolioQueryHandlerModel.ReplyAsync(context, query, parameters.Service.GetOrderAsync(query.OrderId, cancellationToken));
}
