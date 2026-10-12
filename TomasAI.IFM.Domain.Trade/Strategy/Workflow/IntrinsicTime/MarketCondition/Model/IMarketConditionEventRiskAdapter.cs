using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.MarketCondition;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Model;
public interface IMarketConditionEventRiskAdapter
{
    ValueTask<MarketConditionEventRiskState> ReadOnceAsync(MarketConditionEventRiskConfiguration configuration, DateTime evaluationTimestampUtc, CancellationToken cancellationToken);
}
