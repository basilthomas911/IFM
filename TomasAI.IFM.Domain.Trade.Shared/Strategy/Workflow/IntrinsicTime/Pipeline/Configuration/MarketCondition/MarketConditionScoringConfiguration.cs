using FluentValidation;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.MarketCondition;

[MessagePackObject]
public sealed record MarketConditionScoringConfiguration
{
    [Key(0)] public decimal RegimeAlignmentWeight { get; init; } = 0.30m;
    [Key(1)] public decimal TriggerQualityWeight { get; init; } = 0.25m;
    [Key(2)] public decimal FuturesLiquidityWeight { get; init; } = 0.15m;
    [Key(3)] public decimal OptionLiquidityWeight { get; init; } = 0.15m;
    [Key(4)] public decimal DataQualityWeight { get; init; } = 0.10m;
    [Key(5)] public decimal EntryTimingWeight { get; init; } = 0.05m;
    [Key(6)] public decimal MinimumStrength { get; init; } = 55m;
    [Key(7)] public decimal MinimumConfidence { get; init; } = 0.65m;
    [Key(8)] public decimal OptionalMissingPenalty { get; init; } = 0.05m;
    [Key(9)] public decimal OptionalMissingMaximumPenalty { get; init; } = 0.15m;
    [Key(10)] public decimal LowConfidencePenalty { get; init; } = 0.10m;
    [Key(11)] public decimal TransitionPenalty { get; init; } = 0.10m;
    [Key(12)] public decimal ConflictingEvidencePenalty { get; init; } = 0.10m;
    [Key(13)] public decimal ConflictingEvidenceMaximumPenalty { get; init; } = 0.20m;
    [Key(14)] public decimal MaximumTotalPenalty { get; init; } = 0.35m;
}
