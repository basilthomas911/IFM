using System.Collections.Immutable;

namespace TomasAI.IFM.Framework.OptionPricer.Pricing;
public enum PricingFailure
{
    None, InvalidInput, UnsupportedConvention, Expired, UndefinedGreeks,
    PriceOutOfBounds, ImpliedVolatilityNotIdentifiable, VolatilityNotBracketed,
    NonConvergence, NumericalFailure
}
