using MessagePack;
using System.Collections.Immutable;
namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.Pricing;

[MessagePackObject]
public sealed record CompositionMarketInstrument(
    [property: Key(0)] string ContractId,
    [property: Key(1)] OptionPricingQuote Quote,
    [property: Key(2)] OptionPricingContext? Pricing,
    [property: Key(3)] decimal? Strike,
    [property: Key(4)] bool? IsCall,
    [property: Key(5)] OptionPricingQuote? Underlying,
    [property: Key(6)] CompositionFutureDefinition? FutureDefinition = null)
{
    [Key(7)]
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public OptionSelectionValue? Selection { get; init; }
}
