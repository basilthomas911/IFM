using System.Collections.Concurrent;
using TomasAI.IFM.Shared.MarketData.Contracts.LastPrice;
using TomasAI.IFM.Shared.MarketData.Contracts.Pricing;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Framework.MarketData.DataBento.OptionChain;
using TomasAI.IFM.Shared.MarketData.Pricing;
using Unified = TomasAI.IFM.Framework.OptionPricer.Pricing;

namespace TomasAI.IFM.Application.MarketData.Pricing;

public sealed record ChainIvObservation(double Volatility, OptionPricingContext Context, OptionPricingQuote Underlying,
    OptionPricingQuote Option, DateTimeOffset CalculatedAtUtc, string PolicyVersion);
