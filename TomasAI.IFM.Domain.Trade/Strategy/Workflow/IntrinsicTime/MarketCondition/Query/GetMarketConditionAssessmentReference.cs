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
/// <summary>Handles <see cref="GetMarketConditionAssessmentReferenceQuery"/>.</summary>
public static class GetMarketConditionAssessmentReference
{
    /// <summary>Executes the mapped Market Condition query and replies with its typed result.</summary>
    public static async ValueTask ExecuteAsync(this GetMarketConditionAssessmentReferenceQuery query, IMarketConditionQueryContext services, IQueryActorContext<MarketConditionQueryActor> context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested(); var value = new MarketConditionAssessmentReferenceGenerator().Generate();
        await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, new ServiceResult<MarketConditionAssessmentReferenceRow[]>(value)).ConfigureAwait(false);
    }
}
