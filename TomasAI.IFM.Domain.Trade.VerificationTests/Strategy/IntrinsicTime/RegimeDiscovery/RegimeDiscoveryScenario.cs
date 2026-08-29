using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;

namespace TomasAI.IFM.Domain.Trade.VerificationTests.Strategy.IntrinsicTime.RegimeDiscovery;

public sealed record RegimeDiscoveryScenario
{
    public required string Name { get; init; }
    public required IReadOnlyDictionary<RegimeDiscoverySignalMetric, decimal> Values { get; init; }
    public IReadOnlySet<RegimeDiscoverySignalMetric> OmittedMetrics { get; init; } = new HashSet<RegimeDiscoverySignalMetric>();
    public RegimeDirection TrendDirection { get; init; }
    public TrendRegimeStrength TrendStrength { get; init; }
    public TrendRegimePhase TrendPhase { get; init; }
    public decimal? TrendScore { get; init; }
    public VolatilityRegimeLevel VolatilityLevel { get; init; }
    public VolatilityRegimeChange VolatilityChange { get; init; }
    public VxTermStructureRegime TermStructure { get; init; }
    public decimal? VolatilityScore { get; init; }
    public bool NoNewTrade { get; init; }
    public MarketStructureClassification StructureClassification { get; init; }
    public RegimeDirection StructureDirection { get; init; }
    public MarketBreakoutState Breakout { get; init; }
    public decimal? StructureScore { get; init; }
    public RegimeDirection FusionDirection { get; init; }
    public decimal? FusionScore { get; init; }
    public decimal? Conviction { get; init; }
    public RegimeRestriction[] Restrictions { get; init; } = [];
    public string[] RequiredReasonCodes { get; init; } = [];

    public decimal Value(RegimeDiscoverySignalMetric metric) => Values[metric];

    public RegimeDiscoveryScenario With(
        string name,
        params (RegimeDiscoverySignalMetric Metric, decimal Value)[] changes)
    {
        var values = Values.ToDictionary();
        foreach (var (metric, value) in changes)
            values[metric] = value;
        return this with { Name = name, Values = values };
    }
}
