namespace TomasAI.IFM.Framework.OptionPricer.Black76;

using TomasAI.IFM.Framework.OptionPricer.Interop;

/// <summary>
/// Calculates implied volatility and Greeks for European options on futures using Black-76.
/// </summary>
/// <remarks>
/// The calculator is immutable, thread-safe, and contains no shared mutable state. Time to expiry uses
/// Actual/365 Fixed in the legacy DateOnly constructor; the explicit year-fraction constructor preserves
/// the caller's qualified contract convention. The supplied rate is continuously compounded annual decimal.
/// </remarks>
public readonly struct OptionCalculator
{
    /// <summary>Process-pinned engine identity for pricing provenance and replay compatibility.</summary>
    public static string EngineVersion => OptionPricerBackend.UseRust ? "Black76.Rust/v1" : "Black76.Managed/v1";

    private const double DaysPerYear = 365.0;
    private const double MaximumVolatility = 4.0;
    private const double PriceTolerance = 1e-10;
    private const int MaximumIterations = 100;

    private readonly double _timeToExpiry;

    /// <summary>
    /// Creates a calculator for the supplied valuation and maturity dates.
    /// </summary>
    public OptionCalculator(DateOnly valueDate, DateOnly maturityDate)
    {
        _timeToExpiry = (maturityDate.DayNumber - valueDate.DayNumber) / DaysPerYear;
    }

    /// <summary>Creates a calculator with a contract-qualified fractional time to expiry.</summary>
    /// <param name="timeToExpiry">Positive finite years under the caller's explicit day-count convention.</param>
    /// <exception cref="ArgumentOutOfRangeException">The option is expired or the year fraction is invalid.</exception>
    public OptionCalculator(double timeToExpiry)
    {
        if (!double.IsFinite(timeToExpiry) || timeToExpiry <= 0)
            throw new ArgumentOutOfRangeException(nameof(timeToExpiry));
        _timeToExpiry = timeToExpiry;
    }

    /// <summary>
    /// Derives implied volatility from the observed market price and calculates Black-76 Greeks.
    /// </summary>
    /// <param name="optionTypeName"><c>CALL</c> or <c>PUT</c>.</param>
    /// <param name="assetPrice">Current futures price.</param>
    /// <param name="strikePrice">Option strike price.</param>
    /// <param name="optionValue">Observed option market price.</param>
    /// <param name="riskFreeRate">Continuously compounded annual risk-free rate.</param>
    /// <returns>A successful finite result, or <see cref="OptionGreeks.Failed"/> for invalid or unsolvable input.</returns>
    public OptionGreeks GetOptionGreeks(
        string optionTypeName,
        double assetPrice,
        double strikePrice,
        double optionValue,
        double riskFreeRate)
    {
        var optionType = GetOptionType(optionTypeName);
        if (optionType == 0
            || !double.IsFinite(assetPrice) || assetPrice <= 0.0
            || !double.IsFinite(strikePrice) || strikePrice <= 0.0
            || !double.IsFinite(optionValue) || optionValue <= 0.0
            || !double.IsFinite(riskFreeRate)
            || !double.IsFinite(_timeToExpiry) || _timeToExpiry <= 0.0)
        {
            return OptionGreeks.Failed;
        }

        var discountFactor = Math.Exp(-riskFreeRate * _timeToExpiry);
        if (!double.IsFinite(discountFactor) || discountFactor <= 0.0)
            return OptionGreeks.Failed;

        var lowerBound = discountFactor * Math.Max(
            optionType > 0 ? assetPrice - strikePrice : strikePrice - assetPrice,
            0.0);
        var upperBound = discountFactor * (optionType > 0 ? assetPrice : strikePrice);
        var boundTolerance = Math.Max(PriceTolerance, upperBound * PriceTolerance);
        if (optionValue < lowerBound - boundTolerance || optionValue >= upperBound - boundTolerance)
            return OptionGreeks.Failed;

        var useRust = OptionPricerBackend.UseRust;
        double impliedVolatility;
        Black76Result result;
        if (useRust)
        {
            if (!RustOptionModel.TryImpliedVolatilityWithGreeks(
                    assetPrice,
                    strikePrice,
                    riskFreeRate,
                    optionValue,
                    _timeToExpiry,
                    optionType,
                    PriceTolerance,
                    MaximumIterations,
                    initialGuess: null,
                    out impliedVolatility,
                    out result))
            {
                return OptionGreeks.Failed;
            }
        }
        else
        {
            impliedVolatility = OptionModel.ImpliedVolatility(
                assetPrice,
                strikePrice,
                riskFreeRate,
                optionValue,
                _timeToExpiry,
                optionType,
                PriceTolerance,
                MaximumIterations);
            result = default;
        }

        if (!double.IsFinite(impliedVolatility)
            || impliedVolatility <= 0.0
            || impliedVolatility > MaximumVolatility)
        {
            return OptionGreeks.Failed;
        }

        if (!useRust)
        {
            result = OptionModel.PriceWithGreeks(
                assetPrice,
                strikePrice,
                riskFreeRate,
                impliedVolatility,
                _timeToExpiry,
                optionType);
        }
        if (!IsFinite(result))
            return OptionGreeks.Failed;

        return new OptionGreeks(
            true,
            impliedVolatility,
            result.Delta,
            result.Gamma,
            result.Theta,
            result.Vega,
            result.Rho);
    }

    private static int GetOptionType(string optionTypeName) => optionTypeName switch
    {
        "CALL" => 1,
        "PUT" => -1,
        _ => 0
    };

    private static bool IsFinite(in Black76Result result) =>
        double.IsFinite(result.Price)
        && double.IsFinite(result.Delta)
        && double.IsFinite(result.Gamma)
        && double.IsFinite(result.Theta)
        && double.IsFinite(result.Vega)
        && double.IsFinite(result.Rho);
}

/// <summary>
/// Contains Black-76 implied volatility and Greeks for one futures option.
/// </summary>
/// <param name="Success">Whether the calculation completed with finite results.</param>
/// <param name="ImpliedVolatility">Annualized implied volatility as a decimal.</param>
/// <param name="Delta">Price sensitivity to a one-unit futures-price change.</param>
/// <param name="Gamma">Delta sensitivity to a one-unit futures-price change.</param>
/// <param name="Theta">Price change for one year of calendar time passing.</param>
/// <param name="Vega">Price sensitivity to a 1.00 absolute volatility change.</param>
/// <param name="Rho">Price sensitivity to a 1.00 absolute rate change.</param>
public readonly record struct OptionGreeks(
    bool Success,
    double ImpliedVolatility,
    double Delta,
    double Gamma,
    double Theta,
    double Vega,
    double Rho)
{
    /// <summary>Represents a failed calculation without presenting zero values as successful Greeks.</summary>
    public static OptionGreeks Failed => default;
}
