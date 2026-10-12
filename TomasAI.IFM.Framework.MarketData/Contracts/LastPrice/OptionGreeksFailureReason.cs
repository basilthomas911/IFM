namespace TomasAI.IFM.Framework.MarketData.Contracts.LastPrice;

/// <summary>
/// Explains why an available Greeks calculation is not valid.
/// </summary>
public enum OptionGreeksFailureReason
{
    None = 0,
    NoValidQuote = 1,
    MissingFuturesPrice = 2,
    StaleFuturesPrice = 3,
    InvalidFuturesPrice = 4,
    MissingOptionPrice = 5,
    StaleOptionPrice = 6,
    InvalidOptionPrice = 7,
    InvalidContract = 8,
    InvalidMaturity = 9,
    InvalidRiskFreeRate = 10,
    NoArbitrageViolation = 11,
    SolverDidNotConverge = 12,
    NonFiniteResult = 13,
    PricingContextUnavailable = 14
}
