namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

public sealed record OptionVolatilityNumericalRules
{
    public decimal? MinimumIvRank { get; init; }
    public decimal? MaximumIvRank { get; init; }
    public decimal? MinimumIvPercentile { get; init; }
    public decimal? MaximumIvPercentile { get; init; }
    public decimal? DeltaTargetAdjustment { get; init; }
    public decimal? SpreadWidthAdjustment { get; init; }
    public decimal? RiskLimitAdjustment { get; init; }
}
