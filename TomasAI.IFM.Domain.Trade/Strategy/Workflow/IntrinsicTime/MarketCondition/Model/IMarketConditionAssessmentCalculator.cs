using System.Globalization;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Model;
/// <summary>Evaluates one market snapshot for the single triggering timeframe.</summary>
public interface IMarketConditionAssessmentCalculator
{
    /// <summary>Produces a deterministic assessment from frozen parameters, upstream evidence, and the captured snapshot.</summary>
    MarketConditionAssessmentResult Calculate(ExecuteMarketConditionAssessmentCommand command, MarketConditionAssessmentSnapshot snapshot, Guid resultId);
}
