using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

[module: SkipLocalsInit]

namespace TomasAI.IFM.Framework.OptionPricer.Black76;

/// <summary>
/// Represents the complete pricing result from the Black-76 option pricing model,
/// including the theoretical option price and first-order Greeks (sensitivities).
/// </summary>
/// <param name="Price">The theoretical fair value of the option, discounted to present value using the risk-free rate.</param>
/// <param name="Delta">The first derivative of the option price with respect to the underlying futures price.
/// Measures the rate of change of the option value per unit change in the futures price.
/// Calls have delta in [0, 1]; puts have delta in [-1, 0].</param>
/// <param name="Gamma">The second derivative of the option price with respect to the underlying futures price.
/// Measures the rate of change of delta per unit change in the futures price.</param>
/// <param name="Vega">The first derivative of the option price with respect to implied volatility.
/// Measures the sensitivity of the option value to a one-unit change in annualised volatility.</param>
/// <param name="Theta">The first derivative of the option price with respect to time.
/// Measures the rate of time decay of the option value, expressed as the change in price per unit of time.
/// A positive value here indicates the option gains value as time passes (typical when discounting dominates).</param>
/// <param name="Rho">The first derivative of the option price with respect to the risk-free interest rate.
/// For Black-76, rho equals <c>-T * Price</c>, reflecting the discounting effect on present value.</param>
public readonly record struct Black76Result(
    double Price,
    double Delta,
    double Gamma,
    double Vega,
    double Theta,
    double Rho);

/// <summary>
/// Encapsulates the input parameters for the Black-76 option pricing model,
/// grouping the forward price, strike, rate, volatility, time to expiry, and option type
/// into a single immutable value for batch or pipeline-style pricing.
/// </summary>
/// <param name="ForwardPrice">The current futures (forward) price. Must be positive for meaningful results.</param>
/// <param name="StrikePrice">The option strike price. Must be positive for meaningful results.</param>
/// <param name="RiskFreeRate">The continuously compounded risk-free interest rate (annualised, e.g. 0.05 for 5%).</param>
/// <param name="Volatility">The annualised implied volatility of the underlying futures price (e.g. 0.20 for 20%).</param>
/// <param name="TimeToExpiry">Time to expiry in years (e.g. 0.25 for 3 months). Values ≤ 0 are treated as expired.</param>
/// <param name="OptionType">Option type indicator: a value &gt; 0 denotes a call; a value ≤ 0 denotes a put.</param>
public readonly record struct Black76PriceParameter(
    double ForwardPrice,
    double StrikePrice,
    double RiskFreeRate,
    double Volatility,
    double TimeToExpiry,
    int OptionType);

/// <summary>
/// Encapsulates the input parameters for the Black-76 implied volatility solver,
/// grouping the forward price, strike, rate, market price, time to expiry, option type,
/// and solver tuning options into a single immutable value.
/// </summary>
/// <param name="ForwardPrice">The current futures (forward) price. Must be strictly positive.</param>
/// <param name="StrikePrice">The option strike price. Must be strictly positive.</param>
/// <param name="RiskFreeRate">The continuously compounded risk-free interest rate (annualised, e.g. 0.05 for 5%).</param>
/// <param name="MarketPrice">The observed market price of the option. Must be positive.</param>
/// <param name="TimeToExpiry">Time to expiry in years (e.g. 0.25 for 3 months). Must be strictly positive.</param>
/// <param name="OptionType">Option type indicator: a value &gt; 0 denotes a call; a value ≤ 0 denotes a put.</param>
/// <param name="Tolerance">Convergence tolerance for the absolute price difference. Defaults to 1e-10.</param>
/// <param name="MaxIterations">Maximum number of Newton-Raphson iterations. Defaults to 100.</param>
/// <param name="InitialGuess">Starting volatility guess. Defaults to 0.20 (20%) when <c>null</c>.</param>
public readonly record struct Black76ImpliedVolatilityParameter(
    double ForwardPrice,
    double StrikePrice,
    double RiskFreeRate,
    double MarketPrice,
    double TimeToExpiry,
    int OptionType,
    double Tolerance = 1e-10,
    int MaxIterations = 100,
    double? InitialGuess = null);

