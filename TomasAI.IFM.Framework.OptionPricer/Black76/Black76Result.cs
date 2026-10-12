using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.MarketData.Shared;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using TomasAI.IFM.Framework.OptionPricer.Interop;


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
[StructLayout(LayoutKind.Sequential)]
public readonly record struct Black76Result(
    double Price,
    double Delta,
    double Gamma,
    double Vega,
    double Theta,
    double Rho);
