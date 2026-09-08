using System.Collections.Immutable;
using MessagePack;

namespace TomasAI.IFM.Framework.MarketData.Contracts.Pricing;

public enum OptionExerciseStyle { Unknown = 0, European = 1, American = 2 }
public enum OptionSettlementStyle { Unknown = 0, DeliveryOfFuture = 1, Cash = 2 }
public enum PricingDayCount { Unknown = 0, Actual365Fixed = 1, Actual360 = 2 }
public enum OptionPremiumTickRule { Unspecified = 0, Fixed = 1, CmeEsGlobex358A = 2 }

/// <summary>Safe diagnostic data; failed pricing never supplies usable numeric output.</summary>
[MessagePackObject]
public sealed record OptionPricingFailure(
    [property: Key(0)] string Code,
    [property: Key(1)] string Input,
    [property: Key(2)] string ContractId,
    [property: Key(3)] string Detail,
    [property: Key(4)] bool Retryable = false);

/// <summary>An exact reviewed contract mapping, kept separate from raw provider definitions.</summary>
[MessagePackObject]
public sealed record OptionPricingConvention
{
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public required string ContractId { get; init; }
    [Key(2)] public required string Dataset { get; init; }
    [Key(3)] public required ushort PublisherId { get; init; }
    [Key(4)] public required uint InstrumentId { get; init; }
    [Key(5)] public required string RawSymbol { get; init; }
    [Key(6)] public required string Root { get; init; }
    [Key(7)] public required string Exchange { get; init; }
    [Key(8)] public required string Currency { get; init; }
    [Key(9)] public required string UnderlyingContractId { get; init; }
    [Key(10)] public required OptionExerciseStyle ExerciseStyle { get; init; }
    [Key(11)] public required OptionSettlementStyle SettlementStyle { get; init; }
    [Key(12)] public required DateTimeOffset ExpirationUtc { get; init; }
    [Key(13)] public required DateTimeOffset LastTradingUtc { get; init; }
    [Key(14)] public required PricingDayCount DayCount { get; init; }
    [Key(15)] public required string CalendarVersion { get; init; }
    [Key(16)] public required decimal Multiplier { get; init; }
    [Key(17)] public required decimal TickSize { get; init; }
    [Key(18)] public required string TickRuleVersion { get; init; }
    [Key(19)] public required string DefinitionDigest { get; init; }
    [Key(20)] public required string MappingVersion { get; init; }
    [Key(21)] public required string EvidenceId { get; init; }
    [Key(22)] public required DateTimeOffset EffectiveFromUtc { get; init; }
    [Key(23)] public required DateTimeOffset EffectiveUntilUtc { get; init; }
    /// <summary>Append-only schema 2 rule. TickSize is the minimum increment when the rule has premium bands.</summary>
    [Key(24)]
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public OptionPremiumTickRule PremiumTickRule { get; init; }
}

/// <summary>Explicit complete calendar coverage, including exchange value dates and pricing convention.</summary>
[MessagePackObject]
public sealed record OptionPricingCalendar(
    [property: Key(0)] string Version,
    [property: Key(1)] string TimeZoneId,
    [property: Key(2)] DateOnly CoverageFrom,
    [property: Key(3)] DateOnly CoverageUntil,
    [property: Key(4)] TimeOnly ValueDateRollover,
    [property: Key(5)] ImmutableArray<DateOnly> TradingDates);

/// <summary>Immutable source-time quote. A generation change fences all preceding readiness.</summary>
[MessagePackObject]
public sealed record OptionPricingQuote(
    [property: Key(0)] string ContractId,
    [property: Key(1)] decimal Bid,
    [property: Key(2)] decimal Ask,
    [property: Key(3)] decimal BidSize,
    [property: Key(4)] decimal AskSize,
    [property: Key(5)] DateTimeOffset EventAtUtc,
    [property: Key(6)] DateTimeOffset ReceivedAtUtc,
    [property: Key(7)] long Sequence,
    [property: Key(8)] Guid GenerationId);

/// <summary>Exact reference context prepared away from the tick path. T is computed for every pass.</summary>
[MessagePackObject]
public sealed record OptionPricingContext(
    [property: Key(0)] OptionPricingConvention Contract,
    [property: Key(1)] OptionPricingCalendar Calendar,
    [property: Key(2)] TreasuryContinuousRate Rate,
    [property: Key(3)] DateTimeOffset ValidUntilUtc,
    [property: Key(4)] Guid GenerationId,
    [property: Key(5)] string PricerVersion,
    [property: Key(6)] int MaximumQuoteAgeMilliseconds,
    [property: Key(7)] int MaximumQuoteSkewMilliseconds,
    [property: Key(8)] string PublicationPolicyVersion);

/// <summary>A read must return the exact immutable reviewed mapping version; there is no latest fallback.</summary>
public interface IOptionPricingConventionStore
{
    Task<OptionPricingConvention?> GetAsync(string contractId, string mappingVersion, CancellationToken cancellationToken);
}
