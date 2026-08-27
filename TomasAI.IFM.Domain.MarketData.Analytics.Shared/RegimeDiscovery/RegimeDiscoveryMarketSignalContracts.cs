using FluentValidation;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;

/// <summary>Identifies one normalized metric consumed by Regime Discovery.</summary>
public enum RegimeDiscoverySignalMetric : byte
{
    /// <summary>No metric is identified.</summary>
    Unknown = 0,
    /// <summary>Current futures price.</summary>
    CurrentPrice = 1,
    /// <summary>EMA20 value.</summary>
    Ema20 = 2,
    /// <summary>EMA50 value.</summary>
    Ema50 = 3,
    /// <summary>EMA200 value.</summary>
    Ema200 = 4,
    /// <summary>ATR-normalized EMA20 slope.</summary>
    Ema20Slope = 5,
    /// <summary>ATR-normalized EMA50 slope.</summary>
    Ema50Slope = 6,
    /// <summary>ATR-normalized EMA200 slope.</summary>
    Ema200Slope = 7,
    /// <summary>RSI14 value.</summary>
    Rsi14 = 8,
    /// <summary>RSI14 slope.</summary>
    Rsi14Slope = 9,
    /// <summary>ADX14 value.</summary>
    Adx14 = 10,
    /// <summary>ADX positive directional indicator.</summary>
    PlusDi14 = 11,
    /// <summary>ADX negative directional indicator.</summary>
    MinusDi14 = 12,
    /// <summary>Conventional MACD histogram.</summary>
    MacdHistogram = 13,
    /// <summary>ATR14 value.</summary>
    Atr14 = 14,
    /// <summary>ATR14 divided by its baseline.</summary>
    AtrBaselineRatio = 15,
    /// <summary>Current VIX level.</summary>
    VixLevel = 16,
    /// <summary>Front VX contract divided by the second contract.</summary>
    VxFrontSecondRatio = 17,
    /// <summary>Optional realized-volatility percentile.</summary>
    RealizedVolatilityPercentile = 18,
    /// <summary>Prior warm volatility composite.</summary>
    PriorVolatilityComposite = 19,
    /// <summary>Bollinger width.</summary>
    BollingerWidth = 20,
    /// <summary>Bollinger width divided by its baseline.</summary>
    BollingerWidthRatio = 21,
    /// <summary>Normalized Bollinger position.</summary>
    BollingerPosition = 22,
    /// <summary>Price minus EMA20 normalized by ATR.</summary>
    Ema20Interaction = 23,
    /// <summary>Observation range normalized by ATR.</summary>
    AtrNormalizedRange = 24,
    /// <summary>Rolling 20-observation high.</summary>
    RollingHigh20 = 25,
    /// <summary>Rolling 20-observation low.</summary>
    RollingLow20 = 26,
    /// <summary>Signed breakout distance normalized by ATR.</summary>
    BreakoutDistanceAtr = 27,
    /// <summary>ITI direction represented as -1 or +1.</summary>
    ItiDirection = 28,
    /// <summary>ITI threshold-band progress.</summary>
    ItiBandLevel = 29,
    /// <summary>ITI reversal progress.</summary>
    ItiReversalLevel = 30,
    /// <summary>Optional TDI value.</summary>
    Tdi = 31
}

/// <summary>Identifies why a requested signal metric cannot be consumed.</summary>
public enum RegimeDiscoverySignalAvailability : byte
{
    /// <summary>The metric is available and compatible.</summary>
    Available = 0,
    /// <summary>The metric does not exist in the cache.</summary>
    Missing = 1,
    /// <summary>The metric exceeds its configured maximum age.</summary>
    Stale = 2,
    /// <summary>The metric has not completed warm-up.</summary>
    NotWarm = 3,
    /// <summary>The metric failed upstream validation.</summary>
    Invalid = 4,
    /// <summary>The metric timestamp exceeds permitted future clock skew.</summary>
    FutureTimestamp = 5,
    /// <summary>The metric schema is unsupported.</summary>
    SchemaUnsupported = 6,
    /// <summary>The metric calculation version is incompatible.</summary>
    CalculationVersionMismatch = 7,
    /// <summary>The metric configuration does not match the request.</summary>
    ConfigurationMismatch = 8,
    /// <summary>A stable cache revision could not be captured.</summary>
    SnapshotConsistencyFailure = 9
}

