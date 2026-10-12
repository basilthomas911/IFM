using System.Collections.Immutable;
using MessagePack;
using TomasAI.IFM.Framework.MarketData.Contracts.Pricing;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Application.MarketData.Pricing;

[MessagePackObject]
public sealed record CompositionInstrumentSnapshot(
    [property: Key(0)] CompositionMarketInstrument Instrument,
    [property: Key(1)] OptionPricingValue? Valuation);
