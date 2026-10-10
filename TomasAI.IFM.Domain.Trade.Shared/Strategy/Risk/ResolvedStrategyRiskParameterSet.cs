using TomasAI.IFM.Domain.Strategy.Contracts.Shared.Configuration;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Risk;

/// <summary>One exact persisted policy version and its publication metadata.</summary>
public sealed record ResolvedStrategyRiskParameterSet(StrategyRiskParameterSet ParameterSet,
    string PayloadSha256, ConfigurationParameterSetStatus Status, DateTime? EffectiveFromUtc, DateTime? RetiredAtUtc);
