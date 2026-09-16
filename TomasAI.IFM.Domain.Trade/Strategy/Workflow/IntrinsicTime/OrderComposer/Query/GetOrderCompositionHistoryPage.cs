using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Query.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Query.Model;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Query;

/// <summary>Handles <see cref="GetOrderCompositionHistoryPageQuery"/>.</summary>
public static class GetOrderCompositionHistoryPage
{
    /// <summary>Authorizes, reads, and returns a page of composition history.</summary>
    public static async ValueTask ExecuteAsync(this GetOrderCompositionHistoryPageQuery query, IOrderCompositionQueryContext services, IQueryActorContext<OrderCompositionQueryActor> context, CancellationToken cancellationToken)
    {
        await OrderCompositionQueryModel.AuthorizeAsync(services, query.Access, query.PortfolioId, query.FundId, cancellationToken);
        var page = await services.DbFactory.TradeDb.GetOrderCompositionHistoryAsync(query.PortfolioId, query.FundId, query.ValueDate, query.PageSize, OrderCompositionPaging.Decode(query.PortfolioId, query.FundId, query.ValueDate, query.PageSize, query.PagingState), cancellationToken);
        await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, new ServiceOk<OrderCompositionHistoryPage>(new(page.Items, OrderCompositionPaging.Encode(query.PortfolioId, query.FundId, query.ValueDate, query.PageSize, page.PagingState))));
    }
}