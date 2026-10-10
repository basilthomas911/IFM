using System.Collections.Immutable;

namespace TomasAI.IFM.Domain.MarketData.Shared.OptionChainCache;

/// <summary>Approved ES defaults. Catalog identity is supplied explicitly; creation never publishes or enables a set.</summary>
public static class StrategyOptionChainParameterDefaults
{
    /// <summary>Iron Condor: 30-45 calendar DTE, preferred 45, 50-point wings and direction-specific short deltas.</summary>
    public static StrategyOptionChainParameterSet IronCondor(Guid setId, Guid strategyId, int strategyVersion) =>
        Create(setId, strategyId, strategyVersion, "Iron Condor", 30, 45, 45);

    /// <summary>Vertical Spreads: 5-10 calendar DTE, preferred 5, with the same side-specific delta targets and 50-point widths.</summary>
    public static StrategyOptionChainParameterSet VerticalSpread(Guid setId, Guid strategyId, int strategyVersion) =>
        Create(setId, strategyId, strategyVersion, "Vertical Spread", 5, 10, 5);

    static StrategyOptionChainParameterSet Create(Guid setId, Guid strategyId, int strategyVersion, string strategy, int min, int max, int preferred) => new()
    {
        ParameterSetId = setId, StrategyDefinitionId = strategyId, StrategyDefinitionVersion = strategyVersion, Name = strategy + " Global Chain Cache",
        BiasRows = new[] { Row(OptionStrategyMarketBias.Neutral, .16m, .16m, min, max, preferred), Row(OptionStrategyMarketBias.Bullish, .20m, .10m, min, max, preferred),
            Row(OptionStrategyMarketBias.Bearish, .10m, .20m, min, max, preferred) }.ToImmutableArray()
    };

    // Initial signed net target is short-put magnitude minus short-call magnitude; qualifying wing deltas remain included by the composer.
    // Delta percentages are converted to unit magnitudes (16% = 0.16); ranges keep a labelled +/-0.03 selection tolerance.
    static OptionStrategyBiasParameters Row(OptionStrategyMarketBias bias, decimal put, decimal call, int min, int max, int preferred) => new()
    {
        MarketBias = bias, MinimumDte = min, MaximumDte = max, PreferredDte = preferred,
        PutDelta = new(put - .03m, put, put + .03m), CallDelta = new(call - .03m, call, call + .03m),
        PutWingWidths = [50], CallWingWidths = [50], TargetNetDelta = put - call, NetDeltaTolerance = .05m,
        MaximumLegSpreadPoints = 1, MinimumNetCredit = 0
    };
}
