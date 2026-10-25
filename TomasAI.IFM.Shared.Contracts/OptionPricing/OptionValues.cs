using System.Collections.Immutable;

namespace TomasAI.IFM.Shared.OptionPricing;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]

public readonly record struct OptionValues(
    double Price, double Volatility, double Delta, double Gamma,
    double Vega, double Theta, double Rho);
