using TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;

namespace TomasAI.IFM.Domain.Trade.VerificationTests.Strategy.IntrinsicTime.RegimeDiscovery;

public static class RegimeDiscoveryScenarioCatalog
{
    public static RegimeDiscoveryScenario TrendingUp { get; } = new()
    {
        Name = nameof(TrendingUp),
        Values = BullishValues(0.4m),
        TrendDirection = RegimeDirection.Up,
        TrendStrength = TrendRegimeStrength.Strong,
        TrendPhase = TrendRegimePhase.Established,
        TrendScore = 0.799250m,
        VolatilityLevel = VolatilityRegimeLevel.Normal,
        VolatilityChange = VolatilityRegimeChange.Stable,
        TermStructure = VxTermStructureRegime.Contango,
        VolatilityScore = 0.353125m,
        StructureClassification = MarketStructureClassification.Trending,
        StructureDirection = RegimeDirection.Up,
        Breakout = MarketBreakoutState.None,
        StructureScore = 0.966667m,
        FusionDirection = RegimeDirection.Up,
        FusionScore = 0.857846m,
        Conviction = 0.706383m,
        RequiredReasonCodes =
        [
            RegimeDiscoveryReasonCodes.TrendUp,
            RegimeDiscoveryReasonCodes.VolatilityNormal,
            RegimeDiscoveryReasonCodes.StructureTrending,
            RegimeDiscoveryReasonCodes.FusionAligned
        ]
    };

    public static RegimeDiscoveryScenario BullishBreakout { get; } = TrendingUp.With(
        nameof(BullishBreakout),
        (RegimeDiscoverySignalMetric.CurrentPrice, 105.2m),
        (RegimeDiscoverySignalMetric.BreakoutDistanceAtr, 0.6m)) with
    {
        StructureClassification = MarketStructureClassification.BreakingOut,
        StructureDirection = RegimeDirection.Up,
        Breakout = MarketBreakoutState.Up,
        StructureScore = 0.300000m,
        FusionScore = 0.624512m,
        Conviction = 0.514247m,
        RequiredReasonCodes =
        [
            RegimeDiscoveryReasonCodes.TrendUp,
            RegimeDiscoveryReasonCodes.VolatilityNormal,
            RegimeDiscoveryReasonCodes.StructureBreakoutUp,
            RegimeDiscoveryReasonCodes.FusionAligned
        ]
    };

    public static RegimeDiscoveryScenario TrendingDown { get; } = TrendingUp.With(
        nameof(TrendingDown),
        (RegimeDiscoverySignalMetric.CurrentPrice, 96.2m),
        (RegimeDiscoverySignalMetric.Ema20, 97m),
        (RegimeDiscoverySignalMetric.Ema50, 99m),
        (RegimeDiscoverySignalMetric.Ema200, 101m),
        (RegimeDiscoverySignalMetric.Ema20Slope, -0.08m),
        (RegimeDiscoverySignalMetric.Ema50Slope, -0.06m),
        (RegimeDiscoverySignalMetric.Ema200Slope, -0.04m),
        (RegimeDiscoverySignalMetric.Rsi14, 35m),
        (RegimeDiscoverySignalMetric.Rsi14Slope, -2m),
        (RegimeDiscoverySignalMetric.PlusDi14, 15m),
        (RegimeDiscoverySignalMetric.MinusDi14, 30m),
        (RegimeDiscoverySignalMetric.MacdHistogram, -0.5m),
        (RegimeDiscoverySignalMetric.BollingerPosition, -0.5m),
        (RegimeDiscoverySignalMetric.Ema20Interaction, -1m),
        (RegimeDiscoverySignalMetric.BreakoutDistanceAtr, -0.4m),
        (RegimeDiscoverySignalMetric.ItiDirection, -1m),
        (RegimeDiscoverySignalMetric.Tdi, -1m)) with
    {
        TrendDirection = RegimeDirection.Down,
        TrendScore = -0.799250m,
        StructureDirection = RegimeDirection.Down,
        StructureScore = -0.966667m,
        FusionDirection = RegimeDirection.Down,
        FusionScore = -0.857846m,
        Conviction = 0.706383m,
        RequiredReasonCodes =
        [
            RegimeDiscoveryReasonCodes.TrendDown,
            RegimeDiscoveryReasonCodes.VolatilityNormal,
            RegimeDiscoveryReasonCodes.StructureTrending,
            RegimeDiscoveryReasonCodes.FusionAligned
        ]
    };

