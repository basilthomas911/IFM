using TomasAI.IFM.Domain.Portfolio.Query.Actor;
using TomasAI.IFM.Domain.Portfolio.Query.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Validation;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using GetFundOrderTradesPageQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.PortfolioQuery<TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetOrderTradesRequest, TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi.PortfolioPage<TomasAI.IFM.Domain.Portfolio.Shared.ViewModels.FundOrderTradeProjectionReadModel>>;

namespace TomasAI.IFM.Domain.Portfolio.Query;

/// <summary>Handles <see cref="GetFundOrderTradesPageQuery"/>.</summary>
public static class GetFundOrderTradesPage
{
    /// <summary>Executes the mapped Portfolio query and replies with its typed result.</summary>
    public static ValueTask ExecuteAsync(this GetFundOrderTradesPageQuery query, IQueryActorContext<PortfolioQueryActor> context, PortfolioQueryParameters parameters, CancellationToken cancellationToken)
        => PortfolioQueryHandlerModel.ReplyAsync(context, query, parameters.Service.GetOrderTradesAsync(query.Parameters.OrderId, query.Parameters.PageSize, query.Parameters.PageToken, cancellationToken));
}
