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
/// <summary>Handles <see cref="GetMarketConditionQuery"/>.</summary>
public static class GetMarketCondition
{
    /// <summary>Executes the mapped Market Condition query and replies with its typed result.</summary>
    public static async ValueTask ExecuteAsync(this GetMarketConditionQuery query, IMarketConditionQueryContext services, IQueryActorContext<MarketConditionQueryActor> context, CancellationToken cancellationToken)
    {
        var value = await services.DbFactory.TradeDb.GetMarketConditionAsync(query.WorkflowId, cancellationToken).ConfigureAwait(false) ?? throw new KeyNotFoundException($"Market Condition result for workflow {query.WorkflowId} was not found.");
        await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, new ServiceResult<MarketConditionReadModel>(value)).ConfigureAwait(false);
    }
}