    public static RegimeDiscoveryScenario RangeBound { get; } = TrendingUp.With(
        nameof(RangeBound),
        (RegimeDiscoverySignalMetric.CurrentPrice, 100m),
        (RegimeDiscoverySignalMetric.Ema20, 100m),
        (RegimeDiscoverySignalMetric.Ema50, 100m),
        (RegimeDiscoverySignalMetric.Ema200, 100m),
        (RegimeDiscoverySignalMetric.Ema20Slope, 0m),
        (RegimeDiscoverySignalMetric.Ema50Slope, 0m),
        (RegimeDiscoverySignalMetric.Ema200Slope, 0m),
        (RegimeDiscoverySignalMetric.Rsi14, 50m),
        (RegimeDiscoverySignalMetric.Rsi14Slope, 0m),
        (RegimeDiscoverySignalMetric.Adx14, 15m),
        (RegimeDiscoverySignalMetric.PlusDi14, 20m),
        (RegimeDiscoverySignalMetric.MinusDi14, 20m),
        (RegimeDiscoverySignalMetric.MacdHistogram, 0m),
        (RegimeDiscoverySignalMetric.BollingerPosition, 0m),
        (RegimeDiscoverySignalMetric.Ema20Interaction, 0m),
        (RegimeDiscoverySignalMetric.AtrNormalizedRange, 0.5m),
        (RegimeDiscoverySignalMetric.BreakoutDistanceAtr, 0m),
        (RegimeDiscoverySignalMetric.ItiDirection, 0m),
        (RegimeDiscoverySignalMetric.ItiBandLevel, 0.5m),
        (RegimeDiscoverySignalMetric.Tdi, 0m)) with
    {
        TrendDirection = RegimeDirection.Neutral,
        TrendStrength = TrendRegimeStrength.None,
        TrendPhase = TrendRegimePhase.RangeBound,
        TrendScore = 0m,
        StructureClassification = MarketStructureClassification.Ranging,
        StructureDirection = RegimeDirection.Neutral,
        StructureScore = 0m,
        FusionDirection = RegimeDirection.Neutral,
        FusionScore = 0m,
        Conviction = 0m,
        RequiredReasonCodes =
        [
            RegimeDiscoveryReasonCodes.TrendNeutral,
            RegimeDiscoveryReasonCodes.VolatilityNormal,
            RegimeDiscoveryReasonCodes.StructureRanging,
            RegimeDiscoveryReasonCodes.FusionAligned
        ]
    };

    public static RegimeDiscoveryScenario BearishBreakout { get; } = TrendingDown.With(
        nameof(BearishBreakout),
        (RegimeDiscoverySignalMetric.CurrentPrice, 94.8m),
        (RegimeDiscoverySignalMetric.BreakoutDistanceAtr, -0.6m)) with
    {
        StructureClassification = MarketStructureClassification.BreakingOut,
        Breakout = MarketBreakoutState.Down,
        StructureScore = -0.300000m,
        FusionScore = -0.624512m,
        Conviction = 0.514247m,
        RequiredReasonCodes =
        [
            RegimeDiscoveryReasonCodes.TrendDown,
            RegimeDiscoveryReasonCodes.VolatilityNormal,
            RegimeDiscoveryReasonCodes.StructureBreakoutDown,
            RegimeDiscoveryReasonCodes.FusionAligned
        ]
    };

    public static RegimeDiscoveryScenario Compressing { get; } = RangeBound.With(
        nameof(Compressing),
        (RegimeDiscoverySignalMetric.BollingerWidthRatio, 0.70m),
        (RegimeDiscoverySignalMetric.AtrBaselineRatio, 0.80m)) with
    {
        VolatilityLevel = VolatilityRegimeLevel.Low,
        VolatilityChange = VolatilityRegimeChange.Contracting,
        VolatilityScore = 0.241125m,
        StructureClassification = MarketStructureClassification.Compressing,
        RequiredReasonCodes =
        [
            RegimeDiscoveryReasonCodes.TrendNeutral,
            RegimeDiscoveryReasonCodes.StructureCompressing
        ]
    };

