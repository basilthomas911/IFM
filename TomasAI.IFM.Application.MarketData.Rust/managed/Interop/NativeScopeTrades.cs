using System.Runtime.InteropServices;
using TomasAI.IFM.Application.MarketData.Rust.Delivery;
namespace TomasAI.IFM.Application.MarketData.Rust.Interop;

// Fixed source-time trade layout; no native pointer crosses the ABI.
[StructLayout(LayoutKind.Sequential)]
internal struct NativeOptionPublication
{
    internal LivePublication Publication;
    internal uint Size,Flags;
    internal ulong ContextLow,ContextHigh;
    internal long At,QualifiedUntil;
    internal NativePricingQuote Underlying;
    internal uint ManifestOffset,ManifestLength;
}
internal sealed partial class NativeMarketDataLibrary
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal unsafe delegate int ScopeTradePublicationsCall(ulong handle,byte* scope,uint length,ulong expectedLow,ulong expectedHigh,uint size,
        NativeOptionPublication* records,uint capacity,byte* manifests,uint manifestCapacity,out uint count,out uint written);
    internal ScopeTradePublicationsCall ScopeTradePublications=null!;
    void BindScopeTrades(nint module)
    {
        if(Marshal.SizeOf<NativeOptionPublication>()!=816)throw new InvalidDataException("Native option trade layout mismatch.");
        ScopeTradePublications=Marshal.GetDelegateForFunctionPointer<ScopeTradePublicationsCall>(NativeLibrary.GetExport(module,"ifm_market_data_scope_publications_v2"));
    }
}
