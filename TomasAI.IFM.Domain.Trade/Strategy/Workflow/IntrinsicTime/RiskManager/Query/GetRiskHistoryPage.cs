using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Query.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Query.Model;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Query;
/// <summary>Handles <see cref="GetRiskHistoryPageQuery"/>.</summary>
public static class GetRiskHistoryPage
{
    /// <summary>Authorizes, reads, and returns a page of risk history.</summary>
    public static async ValueTask ExecuteAsync(this GetRiskHistoryPageQuery query, IRiskQueryContext services, IQueryActorContext<RiskQueryActor> context, CancellationToken cancellationToken)
    {
        await RiskQueryModel.AuthorizeAsync(services, query.Access, query.PortfolioId, query.FundId, cancellationToken);
        var page = await services.DbFactory.TradeDb.GetRiskHistoryAsync(query.PortfolioId, query.FundId, query.ValueDate, query.PageSize, RiskPaging.Decode(query.PortfolioId, query.FundId, query.ValueDate, query.PageSize, query.PagingState), cancellationToken);
        await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, new ServiceOk<RiskHistoryPage>(new(page.Items, RiskPaging.Encode(query.PortfolioId, query.FundId, query.ValueDate, query.PageSize, page.PagingState))));
    }
}