/// <summary>
/// Encapsulates the input parameters for a batch Black-76 pricing operation,
/// grouping the per-contract forward prices, strike prices, rates, volatilities,
/// times to expiry, and option types into a single immutable value.
/// </summary>
/// <remarks>
/// Each <see cref="ReadOnlyMemory{T}"/> member maps one-to-one to the corresponding
/// <see cref="ReadOnlySpan{T}"/> parameter of <see cref="OptionModel.PriceBatch"/>.
/// All arrays must have the same length; the batch method validates this at call time.
/// </remarks>
/// <param name="ForwardPrices">Per-contract futures (forward) prices. Must be positive for meaningful results.</param>
/// <param name="StrikePrices">Per-contract option strike prices. Must be positive for meaningful results.</param>
/// <param name="RiskFreeRates">Per-contract continuously compounded risk-free interest rates (annualised).</param>
/// <param name="Volatilities">Per-contract annualised implied volatilities.</param>
/// <param name="TimesToExpiry">Per-contract times to expiry in years. Values ≤ 0 are treated as expired.</param>
/// <param name="OptionTypes">Per-contract option type indicators (&gt; 0 = call, ≤ 0 = put).</param>
/// <param name="Results">Pre-allocated list to receive the pricing results. Must have the same length as the input arrays.</param>
public readonly record struct Black76PriceBatchParameter(
    List<double> ForwardPrices,
    List<double> StrikePrices,
    List<double> RiskFreeRates,
    List<double> Volatilities,
    List<double> TimesToExpiry,
    List<int> OptionTypes,
    List<Black76Result> Results);

/// <summary>
/// Provides a managed (pure C#) implementation of the Black-76 option pricing model
/// for European-style options on futures contracts. The model assumes log-normal
/// distribution of futures prices and is parameterised by the forward price, strike,
/// risk-free rate, volatility, and time to expiry.
/// </summary>
/// <remarks>
/// <para>
/// Black-76 is the standard variant of Black-Scholes used for options on futures,
/// where the underlying forward price <c>F</c> replaces the spot price and no
/// cost-of-carry adjustment is needed.
/// </para>
/// <para>
/// The cumulative normal distribution is approximated via an Abramowitz-Stegun
/// complementary error function (<see cref="Erfc"/>) for high throughput.
/// All hot-path methods are marked <see cref="MethodImplOptions.AggressiveInlining"/>.
/// </para>
/// </remarks>
public static class OptionModel
{
    /// <summary>
    /// The precomputed constant 1 / √2, used to convert between the standard normal
    /// CDF and the complementary error function: Φ(x) = ½ · erfc(-x / √2).
    /// </summary>
    private const double InvSqrt2 = 0.70710678118654752440084436210485;

    /// <summary>1 / √(2π), precomputed for NormPdf and merged df·φ(d₁) calculations.</summary>
    private const double InvSqrt2Pi = 0.39894228040143267793994605993438;

    [DoesNotReturn]
    private static void ThrowPositiveRequired() =>
        throw new ArgumentOutOfRangeException("F and K must be positive.");

    [DoesNotReturn]
    private static void ThrowMarketPricePositive() =>
        throw new ArgumentOutOfRangeException(nameof(ThrowMarketPricePositive), "Market price must be positive.");

    [DoesNotReturn]
    private static void ThrowTimeToExpiryPositive() =>
        throw new ArgumentOutOfRangeException(nameof(ThrowTimeToExpiryPositive), "Time to expiry must be positive.");

    [DoesNotReturn]
    private static void ThrowSpanLengthMismatch() =>
        throw new ArgumentException("All input spans must have the same length as the results span.");

