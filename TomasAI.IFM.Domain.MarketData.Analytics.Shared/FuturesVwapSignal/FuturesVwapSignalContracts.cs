using FluentValidation;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVwapSignal;

/// <summary>Describes the lifecycle action of a VWAP trade observation.</summary>
public enum FuturesVwapTradeAction : byte
{
    /// <summary>The source action was absent or unsupported.</summary>
    Unknown = 0,
    /// <summary>A new executed trade was reported.</summary>
    New = 1,
    /// <summary>An existing trade was changed.</summary>
    Change = 2,
    /// <summary>An existing trade was cancelled.</summary>
    Cancel = 3,
    /// <summary>An existing trade was corrected.</summary>
    Correct = 4,
    /// <summary>The source cleared its trade state.</summary>
    Clear = 5,
    /// <summary>The source supplied no action.</summary>
    None = 6
}

/// <summary>Contains feed-neutral conditions relevant to exact VWAP eligibility.</summary>
[Flags]
public enum FuturesVwapTradeConditionFlags : ushort
{
    /// <summary>No condition was reported.</summary>
    None = 0,
    /// <summary>The source marked the trade as a snapshot value.</summary>
    Snapshot = 1 << 0,
    /// <summary>The source marked the price as undefined.</summary>
    UndefinedPrice = 1 << 1,
    /// <summary>The source marked the observation as historical replay.</summary>
    Replay = 1 << 2
}

/// <summary>Identifies why an exact futures-session VWAP is unavailable.</summary>
public enum FuturesVwapInvalidReason : byte
{
    /// <summary>No invalid condition exists.</summary>
    None = 0,
    /// <summary>One or more delivered trade ordinals were missing.</summary>
    DeliveryGap = 1,
    /// <summary>The market-data stream epoch changed during the session.</summary>
    StreamEpochChanged = 2,
    /// <summary>A trade correction could not be correlated deterministically.</summary>
    UncorrelatableCorrection = 3,
    /// <summary>The trade input was structurally invalid.</summary>
    InvalidTrade = 4,
    /// <summary>An exact historical recovery is still incomplete.</summary>
    RecoveryIncomplete = 5
}

/// <summary>Identifies how the persisted VWAP was calculated.</summary>
public enum FuturesVwapCalculationMethod : byte
{
    /// <summary>No calculation method is available.</summary>
    Unknown = 0,
    /// <summary>Every eligible executed trade contributed its exact price and size.</summary>
    TickExact = 1
}

/// <summary>Defines versioned eligibility and current-contract routing for session VWAP.</summary>
[MessagePackObject]
public sealed record FuturesVwapConfiguration
{
    /// <summary>Gets the standard ES futures-session configuration.</summary>
    public static FuturesVwapConfiguration Standard { get; } = new();
    /// <summary>Gets the stable configuration identity.</summary>
    [Key(0)] public string ConfigurationId { get; init; } = "futures-vwap-exact-v1";
    /// <summary>Gets the futures root whose current contract is observed.</summary>
    [Key(1)] public string RootSymbol { get; init; } = "ES";
}

