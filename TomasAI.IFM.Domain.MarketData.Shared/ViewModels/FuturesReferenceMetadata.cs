using MessagePack;
using System.Globalization;

namespace TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

public enum ReferenceReviewState { Unknown = 0, Draft = 1, Reviewed = 2 }
public enum ReferenceAssetType { Unknown = 0, Futures = 1, Equity = 2 }
public enum ReferenceOptionRight { Unknown = 0, Call = 1, Put = 2 }
public enum ReferenceExerciseStyle { Unknown = 0, European = 1, American = 2 }
public enum ReferenceSettlementStyle { Unknown = 0, DeliveryOfFuture = 1, Cash = 2, Physical = 3 }
public enum ReferencePremiumStyle { Unknown = 0, PremiumPaid = 1, FuturesStyleVariation = 2 }
public enum ReferencePremiumTickRule { Unknown = 0, Fixed = 1, CmeEsGlobex358A = 2 }
public enum ReferenceDayCount { Unknown = 0, Actual365Fixed = 1, Actual360 = 2 }

/// <summary>Append-only provider/review evidence. Missing legacy fields never imply qualification.</summary>
public partial record FuturesContractV3ReadModel
{
    [Key(11)] public int SchemaVersion { get; init; }
    [Key(12)] public ReferenceReviewState ReviewState { get; init; }
    [Key(13)] public string? Dataset { get; init; }
    [Key(14)] public ushort? PublisherId { get; init; }
    [Key(15)] public uint? InstrumentId { get; init; }
    [Key(16)] public string? RawSymbol { get; init; }
    [Key(17)] public DateTimeOffset? DefinitionTimestampUtc { get; init; }
    [Key(18)] public string? DefinitionDigest { get; init; }
    [Key(19)] public string? RawDefinitionReference { get; init; }
    [Key(20)] public DateTimeOffset? ExpirationUtc { get; init; }
    [Key(21)] public DateTimeOffset? LastTradingUtc { get; init; }
    [Key(22)] public string? ExchangeTimeZoneId { get; init; }
    [Key(23)] public ReferenceSettlementStyle SettlementStyle { get; init; }
    [Key(24)] public decimal? MultiplierValue { get; init; }
    [Key(25)] public decimal? PriceScale { get; init; }
    [Key(26)] public decimal? TickSize { get; init; }
    [Key(27)] public string? CalendarVersion { get; init; }
    [Key(28)] public string? MappingVersion { get; init; }
    [Key(29)] public string? EvidenceId { get; init; }
    [Key(30)] public DateTimeOffset? EffectiveFromUtc { get; init; }
    [Key(31)] public DateTimeOffset? EffectiveUntilUtc { get; init; }
}

/// <summary>The key-9 double is retained only for wire compatibility; new reference flows use StrikePriceDecimal.</summary>
public partial record FuturesOptionContractReadModel
{
    [Key(11)] public decimal? StrikePriceDecimal { get; init; }
    [Key(12)] public int SchemaVersion { get; init; }
    [Key(13)] public ReferenceReviewState ReviewState { get; init; }
    [Key(14)] public string? Dataset { get; init; }
    [Key(15)] public ushort? PublisherId { get; init; }
    [Key(16)] public uint? InstrumentId { get; init; }
    [Key(17)] public string? RawSymbol { get; init; }
    [Key(18)] public DateTimeOffset? DefinitionTimestampUtc { get; init; }
    [Key(19)] public string? DefinitionDigest { get; init; }
    [Key(20)] public string? RawDefinitionReference { get; init; }
    [Key(21)] public DateTimeOffset? ExpirationUtc { get; init; }
    [Key(22)] public DateTimeOffset? LastTradingUtc { get; init; }
    [Key(23)] public string? ExchangeTimeZoneId { get; init; }
    [Key(24)] public ReferenceSettlementStyle SettlementStyle { get; init; }
    [Key(25)] public decimal? MultiplierValue { get; init; }
    [Key(26)] public decimal? PriceScale { get; init; }
    [Key(27)] public decimal? TickSize { get; init; }
    [Key(28)] public string? CalendarVersion { get; init; }
    [Key(29)] public string? MappingVersion { get; init; }
    [Key(30)] public string? EvidenceId { get; init; }
    [Key(31)] public DateTimeOffset? EffectiveFromUtc { get; init; }
    [Key(32)] public DateTimeOffset? EffectiveUntilUtc { get; init; }
    [Key(33)] public string? UnderlyingContractId { get; init; }
    [Key(34)] public ReferenceAssetType UnderlyingAssetType { get; init; }
    [Key(35)] public ReferenceOptionRight OptionRight { get; init; }
    [Key(36)] public ReferenceExerciseStyle ExerciseStyle { get; init; }
    [Key(37)] public ReferencePremiumStyle PremiumStyle { get; init; }
    [Key(38)] public DateTimeOffset? ExerciseCutoffUtc { get; init; }
    [Key(39)] public string? ExerciseResultContractId { get; init; }
    [Key(40)] public ReferencePremiumTickRule PremiumTickRule { get; init; }
    [Key(41)] public string? TickRuleVersion { get; init; }
    [Key(42)] public ReferenceDayCount DayCount { get; init; }
    [Key(43)] public uint? UnderlyingInstrumentId { get; init; }
    [Key(44)] public ushort? UnderlyingPublisherId { get; init; }

    /// <summary>Explicit legacy translation; conflicting or invalid representations fail rather than round silently.</summary>
    public decimal GetExactStrikePrice()
    {
        if (!double.IsFinite(StrikePrice) || StrikePrice <= 0) throw new InvalidDataException("Invalid legacy strike.");
        if (StrikePriceDecimal is { } exact)
        {
            if (exact <= 0 || (double)exact != StrikePrice) throw new InvalidDataException("Decimal and legacy strike conflict.");
            return exact;
        }
        if (!decimal.TryParse(StrikePrice.ToString("R", CultureInfo.InvariantCulture),
            NumberStyles.Float, CultureInfo.InvariantCulture, out var translated) || translated <= 0
            || (double)translated != StrikePrice)
            throw new InvalidDataException("Legacy strike cannot be translated without changing its value.");
        return translated;
    }
}