/// <summary>Defines one exact signal metric required by a snapshot request.</summary>
[MessagePackObject]
public sealed record RegimeDiscoverySignalRequirement
{
    /// <summary>Gets the normalized metric.</summary>
    [Key(0)] public RegimeDiscoverySignalMetric Metric { get; init; }
    /// <summary>Gets the observation timeframe.</summary>
    [Key(1)] public TimeFrameType TimeFrame { get; init; }
    /// <summary>Gets whether the metric is required.</summary>
    [Key(2)] public bool IsRequired { get; init; }
    /// <summary>Gets the immutable upstream calculation configuration identity.</summary>
    [Key(3)] public string CalculationConfigurationId { get; init; } = string.Empty;
    /// <summary>Gets the maximum accepted age in seconds.</summary>
    [Key(4)] public int MaximumAgeSeconds { get; init; }
    /// <summary>Gets the configured evidence weight.</summary>
    [Key(5)] public decimal Weight { get; init; }
}

/// <summary>Contains one immutable normalized metric and its complete provenance.</summary>
[MessagePackObject]
public sealed record RegimeDiscoverySignalObservation
{
    /// <summary>Gets the normalized metric.</summary>
    [Key(0)] public RegimeDiscoverySignalMetric Metric { get; init; }
    /// <summary>Gets the source analytics signal key.</summary>
    [Key(1)] public MarketAnalyticsSignalKey SignalKey { get; init; }
    /// <summary>Gets the normalized numeric value.</summary>
    [Key(2)] public decimal Value { get; init; }
    /// <summary>Gets the UTC market-data timestamp.</summary>
    [Key(3)] public DateTime MarketDataAsOfUtc { get; init; }
    /// <summary>Gets the UTC calculation timestamp.</summary>
    [Key(4)] public DateTime CalculatedAtUtc { get; init; }
    /// <summary>Gets the upstream source sequence.</summary>
    [Key(5)] public long SourceSequence { get; init; }
    /// <summary>Gets the serialized upstream schema version.</summary>
    [Key(6)] public ushort SchemaVersion { get; init; }
    /// <summary>Gets the upstream calculation version.</summary>
    [Key(7)] public string CalculationVersion { get; init; } = string.Empty;
    /// <summary>Gets whether upstream warm-up completed.</summary>
    [Key(8)] public bool IsWarm { get; init; }
    /// <summary>Gets whether the upstream value is valid.</summary>
    [Key(9)] public bool IsValid { get; init; }
    /// <summary>Gets the evaluated availability for this request.</summary>
    [Key(10)] public RegimeDiscoverySignalAvailability Availability { get; init; }
    /// <summary>Gets normalized freshness in the range zero through one.</summary>
    [Key(11)] public decimal FreshnessFactor { get; init; }
    /// <summary>Gets the stable upstream signal identity.</summary>
    [Key(12)] public string SignalIdentity { get; init; } = string.Empty;
}

/// <summary>Requests one atomic market-signal snapshot for a workflow horizon.</summary>
[MessagePackObject]
public sealed record RegimeDiscoveryMarketSignalSnapshotRequest
{
    /// <summary>Gets the requested provider-neutral market series.</summary>
    [Key(0)] public MarketSeriesIdentity MarketSeriesIdentity { get; init; }
    /// <summary>Gets the single Daily, Weekly, or Monthly target horizon.</summary>
    [Key(1)] public TimeFrameType TargetHorizon { get; init; }
    /// <summary>Gets exact required and optional metrics.</summary>
    [Key(2)] public RegimeDiscoverySignalRequirement[] Requirements { get; init; } = [];
    /// <summary>Gets the maximum tolerated future clock skew in seconds.</summary>
    [Key(3)] public int FutureClockSkewSeconds { get; init; }
    /// <summary>Gets supported upstream schema versions.</summary>
    [Key(4)] public ushort[] SupportedSchemaVersions { get; init; } = [];
    /// <summary>Gets approved upstream calculation versions.</summary>
    [Key(5)] public string[] ApprovedCalculationVersions { get; init; } = [];
    /// <summary>Gets the bounded stable-revision capture attempt count.</summary>
    [Key(6)] public int CaptureAttempts { get; init; } = 3;
}

