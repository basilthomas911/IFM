using System.Collections.Immutable;

namespace TomasAI.IFM.Shared.OptionPricing;
/// <summary>Known cash dividend per share, at a qualified year fraction after valuation.</summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
public readonly record struct CashDividend(double Time, double Amount);
