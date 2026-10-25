using System.Collections.Immutable;
using MessagePack;
using TomasAI.IFM.Shared.MarketData.Contracts.Pricing;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Shared.MarketData.Pricing;

namespace TomasAI.IFM.Application.MarketData.Pricing;

[MessagePackObject]
public sealed record QualifiedOptionDefinition(
    [property: Key(0)] OptionPricingConvention Convention,
    [property: Key(1)] decimal Strike,
    [property: Key(2)] bool IsCall);
