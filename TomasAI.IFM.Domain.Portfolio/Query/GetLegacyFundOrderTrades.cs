using TomasAI.IFM.Domain.Portfolio.Query.Actor;
using TomasAI.IFM.Domain.Portfolio.Query.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Validation;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using GetLegacyFundOrderTradesQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.PortfolioQuery<TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetLegacyFundOrderTradesRequest, TomasAI.IFM.Domain.Portfolio.Shared.ViewModels.LegacyFundTradeHistoryReadModel[]>;

namespace TomasAI.IFM.Domain.Portfolio.Query;

/// <summary>Handles <see cref="GetLegacyFundOrderTradesQuery"/>.</summary>
public static class GetLegacyFundOrderTrades
{
    /// <summary>Executes the mapped Portfolio query and replies with its typed result.</summary>
    public static ValueTask ExecuteAsync(this GetLegacyFundOrderTradesQuery query, IQueryActorContext<PortfolioQueryActor> context, PortfolioQueryParameters parameters, CancellationToken cancellationToken)
        => PortfolioQueryHandlerModel.ReplyAsync(context, query, parameters.LegacyHistory.GetOrderTradesAsync(query.Parameters.LegacyFundId, query.Parameters.OrderId, cancellationToken));
}
