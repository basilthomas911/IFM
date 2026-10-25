using TomasAI.IFM.Domain.Strategy.Contracts.Shared.Configuration;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;

/// <summary>
/// Represents a resolved Market Condition Assessment parameter set and its lifecycle state.
/// </summary>
public sealed record ResolvedMarketConditionAssessmentParameterSet(
    MarketConditionAssessmentParameterSet ParameterSet,
    string PayloadSha256,
    DateTime? EffectiveFromUtc,
    ConfigurationParameterSetStatus Status);
