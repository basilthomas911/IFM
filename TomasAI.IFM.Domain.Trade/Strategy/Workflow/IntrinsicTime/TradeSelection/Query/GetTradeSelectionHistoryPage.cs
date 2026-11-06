using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Query.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Query.Model;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Query;
/// <summary>Handles <see cref = "GetTradeSelectionHistoryPageQuery"/>.</summary>
public static class GetTradeSelectionHistoryPage
{

    extension(GetTradeSelectionHistoryPageQuery query)
    {
        /// <summary>Authorizes, reads, and returns a Trade Selection history page.</summary>
        public async ValueTask<ServiceResult<TradeSelectionHistoryPage>> ExecuteAsync(ITradeSelectionQueryContext services, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);
            ArgumentNullException.ThrowIfNull(services);
            await (TradeSelectionQueryModel.AuthorizeAsync(services, query.Access, query.PortfolioId, query.FundId, cancellationToken)).ConfigureAwait(false);
            var page = await (services.DbFactory.TradeDb.GetTradeSelectionHistoryAsync(query.PortfolioId, query.FundId, query.ValueDate, query.PageSize, TradeSelectionPaging.Decode(query.PortfolioId, query.FundId, query.ValueDate, query.PageSize, query.PagingState), cancellationToken)).ConfigureAwait(false);
            return new ServiceOk<TradeSelectionHistoryPage>(new(page.Items, TradeSelectionPaging.Encode(query.PortfolioId, query.FundId, query.ValueDate, query.PageSize, page.PagingState)));
        }
    }
}
