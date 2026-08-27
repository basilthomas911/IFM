using FluentValidation;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVxTermStructureSignal;

/// <summary>Classifies the shape of the first two eligible VX futures contracts.</summary>
public enum FuturesVxTermStructureState : byte
{
    /// <summary>The curve cannot yet be classified.</summary>
    Unknown = 0,
    /// <summary>The back contract trades above the front contract.</summary>
    Contango = 1,
    /// <summary>The two contracts trade within the configured flat tolerance.</summary>
    Flat = 2,
    /// <summary>The front contract trades above the back contract.</summary>
    Backwardation = 3
}

/// <summary>Identifies which VX curve leg supplied a price observation.</summary>
public enum FuturesVxTermStructureLeg : byte
{
    /// <summary>No leg was identified.</summary>
    Unknown = 0,
    /// <summary>The first eligible VX expiry.</summary>
    Front = 1,
    /// <summary>The immediately following VX expiry.</summary>
    Back = 2
}

/// <summary>Defines versioned calculation and freshness rules for a VX curve.</summary>
[MessagePackObject]
public sealed record FuturesVxTermStructureConfiguration
{
    /// <summary>Gets the default configuration.</summary>
    public static FuturesVxTermStructureConfiguration Standard { get; } = new();
    /// <summary>Gets the stable calculation configuration identity.</summary>
    [Key(0)] public string ConfigurationId { get; init; } = "vx-front-back-v1";
    /// <summary>Gets the absolute term-structure percentage treated as flat.</summary>
    [Key(1)] public decimal FlatEpsilon { get; init; } = 0.0005m;
    /// <summary>Gets the maximum permitted source timestamp difference.</summary>
    [Key(2)] public TimeSpan MaximumSourceSkew { get; init; } = TimeSpan.FromSeconds(5);
}

/// <summary>Identifies one rollover-compatible VX front/back event stream.</summary>
[MessagePackObject]
public readonly record struct FuturesVxTermStructureSignalEntityId : IActorEntityId
{
    /// <summary>Gets the applicable futures trading date.</summary>
    [Key(0)] public DateOnly ValueDate { get; init; }
    /// <summary>Gets the front VX contract ID.</summary>
    [Key(1)] public string FrontContractId { get; init; }
    /// <summary>Gets the back VX contract ID.</summary>
    [Key(2)] public string BackContractId { get; init; }
    /// <summary>Gets the calculation configuration ID.</summary>
    [Key(3)] public string ConfigurationId { get; init; }

    /// <summary>Initializes a rollover-compatible VX curve identity.</summary>
    [SerializationConstructor]
    public FuturesVxTermStructureSignalEntityId(
        DateOnly valueDate,
        string frontContractId,
        string backContractId,
        string configurationId)
    {
        ValueDate = valueDate;
        FrontContractId = frontContractId;
        BackContractId = backContractId;
        ConfigurationId = configurationId;
    }

    /// <summary>Formats the actor routing identity.</summary>
    public string Format() => string.Join('|', ValueDate.ToString("yyyyMMdd"),
        Uri.EscapeDataString(FrontContractId), Uri.EscapeDataString(BackContractId),
        Uri.EscapeDataString(ConfigurationId));

    /// <summary>Parses a formatted identity.</summary>
    public static FuturesVxTermStructureSignalEntityId Parse(string value) =>
        TryParse(value, out var result) ? result : throw new FormatException("VX term-structure identity is malformed.");

    /// <summary>Attempts to parse a formatted identity.</summary>
    public static bool TryParse(string? value, out FuturesVxTermStructureSignalEntityId result)
    {
        result = default;
        var parts = value?.Split('|');
        if (parts is not { Length: 4 }
            || !DateOnly.TryParseExact(parts[0], "yyyyMMdd", out var date)) return false;
        result = new(date, Uri.UnescapeDataString(parts[1]), Uri.UnescapeDataString(parts[2]),
            Uri.UnescapeDataString(parts[3]));
        return new FuturesVxTermStructureSignalEntityIdValidationRules().Execute(result).Length == 0;
    }

    /// <inheritdoc />
    public override string ToString() => Format();
}

