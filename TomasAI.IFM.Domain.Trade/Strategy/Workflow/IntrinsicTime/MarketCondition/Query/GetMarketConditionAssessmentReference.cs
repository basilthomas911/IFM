using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Queries;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Reference;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Query.Actor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Query;
/// <summary>Handles <see cref = "GetMarketConditionAssessmentReferenceQuery"/>.</summary>
public static class GetMarketConditionAssessmentReference
{

    extension(GetMarketConditionAssessmentReferenceQuery query)
    {
        /// <summary>Executes the mapped Market Condition query and returns its typed result.</summary>
        public async ValueTask<ServiceResult<MarketConditionAssessmentReferenceRow[]>> ExecuteAsync(IMarketConditionQueryContext services, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);
            cancellationToken.ThrowIfCancellationRequested();
            var value = new MarketConditionAssessmentReferenceGenerator().Generate();
            return new ServiceResult<MarketConditionAssessmentReferenceRow[]>(value);
        }
    }
}
