using System.Runtime.InteropServices;
namespace TomasAI.IFM.Application.MarketData.Rust.Interop;

// Both layouts are versioned independently of CLR packing defaults and checked
// at library construction. No managed object/reference crosses native memory.
[StructLayout(LayoutKind.Sequential)]
internal struct NativePricingRequest {
    internal uint Size,Action,Flags,Profile;
    internal double Forward,Strike,Time,Rate,Input;
    internal uint Steps,Iterations;
    internal double MaximumVolatility,Tolerance;
}
[StructLayout(LayoutKind.Sequential)]
internal struct NativePricingResult {
    internal uint Size,Failure;
    internal double Price,Volatility,Delta,Gamma,Vega,Theta,Rho;
}
