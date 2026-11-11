using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Queries;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Reference;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Query.Actor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Query;
/// <summary>Handles <see cref = "GetMarketConditionAssessmentHistoryQuery"/>.</summary>
public static class GetMarketConditionAssessmentHistory
{

    extension(GetMarketConditionAssessmentHistoryQuery query)
    {
        /// <summary>Executes the mapped Market Condition query and returns its typed result.</summary>
        public async ValueTask<ServiceResult<MarketConditionAssessmentCompletedEvent[]>> ExecuteAsync(IMarketConditionQueryContext services, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);
            ArgumentNullException.ThrowIfNull(services);
            MarketConditionHistorySelection.RequireAssessment(query.MarketProfileId, query.InstrumentRoot, query.TargetHorizon, query.BeforeUtc, query.PageSize);
            var values = await services.DbFactory.TradeDb.GetMarketConditionAssessmentHistoryAsync(query.MarketProfileId, query.InstrumentRoot, query.TargetHorizon, query.BeforeUtc, query.PageSize, cancellationToken).ConfigureAwait(false);
            return new ServiceResult<MarketConditionAssessmentCompletedEvent[]>(values.ToArray());
        }
    }
}
