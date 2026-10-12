using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.MarketData.Shared;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using TomasAI.IFM.Framework.OptionPricer.Interop;


namespace TomasAI.IFM.Framework.OptionPricer.Black76;

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
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002", Justification = "Preserves the existing public pricing collection contract.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0016", Justification = "Preserves the existing public pricing collection contract.")]
public readonly record struct Black76PriceBatchParameter(
    List<double> ForwardPrices,
    List<double> StrikePrices,
    List<double> RiskFreeRates,
    List<double> Volatilities,
    List<double> TimesToExpiry,
    List<int> OptionTypes,
    List<Black76Result> Results);
