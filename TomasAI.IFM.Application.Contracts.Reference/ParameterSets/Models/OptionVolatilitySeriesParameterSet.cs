namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

/// <summary>Immutable authoring contract for a comparable option-volatility series.</summary>
[MessagePack.MessagePackObject]
public sealed record OptionVolatilitySeriesParameterSet
{
    [MessagePack.Key(0)] public int SchemaVersion { get; init; } = 1;
    [MessagePack.Key(1)] public Guid ParameterSetId { get; init; }
    [MessagePack.Key(2)] public int Version { get; init; } = 1;
    [MessagePack.Key(3)] public string SeriesId { get; init; } = "ES-ATM-30D";
    [MessagePack.Key(4)] public string MethodologyVersion { get; init; } = "methodology-v1";
    [MessagePack.Key(5)] public string UnderlyingRoot { get; init; } = "ES";
    [MessagePack.Key(6)] public string Venue { get; init; } = "XCME";
    [MessagePack.Key(7)] public string Currency { get; init; } = "USD";
    [MessagePack.Key(8)] public OptionVolatilityEnvironment Environment { get; init; } = OptionVolatilityEnvironment.Production;
    [MessagePack.Key(9)] public string MarketDataProvider { get; init; } = "Databento";
    [MessagePack.Key(10)] public string Dataset { get; init; } = "GLBX.MDP3";
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "The published serialization contract uses arrays; preserve its wire shape.")]
    [MessagePack.Key(11)] public string[] EligibleProductFamilies { get; init; } = ["ES", "EW", "EOM"];
    [MessagePack.Key(12)] public int TargetMaturityCalendarDays { get; init; } = 30;
    [MessagePack.Key(13)] public OptionVolatilityAtmConvention AtmConvention { get; init; } = OptionVolatilityAtmConvention.Forward;
    [MessagePack.Key(14)] public OptionVolatilityOptionCombination OptionCombination { get; init; } = OptionVolatilityOptionCombination.CallPutArithmeticMean;
    [MessagePack.Key(15)] public OptionVolatilityExpirySelection ExpirySelection { get; init; } = OptionVolatilityExpirySelection.QualifiedBracketingExpiries;
    [MessagePack.Key(16)] public OptionVolatilityInterpolation Interpolation { get; init; } = OptionVolatilityInterpolation.LinearTotalVariance;
    [MessagePack.Key(17)] public OptionVolatilityUnderlyingMatch UnderlyingMatch { get; init; } = OptionVolatilityUnderlyingMatch.Exact;
    [MessagePack.Key(18)] public OptionVolatilityQuoteMark QuoteMark { get; init; } = OptionVolatilityQuoteMark.ExecutableMidpoint;
    [MessagePack.Key(19)] public int MinimumDisplayedSize { get; init; } = 1;
    [MessagePack.Key(20)] public OptionVolatilityCrossedMarketPolicy CrossedMarketPolicy { get; init; } = OptionVolatilityCrossedMarketPolicy.Reject;
    [MessagePack.Key(21)] public OptionVolatilityLockedMarketPolicy LockedMarketPolicy { get; init; } = OptionVolatilityLockedMarketPolicy.Allow;
    [MessagePack.Key(22)] public int MaximumQuoteAgeSeconds { get; init; } = 5;
    [MessagePack.Key(23)] public int MaximumIvAgeSeconds { get; init; } = 5;
    [MessagePack.Key(24)] public int MaximumQuoteSkewMilliseconds { get; init; } = 250;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "The published serialization contract uses arrays; preserve its wire shape.")]
    [MessagePack.Key(25)] public OptionVolatilityPricerSelection[] Pricers { get; init; } =
    [
        new(OptionVolatilityExerciseStyle.European, ["Black76.Managed/v1", "Black76.Rust/v1"]),
        new(OptionVolatilityExerciseStyle.American, ["AmericanFutures.CRR.Managed/v1"])
    ];
    [MessagePack.Key(26)] public OptionVolatilityRollPolicy RollPolicy { get; init; } = OptionVolatilityRollPolicy.StableSeries;
    [MessagePack.Key(27)] public string ExchangeCalendar { get; init; } = "CME";
    [MessagePack.Key(28)] public string TimeZone { get; init; } = "America/Chicago";
    [MessagePack.Key(29)] public OptionVolatilitySamplingPolicy DailySamplingPolicy { get; init; } = OptionVolatilitySamplingPolicy.ExchangeDefinedDailySettlement;
    [MessagePack.Key(30)] public int IntradayCoalescingMinutes { get; init; } = 5;
    [MessagePack.Key(31)] public int MaximumIntradayCheckpoints { get; init; } = 84;
    [MessagePack.Key(32)] public int HistoricalLookbackSessions { get; init; } = 252;
    [MessagePack.Key(33)] public int MinimumValidObservations { get; init; } = 220;
    [MessagePack.Key(34)] public decimal MinimumCoverage { get; init; } = .85m;
    [MessagePack.Key(35)] public OptionVolatilityGapPolicy GapPolicy { get; init; } = OptionVolatilityGapPolicy.Preserve;
    [MessagePack.Key(36)] public OptionVolatilityRankBounds RankBounds { get; init; } = OptionVolatilityRankBounds.CurrentAndPrior;
    [MessagePack.Key(37)] public OptionVolatilityPercentileConvention PercentileConvention { get; init; } = OptionVolatilityPercentileConvention.StrictlyBelowPrior;
    [MessagePack.Key(38)] public ImpliedVolatilityUnit ImpliedVolatilityUnit { get; init; } = ImpliedVolatilityUnit.AnnualDecimal;
    [MessagePack.Key(39)] public VolatilityMetricUnit RankAndPercentileUnit { get; init; } = VolatilityMetricUnit.ZeroToOneHundred;
}
