using System.Runtime.InteropServices;

namespace TomasAI.IFM.Framework.OptionPricer.Interop;

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
