using TomasAI.IFM.Domain.Trade.Persistence;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Query.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Query.Model;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Query;
/// <summary>Handles <see cref = "GetRiskHistoryPageQuery"/>.</summary>
public static class GetRiskHistoryPage
{

    extension(GetRiskHistoryPageQuery query)
    {
        /// <summary>Authorizes, reads, and returns a page of risk history.</summary>
        public async ValueTask<ServiceResult<RiskHistoryPage>> ExecuteAsync(IRiskQueryContext services, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);
            ArgumentNullException.ThrowIfNull(services);
            await (RiskQueryModel.AuthorizeAsync(services, query.Access, query.PortfolioId, query.FundId, cancellationToken)).ConfigureAwait(false);
            TradeHistorySelection.RequireValueDate(query.PortfolioId, query.FundId, query.ValueDate, query.PageSize, 100, "Invalid Risk history scope.");
            var page = await (services.DbFactory.TradeDb.GetRiskHistoryAsync(query.PortfolioId, query.FundId, query.ValueDate, query.PageSize, RiskPaging.Decode(query.PortfolioId, query.FundId, query.ValueDate, query.PageSize, query.PagingState), cancellationToken)).ConfigureAwait(false);
            return new ServiceOk<RiskHistoryPage>(new(page.Items, RiskPaging.Encode(query.PortfolioId, query.FundId, query.ValueDate, query.PageSize, page.PagingState)));
        }
    }
}
