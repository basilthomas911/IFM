using System.Collections.Immutable;
using MessagePack;
using TomasAI.IFM.Shared.MarketData.Contracts.Pricing;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Application.MarketData.Pricing;

[MessagePackObject]
public sealed record CompositionMarketInstrument(
    [property: Key(0)] string ContractId,
    [property: Key(1)] OptionPricingQuote? Quote,
    [property: Key(2)] OptionPricingContext? Pricing,
    [property: Key(3)] decimal? Strike,
    [property: Key(4)] bool? IsCall,
    [property: Key(5)] OptionPricingQuote? Underlying,
    [property: Key(6)] CompositionFutureDefinition? FutureDefinition = null)
{
    [Key(7)]
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public OptionSelectionValue? Selection { get; init; }
    [Key(8)] public long? SessionVolume { get; init; }
    [Key(9)] public long? OpenInterest { get; init; }
    [Key(10)] public DateTimeOffset? StatisticsAtUtc { get; init; }
    /// <summary>Diagnostic reason for suppressing an unusable browsing quote; never authorizes execution.</summary>
    [Key(11)] public string? QuoteUnavailableReason { get; init; }
    [Key(12)] public DateTimeOffset? LastQuoteEventAtUtc { get; init; }
    [Key(13)] public DateTimeOffset? LastQuoteReceivedAtUtc { get; init; }
    [Key(14)] public DateOnly? VolumeValueDate { get; init; }
    [Key(15)] public DateOnly? OpenInterestValueDate { get; init; }
}
