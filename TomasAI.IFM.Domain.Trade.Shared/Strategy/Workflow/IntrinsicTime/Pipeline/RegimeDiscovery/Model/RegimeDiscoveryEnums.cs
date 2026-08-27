namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;

/// <summary>Identifies the signed market direction produced by Regime Discovery.</summary>
public enum RegimeDirection : byte
{
    /// <summary>No direction has been calculated.</summary>
    Unknown = 0,
    /// <summary>The market direction is negative.</summary>
    Down = 1,
    /// <summary>The market direction is neutral.</summary>
    Neutral = 2,
    /// <summary>The market direction is positive.</summary>
    Up = 3
}

/// <summary>Identifies the absolute strength of a directional trend.</summary>
public enum TrendRegimeStrength : byte
{
    /// <summary>No directional strength applies.</summary>
    None = 0,
    /// <summary>The trend is weak.</summary>
    Weak = 1,
    /// <summary>The trend is moderate.</summary>
    Moderate = 2,
    /// <summary>The trend is strong.</summary>
    Strong = 3,
    /// <summary>The trend is extreme.</summary>
    Extreme = 4
}

/// <summary>Identifies the current trend lifecycle phase.</summary>
public enum TrendRegimePhase : byte
{
    /// <summary>No phase has been calculated.</summary>
    Unknown = 0,
    /// <summary>The market is range bound.</summary>
    RangeBound = 1,
    /// <summary>A directional trend is emerging.</summary>
    Emerging = 2,
    /// <summary>A directional trend is established.</summary>
    Established = 3,
    /// <summary>The trend is losing momentum.</summary>
    Exhausting = 4,
    /// <summary>The trend is reversing.</summary>
    Reversing = 5
}

/// <summary>Identifies the composite volatility level.</summary>
public enum VolatilityRegimeLevel : byte
{
    /// <summary>No level has been calculated.</summary>
    Unknown = 0,
    /// <summary>Volatility is low.</summary>
    Low = 1,
    /// <summary>Volatility is normal.</summary>
    Normal = 2,
    /// <summary>Volatility is high.</summary>
    High = 3,
    /// <summary>Volatility is extreme.</summary>
    Extreme = 4
}

/// <summary>Identifies how volatility is changing.</summary>
public enum VolatilityRegimeChange : byte
{
    /// <summary>No change has been calculated.</summary>
    Unknown = 0,
    /// <summary>Volatility is contracting.</summary>
    Contracting = 1,
    /// <summary>Volatility is stable.</summary>
    Stable = 2,
    /// <summary>Volatility is expanding.</summary>
    Expanding = 3
}

/// <summary>Identifies the VX futures term-structure state.</summary>
public enum VxTermStructureRegime : byte
{
    /// <summary>No term-structure state has been calculated.</summary>
    Unknown = 0,
    /// <summary>The front contract is below the second contract.</summary>
    Contango = 1,
    /// <summary>The two contracts are effectively flat.</summary>
    Flat = 2,
    /// <summary>The front contract is above the second contract.</summary>
    Backwardation = 3
}

/// <summary>Identifies the price-structure classification.</summary>
public enum MarketStructureClassification : byte
{
    /// <summary>The structure is incomplete and cannot complete the pipeline.</summary>
    Unknown = 0,
    /// <summary>Price is directionally organized.</summary>
    Trending = 1,
    /// <summary>Price is range bound.</summary>
    Ranging = 2,
    /// <summary>Price and volatility are compressing.</summary>
    Compressing = 3,
    /// <summary>Price and volatility are expanding.</summary>
    Expanding = 4,
    /// <summary>Price is breaking beyond its configured range.</summary>
    BreakingOut = 5,
    /// <summary>Price is transitioning between stable classifications.</summary>
    Transitioning = 6
}

/// <summary>Identifies a market-structure breakout direction.</summary>
public enum MarketBreakoutState : byte
{
    /// <summary>No breakout is present.</summary>
    None = 0,
    /// <summary>Price is breaking below its rolling low.</summary>
    Down = 1,
    /// <summary>Price is breaking above its rolling high.</summary>
    Up = 2
}

/// <summary>Identifies a normalized confidence band.</summary>
public enum RegimeConfidenceBand : byte
{
    /// <summary>No confidence band has been calculated.</summary>
    Unknown = 0,
    /// <summary>Confidence is below 0.35.</summary>
    Low = 1,
    /// <summary>Confidence is at least 0.35 and below 0.60.</summary>
    Moderate = 2,
    /// <summary>Confidence is at least 0.60 and below 0.80.</summary>
    High = 3,
    /// <summary>Confidence is at least 0.80.</summary>
    VeryHigh = 4
}

/// <summary>Identifies the final Regime Discovery result quality.</summary>
public enum RegimeOverallQuality : byte
{
    /// <summary>No valid quality has been calculated.</summary>
    Unknown = 0,
    /// <summary>The result has low confidence.</summary>
    Low = 1,
    /// <summary>The result is usable with degraded evidence.</summary>
    Degraded = 2,
    /// <summary>The result is acceptable.</summary>
    Acceptable = 3,
    /// <summary>The result has high-quality evidence.</summary>
    High = 4
}

/// <summary>Identifies a deterministic restriction for later strategy stages.</summary>
public enum RegimeRestriction : byte
{
    /// <summary>No restriction applies.</summary>
    None = 0,
    /// <summary>No new trade should be selected.</summary>
    NoNewTrade = 1,
    /// <summary>Trend and structure directions conflict.</summary>
    DirectionConflict = 2,
    /// <summary>The fused confidence is low.</summary>
    LowConfidence = 3,
    /// <summary>Market structure is transitioning.</summary>
    Transition = 4
}

/// <summary>Identifies the severity of a stable Regime Discovery reason.</summary>
public enum RegimeReasonSeverity : byte
{
    /// <summary>No severity applies.</summary>
    None = 0,
    /// <summary>The reason is informational.</summary>
    Information = 1,
    /// <summary>The reason is a warning.</summary>
    Warning = 2,
    /// <summary>The reason restricts later processing.</summary>
    Restriction = 3,
    /// <summary>The reason prevents successful completion.</summary>
    Failure = 4
}

/// <summary>Identifies the calculation area that produced evidence.</summary>
public enum RegimeEvidenceArea : byte
{
    /// <summary>No area is identified.</summary>
    Unknown = 0,
    /// <summary>Configuration evidence.</summary>
    Configuration = 1,
    /// <summary>Market-data quality evidence.</summary>
    Data = 2,
    /// <summary>Trend evidence.</summary>
    Trend = 3,
    /// <summary>Volatility evidence.</summary>
    Volatility = 4,
    /// <summary>Market Structure evidence.</summary>
    MarketStructure = 5,
    /// <summary>Fusion evidence.</summary>
    Fusion = 6,
    /// <summary>Pipeline lifecycle evidence.</summary>
    Pipeline = 7
}