    public static RegimeDiscoveryScenario ExpandingUp { get; } = TrendingUp.With(
        nameof(ExpandingUp),
        (RegimeDiscoverySignalMetric.BollingerWidthRatio, 1.30m)) with
    {
        StructureClassification = MarketStructureClassification.Expanding,
        StructureDirection = RegimeDirection.Up,
        StructureScore = 1m,
        FusionScore = 0.869512m,
        Conviction = 0.715989m,
        RequiredReasonCodes =
        [
            RegimeDiscoveryReasonCodes.TrendUp,
            RegimeDiscoveryReasonCodes.StructureExpanding,
            RegimeDiscoveryReasonCodes.FusionAligned
        ]
    };

    public static RegimeDiscoveryScenario Transitioning { get; } = TrendingUp.With(
        nameof(Transitioning),
        (RegimeDiscoverySignalMetric.BollingerPosition, -0.5m),
        (RegimeDiscoverySignalMetric.BreakoutDistanceAtr, 0.4m)) with
    {
        StructureClassification = MarketStructureClassification.Transitioning,
        StructureDirection = RegimeDirection.Neutral,
        StructureScore = 0m,
        FusionScore = 0.519512m,
        Conviction = 0.427786m,
        Restrictions = [RegimeRestriction.Transition],
        RequiredReasonCodes =
        [
            RegimeDiscoveryReasonCodes.TrendUp,
            RegimeDiscoveryReasonCodes.StructureTransitioning,
            RegimeDiscoveryReasonCodes.FusionTransition
        ]
    };

    public static RegimeDiscoveryScenario ExtremeVolatility { get; } = TrendingUp.With(
        nameof(ExtremeVolatility),
        (RegimeDiscoverySignalMetric.VixLevel, 35m),
        (RegimeDiscoverySignalMetric.AtrBaselineRatio, 1.5m),
        (RegimeDiscoverySignalMetric.VxFrontSecondRatio, 1.08m),
        (RegimeDiscoverySignalMetric.RealizedVolatilityPercentile, 0.9m)) with
    {
        VolatilityLevel = VolatilityRegimeLevel.Extreme,
        VolatilityChange = VolatilityRegimeChange.Expanding,
        TermStructure = VxTermStructureRegime.Backwardation,
        NoNewTrade = true,
        VolatilityScore = 0.792875m,
        StructureClassification = MarketStructureClassification.Expanding,
        StructureScore = 1m,
        FusionScore = 0.869512m,
        Conviction = 0.446084m,
        Restrictions = [RegimeRestriction.NoNewTrade],
        RequiredReasonCodes =
        [
            RegimeDiscoveryReasonCodes.TrendUp,
            RegimeDiscoveryReasonCodes.VolatilityExtreme,
            RegimeDiscoveryReasonCodes.StructureExpanding,
            RegimeDiscoveryReasonCodes.FusionNoNewTrade
        ]
    };

    public static RegimeDiscoveryScenario DirectionConflict { get; } = TrendingUp.With(
        nameof(DirectionConflict),
        (RegimeDiscoverySignalMetric.BollingerPosition, -0.5m),
        (RegimeDiscoverySignalMetric.Ema20Interaction, -1m),
        (RegimeDiscoverySignalMetric.BreakoutDistanceAtr, -0.4m),
        (RegimeDiscoverySignalMetric.ItiDirection, -1m),
        (RegimeDiscoverySignalMetric.Tdi, -1m)) with
    {
        TrendStrength = TrendRegimeStrength.Moderate,
        TrendPhase = TrendRegimePhase.Exhausting,
        TrendScore = 0.614250m,
        StructureClassification = MarketStructureClassification.Trending,
        StructureDirection = RegimeDirection.Down,
        StructureScore = -0.966667m,
        FusionDirection = RegimeDirection.Neutral,
        FusionScore = 0.060929m,
        Conviction = 0.037628m,
        Restrictions = [RegimeRestriction.DirectionConflict, RegimeRestriction.Transition],
        RequiredReasonCodes =
        [
            RegimeDiscoveryReasonCodes.TrendUp,
            RegimeDiscoveryReasonCodes.VolatilityNormal,
            RegimeDiscoveryReasonCodes.StructureTrending,
            RegimeDiscoveryReasonCodes.FusionDirectionConflict,
            RegimeDiscoveryReasonCodes.FusionTransition
        ]
    };

