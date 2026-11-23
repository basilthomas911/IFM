using TomasAI.IFM.Application.MarketData.OperationsHealth;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.MarketCondition;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Model;
public interface IMarketConditionAssessmentSnapshotProvider
{
    ValueTask<MarketConditionAssessmentSnapshot> CaptureAsync(MarketConditionAssessmentParameterSet parameters, DateTime evaluatedAtUtc, CancellationToken cancellationToken);
    /// <summary>Captures option assessments plus fresh front/second VX observations.</summary>
    ValueTask<MarketConditionAssessmentSnapshot> CaptureOptionObservationAsync(MarketConditionAssessmentParameterSet parameters, DateTime evaluatedAtUtc, CancellationToken cancellationToken)
        => throw new NotSupportedException("Option VX snapshot capture is unavailable.");
    /// <summary>Captures observations without strategy eligibility or global execution vetoes.</summary>
    ValueTask<MarketConditionAssessmentSnapshot> CaptureObservationAsync(MarketConditionAssessmentParameterSet parameters, DateTime evaluatedAtUtc, CancellationToken cancellationToken)
        => throw new NotSupportedException("Observation-only snapshot capture is unavailable.");
    /// <summary>Captures market session state without a strategy entry window.</summary>
    ValueTask<MarketConditionAssessmentSnapshot> CaptureDailyFuturesMarketAsync(MarketConditionAssessmentParameterSet parameters, DateTime evaluatedAtUtc, CancellationToken cancellationToken)
        => throw new NotSupportedException("Daily futures market-only snapshot capture is unavailable.");
    /// <summary>Retains capture semantics for already-issued v1 commands.</summary>
    ValueTask<MarketConditionAssessmentSnapshot> CaptureDailyFuturesLegacyAsync(MarketConditionAssessmentParameterSet parameters, DateTime evaluatedAtUtc, CancellationToken cancellationToken)
        => throw new NotSupportedException("Legacy Daily futures snapshot capture is unavailable.");
    /// <summary>Captures futures-only authorities without VX, history acquisition or indicator calculation.</summary>
    ValueTask<MarketConditionAssessmentSnapshot> CaptureDailyFuturesAsync(MarketConditionAssessmentParameterSet parameters, DateTime evaluatedAtUtc, CancellationToken cancellationToken)
        => throw new NotSupportedException("Daily futures snapshot capture is unavailable.");
}