/// <summary>Validates a VX front/back stream identity.</summary>
public sealed class FuturesVxTermStructureSignalEntityIdValidationRules
    : BaseValidationRules, IValidationStructRules<FuturesVxTermStructureSignalEntityId>
{
    static readonly Validator Rules = new();
    /// <inheritdoc />
    public ValidationError[] Execute(FuturesVxTermStructureSignalEntityId value) => Validate(value, Rules);
    sealed class Validator : AbstractValidator<FuturesVxTermStructureSignalEntityId>
    {
        public Validator()
        {
            RuleFor(x => x.ValueDate).NotEqual(default(DateOnly));
            RuleFor(x => x.FrontContractId).NotEmpty();
            RuleFor(x => x.BackContractId).NotEmpty().NotEqual(x => x.FrontContractId);
            RuleFor(x => x.ConfigurationId).NotEmpty();
        }
    }
}

/// <summary>Contains one immutable provider-neutral VX leg observation.</summary>
[MessagePackObject]
public sealed record FuturesVxTermStructureLegObservation
{
    /// <summary>Gets the observed curve leg.</summary>
    [Key(0)] public FuturesVxTermStructureLeg Leg { get; init; }
    /// <summary>Gets the canonical domain contract ID.</summary>
    [Key(1)] public string ContractId { get; init; } = string.Empty;
    /// <summary>Gets the contract expiry.</summary>
    [Key(2)] public DateOnly Expiry { get; init; }
    /// <summary>Gets the positive trade price.</summary>
    [Key(3)] public decimal Price { get; init; }
    /// <summary>Gets the provider-neutral source sequence.</summary>
    [Key(4)] public long SourceSequence { get; init; }
    /// <summary>Gets the source event timestamp.</summary>
    [Key(5)] public DateTimeOffset SourceTimestampUtc { get; init; }
    /// <summary>Gets the market-data stream epoch.</summary>
    [Key(6)] public Guid StreamEpochId { get; init; }
}

/// <summary>Contains replayable paired-leg state owned by the Command actor.</summary>
[MessagePackObject]
public sealed record FuturesVxTermStructureCheckpoint
{
    /// <summary>Gets the latest accepted front observation.</summary>
    [Key(0)] public FuturesVxTermStructureLegObservation? Front { get; init; }
    /// <summary>Gets the latest accepted back observation.</summary>
    [Key(1)] public FuturesVxTermStructureLegObservation? Back { get; init; }
    /// <summary>Gets the most recently emitted front/back ratio.</summary>
    [Key(2)] public decimal? PreviousFrontBackRatio { get; init; }
    /// <summary>Gets the most recently emitted term-structure percentage.</summary>
    [Key(3)] public decimal? PreviousTermStructurePercent { get; init; }
}

/// <summary>Represents a calculated front/back VX futures term structure.</summary>
[MessagePackObject]
public sealed record FuturesVxTermStructureSignalReadModel
{
    [Key(0)] public DateOnly ValueDate { get; init; }
    [Key(1)] public string ConfigurationId { get; init; } = string.Empty;
    [Key(2)] public string FrontVxContractId { get; init; } = string.Empty;
    [Key(3)] public DateOnly FrontExpiry { get; init; }
    [Key(4)] public decimal FrontVxPrice { get; init; }
    [Key(5)] public string BackVxContractId { get; init; } = string.Empty;
    [Key(6)] public DateOnly BackExpiry { get; init; }
    [Key(7)] public decimal BackVxPrice { get; init; }
    [Key(8)] public decimal FrontBackSpread { get; init; }
    [Key(9)] public decimal FrontBackRatio { get; init; }
    [Key(10)] public decimal TermStructurePercent { get; init; }
    [Key(11)] public FuturesVxTermStructureState TermStructureState { get; init; }
    [Key(12)] public decimal? PriorFrontBackRatio { get; init; }
    [Key(13)] public decimal? PriorTermStructurePercent { get; init; }
    [Key(14)] public DateTimeOffset FrontSourceTimestampUtc { get; init; }
    [Key(15)] public DateTimeOffset BackSourceTimestampUtc { get; init; }
    [Key(16)] public long FrontSourceSequence { get; init; }
    [Key(17)] public long BackSourceSequence { get; init; }
    [Key(18)] public DateTimeOffset CalculatedAtUtc { get; init; }
    [Key(19)] public bool IsWarm { get; init; }
    [Key(20)] public bool IsValid { get; init; }
    [Key(21)] public int SchemaVersion { get; init; } = 1;
    [Key(22)] public string CalculationVersion { get; init; } = "vx-term-structure-v1";
}
