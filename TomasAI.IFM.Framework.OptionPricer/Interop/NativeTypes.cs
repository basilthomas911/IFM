using System.Runtime.InteropServices;

namespace TomasAI.IFM.Framework.OptionPricer.Interop;

internal static class OptionPricerNativeConstants
{
    internal const uint AbiVersion = 1;
}

internal enum OptionPricerNativeStatus : int
{
    Ok = 0,
    NullPointer = 1,
    InvalidArgument = 2,
    NoConvergence = 3,
    Panic = 4
}

[StructLayout(LayoutKind.Sequential)]
internal struct NativeImpliedGreeksResult
{
    internal double ImpliedVolatility;
    internal double Price;
    internal double Delta;
    internal double Gamma;
    internal double Vega;
    internal double Theta;
    internal double Rho;
}
