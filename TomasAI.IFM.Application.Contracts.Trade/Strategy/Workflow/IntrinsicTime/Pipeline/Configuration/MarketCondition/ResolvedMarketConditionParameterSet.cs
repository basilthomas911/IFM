namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.MarketCondition;

/// <summary>
/// Represents a resolved and validated Market Condition parameter set.
/// </summary>
public sealed record ResolvedMarketConditionParameterSet(
    MarketConditionParameterSet ParameterSet,
    string PayloadJson,
    string PayloadSha256,
    DateTime EffectiveFromUtc);
