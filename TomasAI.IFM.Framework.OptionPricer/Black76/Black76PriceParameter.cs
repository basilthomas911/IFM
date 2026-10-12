using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.MarketData.Shared;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using TomasAI.IFM.Framework.OptionPricer.Interop;


namespace TomasAI.IFM.Framework.OptionPricer.Black76;

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
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
public readonly record struct Black76PriceParameter(
    double ForwardPrice,
    double StrikePrice,
    double RiskFreeRate,
    double Volatility,
    double TimeToExpiry,
    int OptionType);
