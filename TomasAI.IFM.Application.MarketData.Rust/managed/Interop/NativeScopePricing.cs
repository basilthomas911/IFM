using System.Runtime.InteropServices;
namespace TomasAI.IFM.Application.MarketData.Rust.Interop;

// Fixed native cache record. Field order is the documented 640-byte ABI, with
// explicit decimal words; no Rust pointer, string or borrowed payload crosses it.
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct NativeScopeCalculation
{
    internal uint Size, Flags;
    internal ulong ContextLow, ContextHigh, PhysicalLow, PhysicalHigh;
    internal long At;
    internal double Time;
    internal NativePricingQuote Underlying, Option;
    internal fixed double Values[7];
    internal ulong IvContextLow, IvContextHigh;
    internal long IvAt;
    internal double IvVolatility;
    internal NativePricingQuote IvUnderlying, IvOption;
    internal uint FailureKind, FailureCode, FailureInput, Reserved;
    internal ulong Policy0, Policy1, Policy2, Policy3;
}
internal sealed partial class NativeMarketDataLibrary
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal unsafe delegate int ScopeCalculationCall(ulong handle, byte* scope, uint length, ulong expectedLow, ulong expectedHigh,
        uint instrument, uint risk, uint size, out NativeScopeCalculation calculation);
    internal ScopeCalculationCall ScopeCalculation = null!;
    void BindScopePricing(nint module)
    {
        if (Marshal.SizeOf<NativeScopeCalculation>() != 640) throw new InvalidDataException("Native option cache layout mismatch.");
        ScopeCalculation = Marshal.GetDelegateForFunctionPointer<ScopeCalculationCall>(NativeLibrary.GetExport(module, "ifm_market_data_scope_calculation_v1"));
    }
}
