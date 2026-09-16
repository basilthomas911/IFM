using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Query.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Query.Model;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Query;
/// <summary>Handles <see cref="GetTradeSelectionHistoryPageQuery"/>.</summary>
public static class GetTradeSelectionHistoryPage
{
    /// <summary>Authorizes, reads, and returns a Trade Selection history page.</summary>
    public static async ValueTask ExecuteAsync(this GetTradeSelectionHistoryPageQuery query, ITradeSelectionQueryContext services, IQueryActorContext<TradeSelectionQueryActor> context, CancellationToken cancellationToken)
    {
        await TradeSelectionQueryModel.AuthorizeAsync(services, query.Access, query.PortfolioId, query.FundId, cancellationToken);
        var page = await services.DbFactory.TradeDb.GetTradeSelectionHistoryAsync(query.PortfolioId, query.FundId, query.ValueDate, query.PageSize, TradeSelectionPaging.Decode(query.PortfolioId, query.FundId, query.ValueDate, query.PageSize, query.PagingState), cancellationToken);
        await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, new ServiceOk<TradeSelectionHistoryPage>(new(page.Items, TradeSelectionPaging.Encode(query.PortfolioId, query.FundId, query.ValueDate, query.PageSize, page.PagingState))));
    }
}