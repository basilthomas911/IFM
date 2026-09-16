using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Queries;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Reference;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Query.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Query;
/// <summary>Handles <see cref="GetMarketConditionHistoryQuery"/>.</summary>
public static class GetMarketConditionHistory
{
    /// <summary>Executes the mapped Market Condition query and replies with its typed result.</summary>
    public static async ValueTask ExecuteAsync(this GetMarketConditionHistoryQuery query, IMarketConditionQueryContext services, IQueryActorContext<MarketConditionQueryActor> context, CancellationToken cancellationToken)
    {
        var values = await services.DbFactory.TradeDb.GetMarketConditionHistoryAsync(query.FundId, query.InstrumentRoot, query.TargetHorizon, query.BeforeUtc, query.PageSize, cancellationToken).ConfigureAwait(false);
        await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, new ServiceResult<ICollection<MarketConditionReadModel>>(values)).ConfigureAwait(false);
    }
}
