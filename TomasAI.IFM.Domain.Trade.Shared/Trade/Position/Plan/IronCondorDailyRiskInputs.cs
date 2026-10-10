using MessagePack;

namespace TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;

/// <summary>Frozen daily currency PnL and coherent OptionCalculator scenario inputs. No legacy price limit is inferred as a daily currency limit.</summary>
[MessagePackObject]
public sealed record IronCondorDailyRiskInputs
{
    /// <summary>Gets the explicitly captured DailyPnl; null means unavailable.</summary>
    [Key(0)] public decimal? DailyPnl { get; init; }
    /// <summary>Gets the explicitly captured DailyLossLimit; null means unavailable.</summary>
    [Key(1)] public decimal? DailyLossLimit { get; init; }
    /// <summary>Gets the explicitly captured ContractMultiplier; null means unavailable.</summary>
    [Key(2)] public decimal? ContractMultiplier { get; init; }
    /// <summary>Gets the explicitly captured Currency; null means unavailable.</summary>
    [Key(3)] public string? Currency { get; init; }
    /// <summary>Gets the explicitly captured UnderlyingPrice; null means unavailable.</summary>
    [Key(4)] public decimal? UnderlyingPrice { get; init; }
    /// <summary>Gets the explicitly captured ScenarioHorizonSeconds; null means unavailable.</summary>
    [Key(5)] public long? ScenarioHorizonSeconds { get; init; }
    /// <summary>Gets the explicitly captured ScenarioUnderlyingPrice; null means unavailable.</summary>
    [Key(6)] public decimal? ScenarioUnderlyingPrice { get; init; }
    /// <summary>Gets the explicitly captured ScenarioVolatilityShift; null means unavailable.</summary>
    [Key(7)] public double? ScenarioVolatilityShift { get; init; }
    /// <summary>Gets the explicitly captured ScenarioKind; null means unavailable.</summary>
    [Key(8)] public string? ScenarioKind { get; init; }
    /// <summary>Gets the explicitly captured PricingModelVersion; null means unavailable.</summary>
    [Key(9)] public string? PricingModelVersion { get; init; }
    /// <summary>Gets the explicitly captured EstimatedExitCommission; null means unavailable.</summary>
    [Key(10)] public decimal? EstimatedExitCommission { get; init; }
    /// <summary>Gets the explicitly captured EstimatedExitSlippage; null means unavailable.</summary>
    [Key(11)] public decimal? EstimatedExitSlippage { get; init; }
    /// <summary>Gets the explicitly captured RiskValidUntilUtc; null means unavailable.</summary>
    [Key(12)] public DateTime? RiskValidUntilUtc { get; init; }
    /// <summary>Gets the explicitly captured TimeToExpiryDays; null means unavailable.</summary>
    [Key(13)] public double? TimeToExpiryDays { get; init; }
    /// <summary>Gets the four frozen scenario observations.</summary>
    [Key(14)] public IronCondorTradePlanLegObservation[] Legs { get; init; } = [];
    /// <summary>Gets the exact immutable strategy risk policy used to capture these inputs.</summary>
    [Key(15)] public TomasAI.IFM.Domain.Trade.Shared.Strategy.Risk.StrategyRiskParameterSet? StrategyRiskParameterSet { get; init; }
    /// <summary>Gets the exact position sequence used when constructing signed scenario legs.</summary>
    [Key(16)] public long PositionSequence { get; init; }
    /// <summary>Gets the actor route generation used by the captured position.</summary>
    [Key(17)] public long RouteGeneration { get; init; }
    /// <summary>Gets actual commissions attributable to the captured daily session, in broker currency.</summary>
    [Key(18)] public decimal ActualDailyCommission { get; init; }
    /// <summary>Gets the session for the daily commission attribution and scenario capture.</summary>
    [Key(19)] public DateOnly ValueDate { get; init; }
    /// <summary>Gets the short put model OTM estimate.</summary>
    [Key(20)] public double? PutOTMProbability { get; init; }
    /// <summary>Gets the short call model OTM estimate.</summary>
    [Key(21)] public double? CallOTMProbability { get; init; }
}
