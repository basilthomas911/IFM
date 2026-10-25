using System.Collections.Immutable;
using MessagePack;

namespace TomasAI.IFM.Shared.MarketData.Contracts.Pricing;

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
    // Schema 3 pins the economic inputs that older ES-only mappings obtained from their reviewed profile.
    [Key(25)]
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public OptionPremiumStyle PremiumStyle { get; init; }
    [Key(26)]
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public PricingUnderlyingKind UnderlyingKind { get; init; }
    [Key(27)]
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public decimal? Strike { get; init; }
    [Key(28)]
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public PricingOptionRight Right { get; init; }
}
