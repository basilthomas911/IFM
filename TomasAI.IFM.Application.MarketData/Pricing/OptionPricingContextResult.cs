using TomasAI.IFM.Shared.MarketData.Contracts;
using TomasAI.IFM.Shared.MarketData.Contracts.Pricing;
using TomasAI.IFM.Shared.MarketData.Pricing;

namespace TomasAI.IFM.Application.MarketData.Pricing;

public sealed record OptionPricingContextResult(OptionPricingContext? Context, OptionPricingFailure? Failure);
