using System.Collections.Immutable;
using MessagePack;
using TomasAI.IFM.Framework.MarketData.Contracts.Pricing;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Framework.MarketData.Pricing;

namespace TomasAI.IFM.Application.MarketData.Pricing;

public sealed record EuropeanOptionUniverseResult(ImmutableArray<QualifiedOptionDefinition> Definitions,
    ImmutableArray<OptionPricingFailure> Exclusions, OptionPricingFailure? Failure);
