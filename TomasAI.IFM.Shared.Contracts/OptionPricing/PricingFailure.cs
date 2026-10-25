using System.Collections.Immutable;

namespace TomasAI.IFM.Shared.OptionPricing;
public enum PricingFailure
{
    None, InvalidInput, UnsupportedConvention, Expired, UndefinedGreeks,
    PriceOutOfBounds, ImpliedVolatilityNotIdentifiable, VolatilityNotBracketed,
    NonConvergence, NumericalFailure
}