/// <summary>Identifies one exact futures-contract trading session.</summary>
[MessagePackObject]
public readonly record struct FuturesVwapSignalEntityId : IActorEntityId
{
    /// <summary>Gets the actual futures contract identity.</summary>
    [Key(0)] public string ContractId { get; init; }
    /// <summary>Gets the futures session value date.</summary>
    [Key(1)] public DateOnly ValueDate { get; init; }
    /// <summary>Gets the calculation configuration identity.</summary>
    [Key(2)] public string ConfigurationId { get; init; }

    /// <summary>Initializes a session VWAP identity.</summary>
    [SerializationConstructor]
    public FuturesVwapSignalEntityId(string contractId, DateOnly valueDate, string configurationId)
    {
        ContractId = contractId;
        ValueDate = valueDate;
        ConfigurationId = configurationId;
    }

    /// <summary>Formats the canonical actor routing identity.</summary>
    public string Format() => string.Join('|', Uri.EscapeDataString(ContractId),
        ValueDate.ToString("yyyyMMdd"), Uri.EscapeDataString(ConfigurationId));

    /// <summary>Parses a canonical session VWAP identity.</summary>
    public static FuturesVwapSignalEntityId Parse(string value) => TryParse(value, out var result)
        ? result : throw new FormatException("Futures VWAP identity is malformed.");

    /// <summary>Attempts to parse a canonical session VWAP identity.</summary>
    public static bool TryParse(string? value, out FuturesVwapSignalEntityId result)
    {
        result = default;
        var parts = value?.Split('|');
        if (parts is not { Length: 3 }
            || !DateOnly.TryParseExact(parts[1], "yyyyMMdd", out var date)) return false;
        result = new(Uri.UnescapeDataString(parts[0]), date, Uri.UnescapeDataString(parts[2]));
        return new FuturesVwapSignalEntityIdValidationRules().Execute(result).Length == 0;
    }

    /// <inheritdoc />
    public override string ToString() => Format();
}

/// <summary>Validates a futures-session VWAP identity.</summary>
public sealed class FuturesVwapSignalEntityIdValidationRules
    : BaseValidationRules, IValidationStructRules<FuturesVwapSignalEntityId>
{
    static readonly Validator Rules = new();
    /// <inheritdoc />
    public ValidationError[] Execute(FuturesVwapSignalEntityId value) => Validate(value, Rules);

    sealed class Validator : AbstractValidator<FuturesVwapSignalEntityId>
    {
        public Validator()
        {
            RuleFor(value => value.ContractId).NotEmpty();
            RuleFor(value => value.ValueDate).NotEqual(default(DateOnly));
            RuleFor(value => value.ConfigurationId).NotEmpty();
        }
    }
}

/// <summary>Contains one immutable provider-neutral executed-trade observation.</summary>
[MessagePackObject]
public sealed record FuturesVwapTradeObservation
{
    /// <summary>Gets the actual futures contract identity.</summary>
    [Key(0)] public string ContractId { get; init; } = string.Empty;
    /// <summary>Gets the futures session value date.</summary>
    [Key(1)] public DateOnly ValueDate { get; init; }
    /// <summary>Gets the individual executed-trade price.</summary>
    [Key(2)] public decimal Price { get; init; }
    /// <summary>Gets the individual executed-trade size.</summary>
    [Key(3)] public long Size { get; init; }
    /// <summary>Gets the source sequence.</summary>
    [Key(4)] public long SourceSequence { get; init; }
    /// <summary>Gets the exchange event time in UTC.</summary>
    [Key(5)] public DateTimeOffset EventTimestampUtc { get; init; }
    /// <summary>Gets the normalized trade lifecycle action.</summary>
    [Key(6)] public FuturesVwapTradeAction Action { get; init; }
    /// <summary>Gets provider-neutral trade conditions.</summary>
    [Key(7)] public FuturesVwapTradeConditionFlags Conditions { get; init; }
    /// <summary>Gets the live stream epoch.</summary>
    [Key(8)] public Guid StreamEpochId { get; init; }
    /// <summary>Gets the contiguous live trade ordinal.</summary>
    [Key(9)] public long TradeOrdinal { get; init; }
    /// <summary>Gets the futures session start.</summary>
    [Key(10)] public DateTimeOffset SessionStartUtc { get; init; }
    /// <summary>Gets the futures session end.</summary>
    [Key(11)] public DateTimeOffset SessionEndUtc { get; init; }
}

