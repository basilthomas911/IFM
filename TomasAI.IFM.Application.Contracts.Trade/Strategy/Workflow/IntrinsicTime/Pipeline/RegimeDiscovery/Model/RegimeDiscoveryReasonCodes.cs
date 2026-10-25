namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;

/// <summary>Defines stable machine-readable Regime Discovery V1 reason codes.</summary>
public static class RegimeDiscoveryReasonCodes
{
    /// <summary>Configuration is invalid.</summary>
    public const string ConfigurationInvalid = "RD.CONFIG.INVALID";
    /// <summary>Configuration was not found.</summary>
    public const string ConfigurationNotFound = "RD.CONFIG.NOT_FOUND";
    /// <summary>Configuration version is incompatible.</summary>
    public const string ConfigurationVersionMismatch = "RD.CONFIG.VERSION_MISMATCH";
    /// <summary>Configuration payload hash does not match.</summary>
    public const string ConfigurationHashMismatch = "RD.CONFIG.HASH_MISMATCH";
    /// <summary>A required signal is missing.</summary>
    public const string RequiredDataMissing = "RD.DATA.REQUIRED_MISSING";
    /// <summary>An optional signal is missing.</summary>
    public const string OptionalDataMissing = "RD.DATA.OPTIONAL_MISSING";
    /// <summary>A signal is stale.</summary>
    public const string DataStale = "RD.DATA.STALE";
    /// <summary>A signal is not warm.</summary>
    public const string DataNotWarm = "RD.DATA.NOT_WARM";
    /// <summary>A signal is invalid.</summary>
    public const string DataInvalid = "RD.DATA.INVALID";
    /// <summary>A signal timestamp is in the future.</summary>
    public const string FutureDataTimestamp = "RD.DATA.FUTURE_TIMESTAMP";
    /// <summary>A signal schema is unsupported.</summary>
    public const string DataSchemaUnsupported = "RD.DATA.SCHEMA_UNSUPPORTED";
    /// <summary>A signal calculation version is incompatible.</summary>
    public const string CalculationVersionMismatch = "RD.DATA.CALCULATION_VERSION_MISMATCH";
    /// <summary>The calculated trend is up.</summary>
    public const string TrendUp = "RD.TREND.UP";
    /// <summary>The calculated trend is down.</summary>
    public const string TrendDown = "RD.TREND.DOWN";
    /// <summary>The calculated trend is neutral.</summary>
    public const string TrendNeutral = "RD.TREND.NEUTRAL";
    /// <summary>Trend timeframes conflict.</summary>
    public const string TrendTimeFrameConflict = "RD.TREND.TIMEFRAME_CONFLICT";
    /// <summary>Trend momentum diverges.</summary>
    public const string TrendMomentumDivergence = "RD.TREND.MOMENTUM_DIVERGENCE";
    /// <summary>The trend is reversing.</summary>
    public const string TrendReversing = "RD.TREND.REVERSING";
    /// <summary>Volatility is low.</summary>
    public const string VolatilityLow = "RD.VOL.LOW";
    /// <summary>Volatility is normal.</summary>
    public const string VolatilityNormal = "RD.VOL.NORMAL";
    /// <summary>Volatility is high.</summary>
    public const string VolatilityHigh = "RD.VOL.HIGH";
    /// <summary>Volatility is extreme.</summary>
    public const string VolatilityExtreme = "RD.VOL.EXTREME";
    /// <summary>Volatility is expanding.</summary>
    public const string VolatilityExpanding = "RD.VOL.EXPANDING";
    /// <summary>Volatility is contracting.</summary>
    public const string VolatilityContracting = "RD.VOL.CONTRACTING";
    /// <summary>VX futures are in contango.</summary>
    public const string VolatilityContango = "RD.VOL.CONTANGO";
    /// <summary>VX futures are in backwardation.</summary>
    public const string VolatilityBackwardation = "RD.VOL.BACKWARDATION";
    /// <summary>Market Structure is trending.</summary>
    public const string StructureTrending = "RD.STRUCT.TRENDING";
    /// <summary>Market Structure is ranging.</summary>
    public const string StructureRanging = "RD.STRUCT.RANGING";
    /// <summary>Market Structure is compressing.</summary>
    public const string StructureCompressing = "RD.STRUCT.COMPRESSING";
    /// <summary>Market Structure is expanding.</summary>
    public const string StructureExpanding = "RD.STRUCT.EXPANDING";
    /// <summary>Market Structure is breaking out upward.</summary>
    public const string StructureBreakoutUp = "RD.STRUCT.BREAKOUT_UP";
    /// <summary>Market Structure is breaking out downward.</summary>
    public const string StructureBreakoutDown = "RD.STRUCT.BREAKOUT_DOWN";
    /// <summary>Market Structure is transitioning.</summary>
    public const string StructureTransitioning = "RD.STRUCT.TRANSITIONING";
    /// <summary>Trend and structure are aligned.</summary>
    public const string FusionAligned = "RD.FUSION.ALIGNED";
    /// <summary>Trend and structure directions conflict.</summary>
    public const string FusionDirectionConflict = "RD.FUSION.DIRECTION_CONFLICT";
    /// <summary>Fusion confidence is low.</summary>
    public const string FusionLowConfidence = "RD.FUSION.LOW_CONFIDENCE";
    /// <summary>Fusion disallows a new trade.</summary>
    public const string FusionNoNewTrade = "RD.FUSION.NO_NEW_TRADE";
    /// <summary>The market is transitioning.</summary>
    public const string FusionTransition = "RD.FUSION.TRANSITION";
    /// <summary>A specialist calculation failed.</summary>
    public const string SpecialistFailed = "RD.PIPELINE.SPECIALIST_FAILED";
    /// <summary>Fusion failed.</summary>
    public const string FusionFailed = "RD.PIPELINE.FUSION_FAILED";
    /// <summary>A deterministic consistency rule failed.</summary>
    public const string ConsistencyFault = "RD.PIPELINE.CONSISTENCY_FAULT";
}