    public static RegimeDiscoveryScenario OptionalEvidenceMissing { get; } = TrendingUp with
    {
        Name = nameof(OptionalEvidenceMissing),
        OmittedMetrics = new HashSet<RegimeDiscoverySignalMetric>
        {
            RegimeDiscoverySignalMetric.RealizedVolatilityPercentile
        },
        VolatilityScore = 0.347917m,
        FusionScore = null,
        Conviction = null,
        RequiredReasonCodes = [RegimeDiscoveryReasonCodes.OptionalDataMissing]
    };

    public static IReadOnlyList<RegimeDiscoveryScenario> StructureScenarios { get; } =
    [
        TrendingUp, TrendingDown, RangeBound, BullishBreakout, BearishBreakout,
        Compressing, ExpandingUp, Transitioning
    ];

    static Dictionary<RegimeDiscoverySignalMetric, decimal> BullishValues(decimal breakoutDistance) => new()
    {
        [RegimeDiscoverySignalMetric.CurrentPrice] = 104.8m,
        [RegimeDiscoverySignalMetric.Ema20] = 103m,
        [RegimeDiscoverySignalMetric.Ema50] = 101m,
        [RegimeDiscoverySignalMetric.Ema200] = 99m,
        [RegimeDiscoverySignalMetric.Ema20Slope] = 0.08m,
        [RegimeDiscoverySignalMetric.Ema50Slope] = 0.06m,
        [RegimeDiscoverySignalMetric.Ema200Slope] = 0.04m,
        [RegimeDiscoverySignalMetric.Rsi14] = 65m,
        [RegimeDiscoverySignalMetric.Rsi14Slope] = 2m,
        [RegimeDiscoverySignalMetric.Adx14] = 30m,
        [RegimeDiscoverySignalMetric.PlusDi14] = 30m,
        [RegimeDiscoverySignalMetric.MinusDi14] = 15m,
        [RegimeDiscoverySignalMetric.MacdHistogram] = 0.5m,
        [RegimeDiscoverySignalMetric.Atr14] = 2m,
        [RegimeDiscoverySignalMetric.VixLevel] = 18m,
        [RegimeDiscoverySignalMetric.VxFrontLevel] = 18m,
        [RegimeDiscoverySignalMetric.AtrBaselineRatio] = 1m,
        [RegimeDiscoverySignalMetric.VxFrontSecondRatio] = 0.95m,
        [RegimeDiscoverySignalMetric.PriorVolatilityComposite] = 0.35m,
        [RegimeDiscoverySignalMetric.RealizedVolatilityPercentile] = 0.40m,
        [RegimeDiscoverySignalMetric.BollingerWidthRatio] = 1m,
        [RegimeDiscoverySignalMetric.BollingerWidth] = 8m,
        [RegimeDiscoverySignalMetric.BollingerPosition] = 0.5m,
        [RegimeDiscoverySignalMetric.Ema20Interaction] = 1m,
        [RegimeDiscoverySignalMetric.AtrNormalizedRange] = 1m,
        [RegimeDiscoverySignalMetric.RollingHigh20] = 104m,
        [RegimeDiscoverySignalMetric.RollingLow20] = 96m,
        [RegimeDiscoverySignalMetric.BreakoutDistanceAtr] = breakoutDistance,
        [RegimeDiscoverySignalMetric.ItiDirection] = 1m,
        [RegimeDiscoverySignalMetric.ItiBandLevel] = 1.2m,
        [RegimeDiscoverySignalMetric.ItiReversalLevel] = 0.1m,
        [RegimeDiscoverySignalMetric.Tdi] = 1m
    };
}
