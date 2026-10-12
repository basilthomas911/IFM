using TomasAI.IFM.Framework.MarketData.Contracts;
using TomasAI.IFM.Framework.MarketData.Contracts.Pricing;
using TomasAI.IFM.Framework.MarketData.Pricing;

namespace TomasAI.IFM.Application.MarketData.Pricing;

public sealed record PricingSourceClockPolicy(int MaximumLeadMilliseconds);
