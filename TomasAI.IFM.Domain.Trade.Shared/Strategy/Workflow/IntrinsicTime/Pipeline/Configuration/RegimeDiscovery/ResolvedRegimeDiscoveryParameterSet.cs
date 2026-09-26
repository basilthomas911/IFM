namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;

/// <summary>
/// Represents a resolved and validated Regime Discovery parameter set.
/// </summary>
public sealed record ResolvedRegimeDiscoveryParameterSet(
    RegimeDiscoveryParameterSet ParameterSet,
    string PayloadJson,
    string PayloadSha256,
    DateTime EffectiveFromUtc);