    /// <summary>
    /// Computes the theoretical price of a European option on a futures contract
    /// using the Black-76 closed-form formula.
    /// </summary>
    /// <remarks>
    /// <para>When <paramref name="timeToExpiry"/> ≤ 0 the option has expired and the intrinsic value is returned.</para>
    /// <para>When <paramref name="volatility"/> ≤ 0 the option is priced as the discounted intrinsic value.</para>
    /// <para>For a call (<paramref name="optionType"/> &gt; 0):
    /// <c>Price = e^(-rT) [F·Φ(d₁) - K·Φ(d₂)]</c></para>
    /// <para>For a put (<paramref name="optionType"/> ≤ 0):
    /// <c>Price = e^(-rT) [K·Φ(-d₂) - F·Φ(-d₁)]</c></para>
    /// where d₁ = [ln(F/K) + ½σ²T] / (σ√T) and d₂ = d₁ - σ√T.
    /// </remarks>
    /// <param name="forwardPrice">The current futures (forward) price. Must be positive for meaningful results.</param>
    /// <param name="strikePrice">The option strike price. Must be positive for meaningful results.</param>
    /// <param name="riskFreeRate">The continuously compounded risk-free interest rate (annualised, e.g. 0.05 for 5%).</param>
    /// <param name="volatility">The annualised implied volatility of the underlying futures price (e.g. 0.20 for 20%).</param>
    /// <param name="timeToExpiry">Time to expiry in years (e.g. 0.25 for 3 months). Values ≤ 0 are treated as expired.</param>
    /// <param name="optionType">Option type indicator: a value &gt; 0 denotes a call; a value ≤ 0 denotes a put.</param>
    /// <returns>The present-value option price. Always non-negative.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static double Price(
        double forwardPrice,
        double strikePrice,
        double riskFreeRate,
        double volatility,
        double timeToExpiry,
        int optionType)
    {
        if (timeToExpiry <= 0.0)
            return Intrinsic(forwardPrice, strikePrice, optionType);

        if (volatility <= 0.0)
            return Math.Exp(-riskFreeRate * timeToExpiry) * Intrinsic(forwardPrice, strikePrice, optionType);

        double sqrtT = Math.Sqrt(timeToExpiry);
        double vsqrtT = volatility * sqrtT;
        double invVsqrtT = 1.0 / vsqrtT;
        double d1 = Math.FusedMultiplyAdd(0.5, vsqrtT, Math.Log(forwardPrice / strikePrice) * invVsqrtT);
        double d2 = d1 - vsqrtT;
        double df = Math.Exp(-riskFreeRate * timeToExpiry);

        // Compute call price; derive put via put-call parity: P = C - df*(F - K)
        double callPrice = df * (forwardPrice * NormCdf(d1) - strikePrice * NormCdf(d2));
        return optionType > 0
            ? callPrice
            : callPrice - df * (forwardPrice - strikePrice);
    }

    /// <summary>
    /// Computes the theoretical price and first-order Greeks (Delta, Gamma, Vega, Theta, Rho)
    /// of a European option on a futures contract using the Black-76 model.
    /// </summary>
    /// <remarks>
    /// <para>Edge-case handling:</para>
    /// <list type="bullet">
    ///   <item><description><paramref name="forwardPrice"/> ≤ 0 or <paramref name="strikePrice"/> ≤ 0 — throws <see cref="ArgumentOutOfRangeException"/>.</description></item>
    ///   <item><description><paramref name="timeToExpiry"/> ≤ 0 — returns intrinsic value with a discrete delta (±1 or 0) and all other Greeks zero.</description></item>
    ///   <item><description><paramref name="volatility"/> ≤ 0 — returns the discounted intrinsic value with delta = 0, vega = 0, gamma = 0,
    ///   theta = 0, and rho = -T × price.</description></item>
    /// </list>
    /// <para>Greek definitions:</para>
    /// <list type="bullet">
    ///   <item><description><b>Delta</b> = ∂Price/∂F — discounted cumulative normal of d₁ (call) or -d₁ (put).</description></item>
    ///   <item><description><b>Gamma</b> = ∂²Price/∂F² = e^(-rT) · φ(d₁) / (F · σ√T).</description></item>
    ///   <item><description><b>Vega</b> = ∂Price/∂σ = e^(-rT) · F · φ(d₁) · √T.</description></item>
    ///   <item><description><b>Theta</b> = ∂Price/∂t = -e^(-rT) · F · φ(d₁) · σ / (2√T) + r · Price.</description></item>
    ///   <item><description><b>Rho</b> = ∂Price/∂r = -T · Price.</description></item>
    /// </list>
    /// </remarks>
    /// <param name="forwardPrice">The current futures (forward) price. Must be strictly positive.</param>
    /// <param name="strikePrice">The option strike price. Must be strictly positive.</param>
    /// <param name="riskFreeRate">The continuously compounded risk-free interest rate (annualised).</param>
    /// <param name="volatility">The annualised implied volatility of the underlying futures price.</param>
    /// <param name="timeToExpiry">Time to expiry in years. Values ≤ 0 are treated as expired.</param>
    /// <param name="optionType">Option type indicator: a value &gt; 0 denotes a call; a value ≤ 0 denotes a put.</param>
    /// <returns>A <see cref="Black76Result"/> containing the option price and all five first-order Greeks.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="forwardPrice"/> or <paramref name="strikePrice"/> is less than or equal to zero.
    /// </exception>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static Black76Result PriceWithGreeks(
        double forwardPrice,
        double strikePrice,
        double riskFreeRate,
        double volatility,
        double timeToExpiry,
        int optionType)
    {
        if (forwardPrice <= 0.0 || strikePrice <= 0.0)
            ThrowPositiveRequired();

        if (timeToExpiry <= 0.0)
        {
            double p = Intrinsic(forwardPrice, strikePrice, optionType);
            double intrinsicDelta = optionType > 0
                ? (forwardPrice > strikePrice ? 1.0 : 0.0)
                : (forwardPrice < strikePrice ? -1.0 : 0.0);

            return new Black76Result(p, intrinsicDelta, 0.0, 0.0, 0.0, 0.0);
        }

        if (volatility <= 0.0)
        {
            double p = Math.Exp(-riskFreeRate * timeToExpiry) * Intrinsic(forwardPrice, strikePrice, optionType);
            return new Black76Result(p, 0.0, 0.0, 0.0, 0.0, -timeToExpiry * p);
        }

        double sqrtT = Math.Sqrt(timeToExpiry);
        double vsqrtT = volatility * sqrtT;
        double invVsqrtT = 1.0 / vsqrtT;
        double d1 = Math.FusedMultiplyAdd(0.5, vsqrtT, Math.Log(forwardPrice / strikePrice) * invVsqrtT);
        double d2 = d1 - vsqrtT;
        // Merge df and NormPdf into a single Exp: dfPdfd1 = (1/√2π) · exp(-rT - ½d₁²)
        double rT = riskFreeRate * timeToExpiry;
        double dfPdfd1 = InvSqrt2Pi * Math.Exp(Math.FusedMultiplyAdd(-0.5, d1 * d1, -rT));
        double df = Math.Exp(-rT);

        double price, delta, gamma, vega, theta, rho;

        if (optionType > 0)
        {
            double nd1 = NormCdf(d1);
            double nd2 = NormCdf(d2);
            price = df * (forwardPrice * nd1 - strikePrice * nd2);
            delta = df * nd1;
        }
        else
        {
            double nNd1 = NormCdf(-d1);
            double nNd2 = NormCdf(-d2);
            price = df * (strikePrice * nNd2 - forwardPrice * nNd1);
            delta = -df * nNd1;
        }

        gamma = dfPdfd1 * invVsqrtT / forwardPrice;
        vega = dfPdfd1 * forwardPrice * sqrtT;
        theta = Math.FusedMultiplyAdd(riskFreeRate, price, -0.5 * vega * volatility / timeToExpiry);
        rho = -timeToExpiry * price;

        return new Black76Result(price, delta, gamma, vega, theta, rho);
    }

    /// <summary>
    /// Computes only the price and vega for a European option on a futures contract using Black-76.
    /// Optimised for the Newton-Raphson implied volatility solver — avoids computing delta, gamma, theta, rho.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static (double Price, double Vega) PriceAndVega(
        double forwardPrice,
        double strikePrice,
        double riskFreeRate,
        double volatility,
        double timeToExpiry,
        int optionType)
    {
        double sqrtT = Math.Sqrt(timeToExpiry);
        double vsqrtT = volatility * sqrtT;
        double invVsqrtT = 1.0 / vsqrtT;
        double d1 = Math.FusedMultiplyAdd(0.5, vsqrtT, Math.Log(forwardPrice / strikePrice) * invVsqrtT);
        double d2 = d1 - vsqrtT;
        double rT = riskFreeRate * timeToExpiry;
        double dfPdfd1 = InvSqrt2Pi * Math.Exp(Math.FusedMultiplyAdd(-0.5, d1 * d1, -rT));
        double df = Math.Exp(-rT);

        // Compute call price; derive put via put-call parity
        double callPrice = df * (forwardPrice * NormCdf(d1) - strikePrice * NormCdf(d2));
        double price = optionType > 0
            ? callPrice
            : callPrice - df * (forwardPrice - strikePrice);
        double vega = dfPdfd1 * forwardPrice * sqrtT;

        return (price, vega);
    }

    /// <summary>
    /// Computes the implied volatility of a European option on a futures contract
    /// using the Newton-Raphson method with vega as the first derivative.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The solver iterates σ_{n+1} = σ_n - (Price(σ_n) - marketPrice) / Vega(σ_n),
    /// converging quadratically near the solution. A vega floor is applied to prevent
    /// division by near-zero derivatives far from the money.
    /// </para>
    /// <para>
    /// The initial guess defaults to 0.20 (20% annualised volatility) when
    /// <paramref name="initialGuess"/> is <c>null</c>.
    /// </para>
    /// </remarks>
    /// <param name="forwardPrice">The current futures (forward) price. Must be strictly positive.</param>
    /// <param name="strikePrice">The option strike price. Must be strictly positive.</param>
    /// <param name="riskFreeRate">The continuously compounded risk-free interest rate (annualised).</param>
    /// <param name="marketPrice">The observed market price of the option. Must be positive.</param>
    /// <param name="timeToExpiry">Time to expiry in years. Must be strictly positive.</param>
    /// <param name="optionType">Option type indicator: a value &gt; 0 denotes a call; a value ≤ 0 denotes a put.</param>
    /// <param name="tolerance">Convergence tolerance for the absolute price difference. Defaults to 1e-10.</param>
    /// <param name="maxIterations">Maximum number of Newton-Raphson iterations. Defaults to 100.</param>
    /// <param name="initialGuess">Starting volatility guess. Defaults to 0.20 (20%).</param>
    /// <returns>The implied volatility as an annualised decimal (e.g. 0.20 for 20%), or <see cref="double.NaN"/> if the solver fails to converge.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="forwardPrice"/>, <paramref name="strikePrice"/>,
    /// <paramref name="marketPrice"/>, or <paramref name="timeToExpiry"/> is less than or equal to zero.
    /// </exception>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static double ImpliedVolatility(
        double forwardPrice,
        double strikePrice,
        double riskFreeRate,
        double marketPrice,
        double timeToExpiry,
        int optionType,
        double tolerance = 1e-10,
        int maxIterations = 100,
        double? initialGuess = null)
    {
        if (forwardPrice <= 0.0 || strikePrice <= 0.0)
            ThrowPositiveRequired();

        if (marketPrice <= 0.0)
            ThrowMarketPricePositive();

        if (timeToExpiry <= 0.0)
            ThrowTimeToExpiryPositive();

        double sigma = initialGuess ?? 0.20;
        const double vegaFloor = 1e-12;

        // Hoist all loop-invariant transcendentals and products.
        // Only sigma (and therefore d1, d2) changes across iterations.
        double sqrtT = Math.Sqrt(timeToExpiry);
        double logFK = Math.Log(forwardPrice / strikePrice);
        double rT = riskFreeRate * timeToExpiry;
        double df = Math.Exp(-rT);
        double dfF = df * forwardPrice;
        double dfK = df * strikePrice;
        double dfFMinusK = dfF - dfK;
        double Cdf = InvSqrt2Pi * df;
        double FsqrtT = forwardPrice * sqrtT;

        for (int i = 0; i < maxIterations; i++)
        {
            double vsqrtT = sigma * sqrtT;
            double invVsqrtT = 1.0 / vsqrtT;
            double d1 = Math.FusedMultiplyAdd(0.5, vsqrtT, logFK * invVsqrtT);
            double d2 = d1 - vsqrtT;

            double dfPdfd1 = Cdf * Math.Exp(-0.5 * d1 * d1);
            double callPrice = dfF * NormCdf(d1) - dfK * NormCdf(d2);
            double price = optionType > 0 ? callPrice : callPrice - dfFMinusK;
            double vega = dfPdfd1 * FsqrtT;

            double diff = price - marketPrice;

            if (Math.Abs(diff) < tolerance)
                return sigma;

            sigma -= diff / Math.Max(Math.Abs(vega), vegaFloor);

            if (sigma <= 0.0)
                sigma = 1e-6;
        }

        return double.NaN;
    }

    /// <summary>
    /// Computes the theoretical price of multiple European options on futures contracts
    /// in a single batch operation using the Black-76 closed-form formula.
    /// </summary>
    /// <remarks>
    /// <para>
    /// All input spans must have the same length as <paramref name="results"/>.
    /// Each element index represents a single option contract. The pricing for each
    /// contract delegates to the scalar <see cref="Price"/> method, benefiting from
    /// <see cref="MethodImplOptions.AggressiveOptimization"/> for tight loop codegen.
    /// </para>
    /// </remarks>
    /// <param name="forwardPrices">Span of futures (forward) prices for each option.</param>
    /// <param name="strikePrices">Span of strike prices for each option.</param>
    /// <param name="riskFreeRates">Span of risk-free interest rates for each option.</param>
    /// <param name="volatilities">Span of implied volatilities for each option.</param>
    /// <param name="timesToExpiry">Span of times to expiry (in years) for each option.</param>
    /// <param name="optionTypes">Span of option type indicators (&gt; 0 = call, ≤ 0 = put).</param>
    /// <param name="results">Output span to receive the computed option prices. Must be the same length as input spans.</param>
    /// <exception cref="ArgumentException">Thrown when input spans have mismatched lengths.</exception>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void PriceBatch(
        ReadOnlySpan<double> forwardPrices,
        ReadOnlySpan<double> strikePrices,
        ReadOnlySpan<double> riskFreeRates,
        ReadOnlySpan<double> volatilities,
        ReadOnlySpan<double> timesToExpiry,
        ReadOnlySpan<int> optionTypes,
        Span<double> results)
    {
        int n = results.Length;

        if (forwardPrices.Length != n || strikePrices.Length != n ||
            riskFreeRates.Length != n || volatilities.Length != n ||
            timesToExpiry.Length != n || optionTypes.Length != n)
            ThrowSpanLengthMismatch();

        ref double fRef = ref MemoryMarshal.GetReference(forwardPrices);
        ref double kRef = ref MemoryMarshal.GetReference(strikePrices);
        ref double rRef = ref MemoryMarshal.GetReference(riskFreeRates);
        ref double vRef = ref MemoryMarshal.GetReference(volatilities);
        ref double tRef = ref MemoryMarshal.GetReference(timesToExpiry);
        ref int oRef = ref MemoryMarshal.GetReference(optionTypes);
        ref double resRef = ref MemoryMarshal.GetReference(results);

        for (int i = 0; i < n; i++)
        {
            Unsafe.Add(ref resRef, i) = Price(
                Unsafe.Add(ref fRef, i), Unsafe.Add(ref kRef, i), Unsafe.Add(ref rRef, i),
                Unsafe.Add(ref vRef, i), Unsafe.Add(ref tRef, i), Unsafe.Add(ref oRef, i));
        }
    }

    /// <summary>
    /// Computes the theoretical price and first-order Greeks of multiple European options
    /// on futures contracts in a single batch operation using the Black-76 model.
    /// </summary>
    /// <remarks>
    /// <para>
    /// All input spans must have the same length as <paramref name="results"/>.
    /// Each element index represents a single option contract. The computation for each
    /// contract delegates to the scalar <see cref="PriceWithGreeks"/> method.
    /// </para>
    /// </remarks>
    /// <param name="forwardPrices">Span of futures (forward) prices for each option. Must be strictly positive.</param>
    /// <param name="strikePrices">Span of strike prices for each option. Must be strictly positive.</param>
    /// <param name="riskFreeRates">Span of risk-free interest rates for each option.</param>
    /// <param name="volatilities">Span of implied volatilities for each option.</param>
    /// <param name="timesToExpiry">Span of times to expiry (in years) for each option.</param>
    /// <param name="optionTypes">Span of option type indicators (&gt; 0 = call, ≤ 0 = put).</param>
    /// <param name="results">Output span to receive the computed <see cref="Black76Result"/> values. Must be the same length as input spans.</param>
    /// <exception cref="ArgumentException">Thrown when input spans have mismatched lengths.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any forward price or strike price is less than or equal to zero.</exception>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void PriceWithGreeksBatch(
        ReadOnlySpan<double> forwardPrices,
        ReadOnlySpan<double> strikePrices,
        ReadOnlySpan<double> riskFreeRates,
        ReadOnlySpan<double> volatilities,
        ReadOnlySpan<double> timesToExpiry,
        ReadOnlySpan<int> optionTypes,
        Span<Black76Result> results)
    {
        int n = results.Length;

        if (forwardPrices.Length != n || strikePrices.Length != n ||
            riskFreeRates.Length != n || volatilities.Length != n ||
            timesToExpiry.Length != n || optionTypes.Length != n)
            ThrowSpanLengthMismatch();

        ref double fRef = ref MemoryMarshal.GetReference(forwardPrices);
        ref double kRef = ref MemoryMarshal.GetReference(strikePrices);
        ref double rRef = ref MemoryMarshal.GetReference(riskFreeRates);
        ref double vRef = ref MemoryMarshal.GetReference(volatilities);
        ref double tRef = ref MemoryMarshal.GetReference(timesToExpiry);
        ref int oRef = ref MemoryMarshal.GetReference(optionTypes);
        ref Black76Result resRef = ref MemoryMarshal.GetReference(results);

        for (int i = 0; i < n; i++)
        {
            Unsafe.Add(ref resRef, i) = PriceWithGreeks(
                Unsafe.Add(ref fRef, i), Unsafe.Add(ref kRef, i), Unsafe.Add(ref rRef, i),
                Unsafe.Add(ref vRef, i), Unsafe.Add(ref tRef, i), Unsafe.Add(ref oRef, i));
        }
    }

    /// <summary>
    /// Calculates the intrinsic (payoff) value of an option at expiry.
    /// </summary>
    /// <remarks>
    /// For a call: max(F - K, 0). For a put: max(K - F, 0).
    /// No discounting is applied; the caller is responsible for multiplying
    /// by the discount factor when needed.
    /// </remarks>
    /// <param name="F">The current futures (forward) price.</param>
    /// <param name="K">The option strike price.</param>
    /// <param name="optionType">Option type indicator: a value &gt; 0 denotes a call; a value ≤ 0 denotes a put.</param>
    /// <returns>The non-negative intrinsic value of the option.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double Intrinsic(double F, double K, int optionType) =>
        optionType > 0 ? Math.Max(F - K, 0.0) : Math.Max(K - F, 0.0);

    /// <summary>
    /// Computes the cumulative distribution function (CDF) of the standard normal distribution, Φ(x).
    /// </summary>
    /// <remarks>
    /// Implemented as Φ(x) = ½ · erfc(-x / √2), delegating to the <see cref="Erfc"/> approximation.
    /// Maximum absolute error is approximately 1.5 × 10⁻⁷.
    /// </remarks>
    /// <param name="x">The quantile at which to evaluate the standard normal CDF.</param>
    /// <returns>The probability P(Z ≤ x) where Z ~ N(0, 1), in the range [0, 1].</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double NormCdf(double x) =>
        0.5 * Erfc(-x * InvSqrt2);

    /// <summary>
    /// Computes the probability density function (PDF) of the standard normal distribution, φ(x).
    /// </summary>
    /// <remarks>
    /// Evaluated as φ(x) = (1 / √(2π)) · exp(-x² / 2), where the leading constant
    /// 1 / √(2π) ≈ 0.39894228040143267793994605993438 is precomputed for performance.
    /// </remarks>
    /// <param name="x">The point at which to evaluate the standard normal PDF.</param>
    /// <returns>The density value φ(x), always non-negative.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double NormPdf(double x) =>
        InvSqrt2Pi * Math.Exp(-0.5 * x * x);

    /// <summary>
    /// Computes the complementary error function, erfc(x) = 1 - erf(x),
    /// using an Abramowitz-Stegun rational approximation (formula 7.1.26).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The approximation uses a 9-term polynomial in a transformed variable
    /// <c>t = 1 / (1 + 0.5|x|)</c> and exploits <see cref="Math.FusedMultiplyAdd"/>
    /// for improved numerical accuracy and potential hardware acceleration.
    /// </para>
    /// <para>
    /// The maximum absolute error is approximately 1.5 × 10⁻⁷ across the
    /// entire real line, which is sufficient for option pricing applications.
    /// </para>
    /// <para>
    /// For negative <paramref name="x"/>, the identity erfc(-x) = 2 - erfc(x) is applied.
    /// </para>
    /// </remarks>
    /// <param name="x">The argument at which to evaluate erfc. May be any finite double value.</param>
    /// <returns>The complementary error function value erfc(x), in the range [0, 2].</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double Erfc(double x)
    {
        // Abramowitz-Stegun style approximation
        double z = Math.Abs(x);
        double t = 1.0 / Math.FusedMultiplyAdd(0.5, z, 1.0);

        double ans = t * Math.Exp(
            Math.FusedMultiplyAdd(-z, z, -1.26551223
            + t * Math.FusedMultiplyAdd(t, Math.FusedMultiplyAdd(t, Math.FusedMultiplyAdd(t,
                Math.FusedMultiplyAdd(t, Math.FusedMultiplyAdd(t, Math.FusedMultiplyAdd(t,
                    Math.FusedMultiplyAdd(t, Math.FusedMultiplyAdd(t, 0.17087277, -0.82215223),
                    1.48851587), -1.13520398), 0.27886807), -0.18628806),
                0.09678418), 0.37409196), 1.00002368)));

        // JIT emits cmov — 3 µops vs 8 for the bit-manipulation variant.
        return x >= 0.0 ? ans : 2.0 - ans;
    }
}
