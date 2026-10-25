using MessagePack;
using TomasAI.IFM.Shared.MarketData.Contracts.Pricing;
using TomasAI.IFM.Shared.MarketData.Pricing;
using TomasAI.IFM.Framework.MarketData.ReferenceData;
using Unified = TomasAI.IFM.Framework.OptionPricer.Pricing;

namespace TomasAI.IFM.Application.MarketData.Pricing;

[MessagePackObject]
public sealed record OptionPricingPassResult(
    [property: Key(0)] OptionPricingValue? Value,
    [property: Key(1)] OptionPricingFailure? Failure);
