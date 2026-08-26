using FluentValidation;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketSignals.Observation;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketSignals.Common;

/// <summary>Identifies how a market analytics value was calculated.</summary>
public enum MarketSignalCalculationMethod : byte
{
    /// <summary>The calculation method is unknown.</summary>
    Unknown = 0,

    /// <summary>The signal was calculated from exact normalized trades.</summary>
    ExactTrades = 1,

    /// <summary>The signal was calculated from an IFM closed OHLCV observation.</summary>
    ClosedObservation = 2,

    /// <summary>The signal was calculated from a normalized historical provider aggregate.</summary>
    NormalizedHistoricalAggregate = 3
}

/// <summary>Provides stable reason codes for invalid or incomplete market signal inputs.</summary>
public enum MarketSignalValidationIssue : byte
{
    /// <summary>No validation issue was detected.</summary>
    None = 0,

    /// <summary>The source observation is incomplete.</summary>
    IncompleteObservation = 1,

    /// <summary>The OHLC price relationship is invalid.</summary>
    InvalidOhlc = 2,

    /// <summary>Required volume is missing.</summary>
    MissingVolume = 3,

    /// <summary>Required trade lineage is missing.</summary>
    MissingTradeLineage = 4,

    /// <summary>A gap exists in source delivery lineage.</summary>
    SourceGap = 5,

    /// <summary>The market series does not match the requested calculation.</summary>
    SeriesMismatch = 6,

    /// <summary>The observation does not match a composed calculation input.</summary>
    ObservationMismatch = 7,

    /// <summary>The source is too stale for the calculation.</summary>
    StaleSource = 8,

    /// <summary>The calculation produced an invalid result.</summary>
    InvalidCalculation = 9
}

/// <summary>
/// Carries common identity, provenance, configuration, and validity metadata for an analytics signal.
/// </summary>
[MessagePackObject]
public sealed record MarketAnalyticsSignalMetadata
{
    /// <summary>Gets the exact configured signal key.</summary>
    [Key(0)] public MarketAnalyticsSignalKey SignalKey { get; init; }

    /// <summary>Gets the actual source contract used by this calculation.</summary>
    [Key(1)] public string ContractId { get; init; } = string.Empty;

    /// <summary>Gets the futures trading value date.</summary>
    [Key(2)] public DateOnly ValueDate { get; init; }

    /// <summary>Gets the source observation identity.</summary>
    [Key(3)] public FuturesAnalyticsObservationId ObservationId { get; init; }

    /// <summary>Gets the last exchange event included in the calculation.</summary>
    [Key(4)] public DateTimeOffset MarketDataAsOfUtc { get; init; }

    /// <summary>Gets the UTC time at which calculation completed.</summary>
    [Key(5)] public DateTimeOffset CalculatedAtUtc { get; init; }

    /// <summary>Gets the last accepted source sequence included in the calculation.</summary>
    [Key(6)] public long SourceSequence { get; init; }

    /// <summary>Gets the serialized signal schema version.</summary>
    [Key(7)] public ushort SchemaVersion { get; init; }

    /// <summary>Gets the calculation implementation version.</summary>
    [Key(8)] public string CalculationVersion { get; init; } = string.Empty;

    /// <summary>Gets the method used to calculate the signal.</summary>
    [Key(9)] public MarketSignalCalculationMethod CalculationMethod { get; init; }

    /// <summary>Gets whether the persisted signal value passed formula validation.</summary>
    [Key(10)] public bool IsValid { get; init; }

    /// <summary>Gets stable issues explaining an invalid signal.</summary>
    [Key(11)] public MarketSignalValidationIssue[] ValidationIssues { get; init; } = [];

    /// <summary>Gets the exact provider-neutral market series from <see cref="SignalKey"/>.</summary>
    [IgnoreMember]
    public MarketSeriesIdentity MarketSeriesIdentity => SignalKey.MarketSeriesIdentity;

    /// <summary>Gets the roll-aware continuation identity when this is a continuation signal.</summary>
    [IgnoreMember]
    public FuturesSeriesId? FuturesSeriesId => SignalKey.MarketSeriesIdentity.FuturesSeriesId;

    /// <summary>Gets the signal timeframe from <see cref="SignalKey"/>.</summary>
    [IgnoreMember]
    public TimeFrameType TimeFrame => SignalKey.TimeFrame;

    /// <summary>Gets the immutable calculation-configuration identity from <see cref="SignalKey"/>.</summary>
    [IgnoreMember]
    public string CalculationConfigurationId => SignalKey.CalculationConfigurationId;
}

/// <summary>Validates common analytics signal metadata.</summary>
public sealed class MarketAnalyticsSignalMetadataValidationRules
    : BaseValidationRules, IValidationRules<MarketAnalyticsSignalMetadata>
{
    static readonly Validator Rules = new();

    /// <summary>Validates the supplied signal metadata.</summary>
    /// <param name="value">Metadata to validate.</param>
    /// <returns>All validation errors, or an empty array when valid.</returns>
    public ValidationError[] Execute(MarketAnalyticsSignalMetadata value) => Validate(value, Rules);

    sealed class Validator : AbstractValidator<MarketAnalyticsSignalMetadata>
    {
        public Validator()
        {
            RuleFor(x => x.SignalKey)
                .Must(x => new MarketAnalyticsSignalKeyValidationRules().Execute(x).Length == 0);
            RuleFor(x => x.ContractId).NotEmpty();
            RuleFor(x => x.ValueDate).Must(x => x != DateOnly.MinValue && x != DateOnly.MaxValue);
            RuleFor(x => x.ObservationId.Value).NotEmpty();
            RuleFor(x => x.MarketDataAsOfUtc).Must(IsUtc);
            RuleFor(x => x.CalculatedAtUtc).Must(IsUtc);
            RuleFor(x => x).Must(x => x.CalculatedAtUtc >= x.MarketDataAsOfUtc)
                .WithMessage("CalculatedAtUtc must not precede MarketDataAsOfUtc.");
            RuleFor(x => x.SourceSequence).GreaterThanOrEqualTo(0);
            RuleFor(x => x.SchemaVersion).GreaterThan((ushort)0);
            RuleFor(x => x.CalculationVersion).NotEmpty();
            RuleFor(x => x.CalculationMethod).IsInEnum().NotEqual(MarketSignalCalculationMethod.Unknown);
            RuleFor(x => x.ValidationIssues).NotNull();
            RuleFor(x => x).Must(x => !x.IsValid || x.ValidationIssues.Length == 0)
                .WithMessage("A valid signal cannot contain validation issues.");
        }

        static bool IsUtc(DateTimeOffset value) => value.Offset == TimeSpan.Zero;
    }
}
