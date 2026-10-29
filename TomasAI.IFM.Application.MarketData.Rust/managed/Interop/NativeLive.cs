using System.Runtime.InteropServices;
namespace TomasAI.IFM.Application.MarketData.Rust.Interop;

[StructLayout(LayoutKind.Sequential)]
internal struct NativeLiveConfiguration {
    internal uint Size,Date;
    internal long UtcTicks;
    internal ulong IdentityLow,IdentityHigh;
    internal uint Capacity,Advancing;
}
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct NativeControlResult {
    internal uint Size,Failure,Length,Reserved;
    internal fixed byte Message[4096];
}
[StructLayout(LayoutKind.Sequential)]
internal struct NativeLaneStatus {
    internal uint Size,Phase;
    internal ulong Attempt,Episode,Attempts,GenerationLow,GenerationHigh,Reserved0,Reserved1;
}
[StructLayout(LayoutKind.Sequential)]
internal struct NativeLast {
    internal uint Size,Flags;
    internal ulong GenerationLow,GenerationHigh;
    internal long Sequence,EventTicks,ReceiveTicks,Price;
    internal uint Quantity,Reserved;
    internal long Bid,Ask;
    internal uint BidQuantity,AskQuantity;
    internal long LocalTicks;
    internal uint BidCount,AskCount;
}
internal sealed partial class NativeMarketDataLibrary {
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int SignalsLiveCall(ulong handle,out uint bits);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal unsafe delegate int EventsLiveCall(ulong handle,Delivery.LiveMarketEvent* records,uint capacity,out uint count);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal unsafe delegate int DiagnosticsLiveCall(ulong handle,NativeDiagnostic* records,uint capacity,out uint count);
    internal SignalsLiveCall SignalsLive=null!;
    internal EventsLiveCall EventsLive=null!;
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal unsafe delegate int PublicationsLiveCall(ulong handle,Delivery.LivePublication* records,uint capacity,out uint count);
    internal PublicationsLiveCall PublicationsLive=null!;
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int StateLiveCall(ulong handle,uint lane,uint instrument,uint publisher,uint size,out Delivery.LivePublication state);
    internal StateLiveCall StateLive=null!;
    internal DiagnosticsLiveCall DiagnosticsLive=null!;
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int ConfigureLiveCall(ulong handle,in NativeLiveConfiguration config);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int ClockLiveCall(ulong handle,long ticks,uint advancing);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal unsafe delegate int SubmitLiveCall(ulong handle,ulong operation,uint action,uint lane,byte* data,uint length,ulong budgetMs);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal unsafe delegate int TakeLiveCall(ulong handle,ulong operation,uint size,out NativeControlResult result);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int StatusLiveCall(ulong handle,uint lane,uint size,out NativeLaneStatus result);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal unsafe delegate int LastLiveCall(ulong handle,uint lane,uint kind,byte* id,uint length,uint size,out NativeLast result);
    internal ConfigureLiveCall ConfigureLive=null!;
    internal ClockLiveCall ClockLive=null!;
    internal SubmitLiveCall SubmitLive=null!;
    internal TakeLiveCall TakeLive=null!;
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal unsafe delegate int FailureLiveCall(ulong handle,uint lane,uint size,out NativeControlResult result);
    internal FailureLiveCall FailureLive=null!;
    internal StatusLiveCall StatusLive=null!;
    internal LastLiveCall LastLive=null!;
    unsafe void BindLive(nint module) {
        T Bind<T>(string symbol) where T:Delegate=>Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(module,symbol));
        if(Marshal.SizeOf<NativeLiveConfiguration>()!=40||Marshal.SizeOf<NativeControlResult>()!=4112||Marshal.SizeOf<NativeLaneStatus>()!=64||Marshal.SizeOf<NativeLast>()!=104) throw new InvalidDataException("Rust live ABI layout mismatch.");
        ConfigureLive=Bind<ConfigureLiveCall>("ifm_market_data_live_configure_v1");
        ClockLive=Bind<ClockLiveCall>("ifm_market_data_live_clock_v1");
        SubmitLive=Bind<SubmitLiveCall>("ifm_market_data_live_submit_v1");
        TakeLive=Bind<TakeLiveCall>("ifm_market_data_live_take_v1");
        FailureLive=Bind<FailureLiveCall>("ifm_market_data_live_failure_v1");
        StatusLive=Bind<StatusLiveCall>("ifm_market_data_live_status_v1");
        LastLive=Bind<LastLiveCall>("ifm_market_data_live_last_v1");
        SignalsLive=Bind<SignalsLiveCall>("ifm_market_data_live_signals_v1");
        EventsLive=Bind<EventsLiveCall>("ifm_market_data_live_events_v1");
        if(Marshal.SizeOf<Delivery.LivePublication>()!=656)throw new InvalidDataException("Rust publication ABI layout mismatch.");
        PublicationsLive=Bind<PublicationsLiveCall>("ifm_market_data_live_publications_v1");
        StateLive=Bind<StateLiveCall>("ifm_market_data_live_state_v1");
        DiagnosticsLive=Bind<DiagnosticsLiveCall>("ifm_market_data_live_diagnostics_v1");
        if(Marshal.SizeOf<Delivery.LiveMarketEvent>()!=128||Marshal.SizeOf<NativeDiagnostic>()!=4128)throw new InvalidDataException("Rust live event ABI layout mismatch.");
    }
}
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct NativeDiagnostic {
    internal uint Size,Lane,Kind,Code;
    internal ulong Attempt;
    internal uint Last,Length;
    internal fixed byte Message[4096];
}
