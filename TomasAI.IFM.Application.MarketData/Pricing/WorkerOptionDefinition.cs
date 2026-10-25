using System.Collections.Immutable;
using MessagePack;

namespace TomasAI.IFM.Application.MarketData.Pricing;

[MessagePackObject]
public sealed record WorkerOptionDefinition(
    [property: Key(0)] Shared.MarketData.Contracts.Pricing.OptionPricingContext Pricing,
    [property: Key(1)] decimal Strike,
    [property: Key(2)] bool IsCall);
