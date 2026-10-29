using System.Runtime.InteropServices;
namespace TomasAI.IFM.Application.MarketData.Rust.Interop;

internal sealed partial class NativeMarketDataLibrary
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate int ReferenceSubmitCall(ulong handle,ulong operation,uint mode,uint from,uint to,ulong budget);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal unsafe delegate int ReferenceTakeCall(ulong handle,ulong operation,byte* output,uint capacity,out uint length);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate int ReferenceDiscardCall(ulong handle,ulong operation);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate int TreasuryRateCall(double halfYield,out double rate);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal unsafe delegate int ColdSubmitCall(ulong handle,ulong operation,uint mode,byte* input,uint length,ulong budget);
    internal ClockLiveCall ApiClock=null!;
    internal HistoryConfigureCall FmpConfigure=null!;
    internal ColdSubmitCall FmpSubmit=null!;
    internal ColdSubmitCall HistoryControl=null!;
    internal HistoryConfigureCall ReferenceConfigure=null!;
    internal ReferenceSubmitCall ReferenceSubmit=null!;
    internal ReferenceTakeCall ReferenceTake=null!;
    internal ReferenceDiscardCall ReferenceDiscard=null!;
    internal TreasuryRateCall TreasuryRate=null!;
    void BindReference(nint module)
    {
        T Bind<T>(string name)where T:Delegate=>Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(module,name));
        ApiClock=Bind<ClockLiveCall>("ifm_market_data_clock_v1");
        FmpConfigure=Bind<HistoryConfigureCall>("ifm_market_data_fmp_configure_v1");
        HistoryControl=Bind<ColdSubmitCall>("ifm_market_data_history_control_v1");
        FmpSubmit=Bind<ColdSubmitCall>("ifm_market_data_fmp_submit_v1");
        ReferenceConfigure=Bind<HistoryConfigureCall>("ifm_market_data_reference_configure_v1");
        ReferenceSubmit=Bind<ReferenceSubmitCall>("ifm_market_data_reference_submit_v1");
        ReferenceTake=Bind<ReferenceTakeCall>("ifm_market_data_reference_take_v1");
        ReferenceDiscard=Bind<ReferenceDiscardCall>("ifm_market_data_reference_discard_v1");
        TreasuryRate=Bind<TreasuryRateCall>("ifm_market_data_treasury_rate_v1");
    }
}
