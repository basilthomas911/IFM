using MessagePack;
using System.Collections.Immutable;
namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.Pricing;

[MessagePackObject]
public sealed record CompositionInstrumentSnapshot(
    [property: Key(0)] CompositionMarketInstrument Instrument,
    [property: Key(1)] OptionPricingValue? Valuation);
