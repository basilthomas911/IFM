using System.Collections.Immutable;

namespace TomasAI.IFM.Shared.OptionPricing;

/// <summary>Residual is repriced price minus market price. Iterations counts bisection iterations.</summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
public readonly record struct ImpliedVolatilityValues(
    double Volatility, double RepricedPrice, double Residual, int Iterations);
