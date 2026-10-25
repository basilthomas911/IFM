using System.Collections.Immutable;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

public enum VolatilitySeriesConstructionFailure
{
    None = 0,
    NoQualifiedInputs = 1,
    MissingTenorBracket = 2,
    MissingOptionSide = 3,
    ExactUnderlyingMismatch = 4,
    IncompatibleConvention = 5,
    IncompatibleSource = 6,
    StaleInput = 7,
    InvalidImpliedVolatility = 8,
    InvalidDefinition = 9
}
