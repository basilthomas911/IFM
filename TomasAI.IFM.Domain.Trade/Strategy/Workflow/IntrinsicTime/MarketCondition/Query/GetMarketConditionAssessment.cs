using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Queries;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Reference;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Query.Actor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Query;
/// <summary>Handles <see cref = "GetMarketConditionAssessmentQuery"/>.</summary>
public static class GetMarketConditionAssessment
{

    extension(GetMarketConditionAssessmentQuery query)
    {
        /// <summary>Executes the mapped Market Condition query and returns its typed result.</summary>
        public async ValueTask<ServiceResult<MarketConditionAssessmentCompletedEvent>> ExecuteAsync(IMarketConditionQueryContext services, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);
            ArgumentNullException.ThrowIfNull(services);
            var value = await services.DbFactory.TradeDb.GetMarketConditionAssessmentAsync(query.WorkflowId, cancellationToken).ConfigureAwait(false) ?? throw new KeyNotFoundException("Assessment result was not found.");
            return new ServiceResult<MarketConditionAssessmentCompletedEvent>(value);
        }
    }
}
