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
/// <summary>Handles <see cref="GetLatestMarketConditionQuery"/>.</summary>
public static class GetLatestMarketCondition
{
    /// <summary>Executes the mapped Market Condition query and replies with its typed result.</summary>
    public static async ValueTask ExecuteAsync(this GetLatestMarketConditionQuery query, IMarketConditionQueryContext services, IQueryActorContext<MarketConditionQueryActor> context, CancellationToken cancellationToken)
    {
        var values = await services.DbFactory.TradeDb.GetMarketConditionHistoryAsync(query.FundId, query.InstrumentRoot, query.TargetHorizon, DateTime.MaxValue, 1, cancellationToken).ConfigureAwait(false); var value = values.FirstOrDefault() ?? throw new KeyNotFoundException($"Latest Market Condition result for fund {query.FundId}/{query.InstrumentRoot}/{query.TargetHorizon} was not found.");
        await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, new ServiceResult<MarketConditionReadModel>(value)).ConfigureAwait(false);
    }
}
