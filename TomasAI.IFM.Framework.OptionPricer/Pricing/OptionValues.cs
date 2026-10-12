using System.Collections.Immutable;

namespace TomasAI.IFM.Framework.OptionPricer.Pricing;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]

public readonly record struct OptionValues(
    double Price, double Volatility, double Delta, double Gamma,
    double Vega, double Theta, double Rho);
