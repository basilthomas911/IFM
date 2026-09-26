using TomasAI.IFM.Domain.Trade.Shared;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;

public interface IMarketConditionAssessmentQueryApi
{
    ValueTask<ServiceResult<MarketConditionAssessmentCompletedEvent>> GetAsync(StrategyWorkflowId workflowId, CancellationToken cancellationToken = default);
    ValueTask<ServiceResult<MarketConditionAssessmentCompletedEvent[]>> HistoryAsync(string profile, string root, TimeFrameType horizon, DateTime beforeUtc, int pageSize = 25, CancellationToken cancellationToken = default);
    ValueTask<ServiceResult<MarketConditionAssessmentCompletedEvent[]>> LatestAsync(string profile, string root, TimeFrameType horizon, CancellationToken cancellationToken = default);
}
