using TomasAI.IFM.Domain.Portfolio.Query.Actor;
using TomasAI.IFM.Domain.Portfolio.Query.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Validation;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using GetLegacyFundOrdersQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.PortfolioQuery<TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetLegacyFundOrdersRequest, TomasAI.IFM.Domain.Portfolio.Shared.ViewModels.LegacyFundOrderHistoryReadModel[]>;

namespace TomasAI.IFM.Domain.Portfolio.Query;

/// <summary>Handles <see cref="GetLegacyFundOrdersQuery"/>.</summary>
public static class GetLegacyFundOrders
{
    /// <summary>Executes the mapped Portfolio query and replies with its typed result.</summary>
    public static ValueTask ExecuteAsync(this GetLegacyFundOrdersQuery query, IQueryActorContext<PortfolioQueryActor> context, PortfolioQueryParameters parameters, CancellationToken cancellationToken)
        => PortfolioQueryHandlerModel.ReplyAsync(context, query, parameters.LegacyHistory.GetOrdersAsync(query.Parameters.LegacyFundId, query.Parameters.FromDate, query.Parameters.ToDate, query.Parameters.PageSize, cancellationToken));
}
