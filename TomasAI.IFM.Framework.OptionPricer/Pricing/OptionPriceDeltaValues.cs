using System.Collections.Immutable;

namespace TomasAI.IFM.Framework.OptionPricer.Pricing;

/// <summary>Only calculated outputs; no placeholders for unrequested Greeks.</summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
public readonly record struct OptionPriceDeltaValues(double Price, double Volatility, double Delta);
