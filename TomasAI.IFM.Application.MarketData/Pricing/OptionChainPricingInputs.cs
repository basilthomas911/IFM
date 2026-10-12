using System.Collections.Concurrent;
using TomasAI.IFM.Framework.MarketData.Contracts.LastPrice;
using TomasAI.IFM.Framework.MarketData.Contracts.Pricing;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Framework.MarketData.DataBento.OptionChain;

namespace TomasAI.IFM.Application.MarketData.Pricing;

/// <summary>One immutable read of reference context, underlying and last quote for a registered option.</summary>
public sealed record OptionChainPricingInputs(OptionPricingContext Context, OptionPricingQuote Underlying,
    LastQuoteTickSnapshot? OptionQuote = null);