/// <summary>Contains one immutable, revision-stable Regime Discovery market snapshot.</summary>
[MessagePackObject]
public sealed record RegimeDiscoveryMarketSignalSnapshot
{
    /// <summary>Gets the unique snapshot identity.</summary>
    [Key(0)] public Guid SnapshotId { get; init; }
    /// <summary>Gets the monotonic cache revision captured atomically.</summary>
    [Key(1)] public long CacheRevision { get; init; }
    /// <summary>Gets the provider-neutral market series.</summary>
    [Key(2)] public MarketSeriesIdentity MarketSeriesIdentity { get; init; }
    /// <summary>Gets the single target workflow horizon.</summary>
    [Key(3)] public TimeFrameType TargetHorizon { get; init; }
    /// <summary>Gets the UTC snapshot capture timestamp.</summary>
    [Key(4)] public DateTime CapturedAtUtc { get; init; }
    /// <summary>Gets the latest included market-data timestamp.</summary>
    [Key(5)] public DateTime MarketDataAsOfUtc { get; init; }
    /// <summary>Gets accepted and unavailable observations in deterministic request order.</summary>
    [Key(6)] public RegimeDiscoverySignalObservation[] Observations { get; init; } = [];
}

/// <summary>Returns either one immutable snapshot or explicit capture issues.</summary>
[MessagePackObject]
public sealed record RegimeDiscoveryMarketSignalSnapshotResult
{
    /// <summary>Gets whether every required observation is available.</summary>
    [Key(0)] public bool IsSuccess { get; init; }
    /// <summary>Gets the snapshot when capture succeeded.</summary>
    [Key(1)] public RegimeDiscoveryMarketSignalSnapshot? Snapshot { get; init; }
    /// <summary>Gets unavailable required and optional observations.</summary>
    [Key(2)] public RegimeDiscoverySignalObservation[] Issues { get; init; } = [];
}

/// <summary>Captures one immutable, provider-neutral Regime Discovery market-signal snapshot.</summary>
public interface IRegimeDiscoveryMarketSignalSnapshotProvider
{
    /// <summary>Captures a revision-stable snapshot for the supplied exact requirements.</summary>
    /// <param name="request">Exact snapshot request.</param>
    /// <param name="cancellationToken">Signals cancellation.</param>
    /// <returns>A successful snapshot or explicit availability issues.</returns>
    ValueTask<RegimeDiscoveryMarketSignalSnapshotResult> CaptureAsync(
        RegimeDiscoveryMarketSignalSnapshotRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>Accepts immutable normalized analytics observations into the latest-signal cache.</summary>
public interface IRegimeDiscoveryMarketSignalCache
{
    /// <summary>Gets the latest monotonic cache revision.</summary>
    long Revision { get; }
    /// <summary>Publishes one immutable normalized observation and advances the cache revision.</summary>
    void Upsert(RegimeDiscoverySignalObservation observation);
    /// <summary>Clears process-local observations during controlled restart or tests.</summary>
    void Clear();
}

/// <summary>Validates an exact Regime Discovery snapshot request.</summary>
public sealed class RegimeDiscoveryMarketSignalSnapshotRequestValidationRules
    : BaseValidationRules, IValidationRules<RegimeDiscoveryMarketSignalSnapshotRequest>
{
    static readonly Validator Rules = new();

    /// <summary>Validates the supplied request.</summary>
    /// <param name="value">Request to validate.</param>
    /// <returns>All validation errors, or an empty array when valid.</returns>
    public ValidationError[] Execute(RegimeDiscoveryMarketSignalSnapshotRequest value) => Validate(value, Rules);

    sealed class Validator : AbstractValidator<RegimeDiscoveryMarketSignalSnapshotRequest>
    {
        public Validator()
        {
            RuleFor(x => x.MarketSeriesIdentity)
                .Must(x => new MarketSeriesIdentityValidationRules().Execute(x).Length == 0);
            RuleFor(x => x.TargetHorizon).Must(static value =>
                value is TimeFrameType.Daily or TimeFrameType.Weekly or TimeFrameType.Monthly);
            RuleFor(x => x.Requirements).NotEmpty();
            RuleForEach(x => x.Requirements).ChildRules(requirement =>
            {
                requirement.RuleFor(x => x.Metric).IsInEnum().NotEqual(RegimeDiscoverySignalMetric.Unknown);
                requirement.RuleFor(x => x.TimeFrame).IsInEnum().NotEqual(TimeFrameType.None);
                requirement.RuleFor(x => x.CalculationConfigurationId).NotEmpty();
                requirement.RuleFor(x => x.MaximumAgeSeconds).GreaterThan(0);
                requirement.RuleFor(x => x.Weight).GreaterThanOrEqualTo(0m);
            });
            RuleFor(x => x.FutureClockSkewSeconds).GreaterThanOrEqualTo(0);
            RuleFor(x => x.SupportedSchemaVersions).NotEmpty();
            RuleFor(x => x.ApprovedCalculationVersions).NotEmpty();
            RuleFor(x => x.CaptureAttempts).InclusiveBetween(1, 10);
        }
    }
}
