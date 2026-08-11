namespace TomasAI.IFM.Framework.MarketData.Contracts.LastPrice;

/// <summary>
/// Identifies the option-price observation used to calculate implied
/// volatility and Black-76 Greeks.
/// </summary>
public enum OptionGreeksPriceSource
{
    None = 0,
    QuoteMidpoint = 1
}

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
    NonFiniteResult = 13
}

/// <summary>
/// An immutable option valuation calculated from one option-price observation,
/// one underlying futures-price observation, and one session risk-free rate.
/// </summary>
/// <remarks>
/// Availability and validity are separate. A reader may return an available
/// snapshot whose <see cref="IsValid"/> value is <see langword="false"/> so the
/// caller can inspect <see cref="FailureReason"/> without a second call.
/// Missing or failed numeric inputs remain null and are never represented by
/// zero sentinels.
/// </remarks>
public readonly record struct OptionGreeksSnapshot(
    bool IsValid,
    bool IsStale,
    OptionGreeksFailureReason FailureReason,
    OptionGreeksPriceSource PriceSource,
    string FuturesContractId,
    decimal? FuturesPrice,
    decimal? OptionMarkPrice,
    double? RiskFreeRate,
    double? TimeToExpiryYears,
    double? ImpliedVolatility,
    double? TheoreticalPrice,
    double? Delta,
    double? Gamma,
    double? Vega,
    double? Theta,
    double? Rho,
    int SolverIterations,
    long FuturesPriceSourceSequence,
    long OptionPriceSourceSequence,
    DateTimeOffset FuturesPriceTimestamp,
    DateTimeOffset OptionPriceTimestamp,
    DateTimeOffset CalculatedAtUtc);

/// <summary>
/// Atomically couples the latest option quote with the calculation produced
/// for that exact quote observation.
/// </summary>
public readonly record struct LastQuoteTickWithGreeksSnapshot(
    LastQuoteTickSnapshot Tick,
    OptionGreeksSnapshot Greeks);

/// <summary>
/// Atomically couples the latest option trade with the most recent
/// quote-derived Greeks state available when that trade was processed.
/// </summary>
public readonly record struct LastTradeTickWithGreeksSnapshot(
    LastTradeTickSnapshot Tick,
    OptionGreeksSnapshot Greeks);
