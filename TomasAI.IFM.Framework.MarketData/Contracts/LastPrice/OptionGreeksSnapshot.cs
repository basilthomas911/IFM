namespace TomasAI.IFM.Framework.MarketData.Contracts.LastPrice;

/// <summary>
/// Identifies the option-price observation used to calculate implied
/// volatility and Black-76 Greeks.
/// </summary>
public enum OptionGreeksPriceSource
{
    None = 0,
    QuoteMidpoint = 1,
    Trade = 2
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
    NonFiniteResult = 13,
    PricingContextUnavailable = 14
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
/// <param name="IsValid">The is valid.</param>
/// <param name="IsStale">The is stale.</param>
/// <param name="FailureReason">The failure reason.</param>
/// <param name="PriceSource">The price source.</param>
/// <param name="FuturesContractId">The futures contract id.</param>
/// <param name="FuturesPrice">The futures price.</param>
/// <param name="OptionMarkPrice">The option mark price.</param>
/// <param name="RiskFreeRate">The risk free rate.</param>
/// <param name="TimeToExpiryYears">The time to expiry years.</param>
/// <param name="ImpliedVolatility">The implied volatility.</param>
/// <param name="TheoreticalPrice">The theoretical price.</param>
/// <param name="Delta">The delta.</param>
/// <param name="Gamma">The gamma.</param>
/// <param name="Vega">The vega.</param>
/// <param name="Theta">The theta.</param>
/// <param name="Rho">The rho.</param>
/// <param name="SolverIterations">The solver iterations.</param>
/// <param name="FuturesPriceSourceSequence">The futures price source sequence.</param>
/// <param name="OptionPriceSourceSequence">The option price source sequence.</param>
/// <param name="FuturesPriceTimestamp">The futures price timestamp.</param>
/// <param name="OptionPriceTimestamp">The option price timestamp.</param>
/// <param name="CalculatedAtUtc">The calculated at utc.</param>
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
    DateTimeOffset CalculatedAtUtc)
{
    /// <summary>Structured prerequisite/solver failure; numeric outputs remain absent.</summary>
    public Pricing.OptionPricingFailure? PricingFailure { get; init; }
    /// <summary>Exact immutable reference and quote context used by this calculation.</summary>
    public string? PricingContextDigest { get; init; }
}

/// <summary>
/// Atomically couples the latest option quote with the calculation produced
/// for that exact quote observation.
/// </summary>
/// <param name="Tick">The tick.</param>
/// <param name="Greeks">The calculated option sensitivities, when available.</param>
public readonly record struct LastQuoteTickWithGreeksSnapshot(
    LastQuoteTickSnapshot Tick,
    OptionGreeksSnapshot Greeks);

/// <summary>
/// Atomically couples the latest option trade with the most recent
/// quote-derived Greeks state available when that trade was processed.
/// </summary>
/// <param name="Tick">The tick.</param>
/// <param name="Greeks">The calculated option sensitivities, when available.</param>
public readonly record struct LastTradeTickWithGreeksSnapshot(
    LastTradeTickSnapshot Tick,
    OptionGreeksSnapshot Greeks);
