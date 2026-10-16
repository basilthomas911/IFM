namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

[MessagePack.MessagePackObject]
public sealed record OptionVolatilityNumericalRules
{
    [MessagePack.Key(0)] public decimal? MinimumIvRank { get; init; }
    [MessagePack.Key(1)] public decimal? MaximumIvRank { get; init; }
    [MessagePack.Key(2)] public decimal? MinimumIvPercentile { get; init; }
    [MessagePack.Key(3)] public decimal? MaximumIvPercentile { get; init; }
    [MessagePack.Key(4)] public decimal? DeltaTargetAdjustment { get; init; }
    [MessagePack.Key(5)] public decimal? SpreadWidthAdjustment { get; init; }
    [MessagePack.Key(6)] public decimal? RiskLimitAdjustment { get; init; }
}