/// <summary>Contains the replayable exact futures-session VWAP accumulator.</summary>
[MessagePackObject]
public sealed record FuturesVwapCheckpoint
{
    /// <summary>Gets the futures session start.</summary>
    [Key(0)] public DateTimeOffset SessionStartUtc { get; init; }
    /// <summary>Gets the futures session end.</summary>
    [Key(1)] public DateTimeOffset SessionEndUtc { get; init; }
    /// <summary>Gets the cumulative price multiplied by individual trade size.</summary>
    [Key(2)] public decimal CumulativePriceVolume { get; init; }
    /// <summary>Gets the cumulative eligible executed volume.</summary>
    [Key(3)] public long CumulativeVolume { get; init; }
    /// <summary>Gets the number of eligible trades included.</summary>
    [Key(4)] public long EligibleTradeCount { get; init; }
    /// <summary>Gets the number of rejected or invalidating trades.</summary>
    [Key(5)] public long RejectedTradeCount { get; init; }
    /// <summary>Gets the latest observed trade price.</summary>
    [Key(6)] public decimal LastPrice { get; init; }
    /// <summary>Gets the last accepted source sequence.</summary>
    [Key(7)] public long LastTradeSourceSequence { get; init; }
    /// <summary>Gets the active stream or replay epoch.</summary>
    [Key(8)] public Guid StreamEpochId { get; init; }
    /// <summary>Gets the last accepted live trade ordinal.</summary>
    [Key(9)] public long LastTradeOrdinal { get; init; }
    /// <summary>Gets whether every eligible session trade is represented.</summary>
    [Key(10)] public bool IsValid { get; init; }
    /// <summary>Gets the current invalid reason.</summary>
    [Key(11)] public FuturesVwapInvalidReason InvalidReason { get; init; }
    /// <summary>Gets whether a private exact recovery is active.</summary>
    [Key(12)] public bool IsRecovering { get; init; }
    /// <summary>Gets the active recovery generation.</summary>
    [Key(13)] public Guid RecoveryGenerationId { get; init; }
    /// <summary>Gets the last accepted recovery batch ordinal.</summary>
    [Key(14)] public long RecoveryBatchOrdinal { get; init; } = -1;
    /// <summary>Gets the last calculation time.</summary>
    [Key(15)] public DateTimeOffset AsOfUtc { get; init; }
}

/// <summary>Represents one exact or explicitly invalid futures-session VWAP projection.</summary>
[MessagePackObject]
public sealed record FuturesVwapSignalReadModel
{
    [Key(0)] public string ContractId { get; init; } = string.Empty;
    [Key(1)] public DateOnly ValueDate { get; init; }
    [Key(2)] public string ConfigurationId { get; init; } = string.Empty;
    [Key(3)] public DateTimeOffset SessionStartUtc { get; init; }
    [Key(4)] public DateTimeOffset SessionEndUtc { get; init; }
    [Key(5)] public DateTimeOffset AsOfUtc { get; init; }
    [Key(6)] public decimal CumulativePriceVolume { get; init; }
    [Key(7)] public long CumulativeVolume { get; init; }
    [Key(8)] public long EligibleTradeCount { get; init; }
    [Key(9)] public long RejectedTradeCount { get; init; }
    [Key(10)] public decimal LastPrice { get; init; }
    [Key(11)] public decimal? Vwap { get; init; }
    [Key(12)] public decimal? PriceMinusVwap { get; init; }
    [Key(13)] public decimal? PriceToVwapPercent { get; init; }
    [Key(14)] public long LastTradeSourceSequence { get; init; }
    [Key(15)] public Guid StreamEpochId { get; init; }
    [Key(16)] public long LastTradeOrdinal { get; init; }
    [Key(17)] public bool IsWarm { get; init; }
    [Key(18)] public bool IsValid { get; init; }
    [Key(19)] public FuturesVwapInvalidReason InvalidReason { get; init; }
    [Key(20)] public bool IsTickExact { get; init; }
    [Key(21)] public FuturesVwapCalculationMethod CalculationMethod { get; init; }
    [Key(22)] public int SchemaVersion { get; init; } = 1;
    [Key(23)] public string CalculationVersion { get; init; } = "futures-vwap-exact-v1";
}
