using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.MarketData.Shared;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using TomasAI.IFM.Framework.OptionPricer.Interop;


namespace TomasAI.IFM.Framework.OptionPricer.Black76;

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
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
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
