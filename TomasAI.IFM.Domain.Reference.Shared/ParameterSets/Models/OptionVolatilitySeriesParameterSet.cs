namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

/// <summary>Immutable authoring contract for a comparable option-volatility series.</summary>
public sealed record OptionVolatilitySeriesParameterSet
{
    public int SchemaVersion { get; init; } = 1;
    public Guid ParameterSetId { get; init; }
    public int Version { get; init; } = 1;
    public string SeriesId { get; init; } = "ES-ATM-30D";
    public string MethodologyVersion { get; init; } = "methodology-v1";
    public string UnderlyingRoot { get; init; } = "ES";
    public string Venue { get; init; } = "XCME";
    public string Currency { get; init; } = "USD";
    public OptionVolatilityEnvironment Environment { get; init; } = OptionVolatilityEnvironment.Production;
    public string MarketDataProvider { get; init; } = "Databento";
    public string Dataset { get; init; } = "GLBX.MDP3";
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "The published serialization contract uses arrays; preserve its wire shape.")]
    public string[] EligibleProductFamilies { get; init; } = ["ES", "EW", "EOM"];
    public int TargetMaturityCalendarDays { get; init; } = 30;
    public OptionVolatilityAtmConvention AtmConvention { get; init; } = OptionVolatilityAtmConvention.Forward;
    public OptionVolatilityOptionCombination OptionCombination { get; init; } = OptionVolatilityOptionCombination.CallPutArithmeticMean;
    public OptionVolatilityExpirySelection ExpirySelection { get; init; } = OptionVolatilityExpirySelection.QualifiedBracketingExpiries;
    public OptionVolatilityInterpolation Interpolation { get; init; } = OptionVolatilityInterpolation.LinearTotalVariance;
    public OptionVolatilityUnderlyingMatch UnderlyingMatch { get; init; } = OptionVolatilityUnderlyingMatch.Exact;
    public OptionVolatilityQuoteMark QuoteMark { get; init; } = OptionVolatilityQuoteMark.ExecutableMidpoint;
    public int MinimumDisplayedSize { get; init; } = 1;
    public OptionVolatilityCrossedMarketPolicy CrossedMarketPolicy { get; init; } = OptionVolatilityCrossedMarketPolicy.Reject;
    public OptionVolatilityLockedMarketPolicy LockedMarketPolicy { get; init; } = OptionVolatilityLockedMarketPolicy.Allow;
    public int MaximumQuoteAgeSeconds { get; init; } = 5;
    public int MaximumIvAgeSeconds { get; init; } = 5;
    public int MaximumQuoteSkewMilliseconds { get; init; } = 250;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "The published serialization contract uses arrays; preserve its wire shape.")]
    public OptionVolatilityPricerSelection[] Pricers { get; init; } =
    [
        new(OptionVolatilityExerciseStyle.European, ["Black76.Managed/v1", "Black76.Rust/v1"]),
        new(OptionVolatilityExerciseStyle.American, ["AmericanFutures.CRR.Managed/v1"])
    ];
    public OptionVolatilityRollPolicy RollPolicy { get; init; } = OptionVolatilityRollPolicy.StableSeries;
    public string ExchangeCalendar { get; init; } = "CME";
    public string TimeZone { get; init; } = "America/Chicago";
    public OptionVolatilitySamplingPolicy DailySamplingPolicy { get; init; } = OptionVolatilitySamplingPolicy.ExchangeDefinedDailySettlement;
    public int IntradayCoalescingMinutes { get; init; } = 5;
    public int MaximumIntradayCheckpoints { get; init; } = 84;
    public int HistoricalLookbackSessions { get; init; } = 252;
    public int MinimumValidObservations { get; init; } = 220;
    public decimal MinimumCoverage { get; init; } = .85m;
    public OptionVolatilityGapPolicy GapPolicy { get; init; } = OptionVolatilityGapPolicy.Preserve;
    public OptionVolatilityRankBounds RankBounds { get; init; } = OptionVolatilityRankBounds.CurrentAndPrior;
    public OptionVolatilityPercentileConvention PercentileConvention { get; init; } = OptionVolatilityPercentileConvention.StrictlyBelowPrior;
    public ImpliedVolatilityUnit ImpliedVolatilityUnit { get; init; } = ImpliedVolatilityUnit.AnnualDecimal;
    public VolatilityMetricUnit RankAndPercentileUnit { get; init; } = VolatilityMetricUnit.ZeroToOneHundred;
}
