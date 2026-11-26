using System.Runtime.InteropServices;

namespace TomasAI.IFM.Application.MarketData.Rust.Interop;

internal sealed partial class NativeMarketDataLibrary
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int SessionHoldCall(ulong lease, ulong budgetMs);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int SessionReleaseCall(ulong lease);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate ulong SessionStatusCall();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal unsafe delegate int SessionPrefixCall(byte* scope, uint scopeLength, ulong lease, byte* output, uint capacity, out uint required);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal unsafe delegate int CheckpointExportCall(byte* scope,uint scopeLength,ulong lease,byte* path,uint pathLength,byte* fileSha256);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal unsafe delegate int CheckpointImportCall(byte* scope,uint scopeLength,byte* path,uint pathLength,byte* expectedFileSha256);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal unsafe delegate int CheckpointBeginCall(byte* scope,uint scopeLength,ulong lease);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal unsafe delegate int CheckpointRestoreRootCall(byte* scope,uint scopeLength,byte* root,uint rootLength);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal unsafe delegate int ProductionPauseCall(byte* scope,uint scopeLength,ulong lease,uint budgetMs);
    internal SessionHoldCall? SessionHold;
    internal SessionReleaseCall? SessionRelease;
    internal SessionStatusCall? SessionLease, SessionActive;
    internal SessionPrefixCall? SessionPrefix;
    internal CheckpointExportCall? CheckpointExport;
    internal CheckpointImportCall? CheckpointImport;
    internal CheckpointBeginCall? CheckpointBegin;
    internal SessionReleaseCall? CheckpointEnd;
    internal CheckpointRestoreRootCall? CheckpointRestoreRoot;
    internal ProductionPauseCall? ProductionPause;
    internal SessionReleaseCall? ProductionResume;
    internal SessionStatusCall? ProductionLease, ProductionActive;
    void BindSessionBoundary(nint module)
    {
        T? Bind<T>(string name) where T : Delegate => NativeLibrary.TryGetExport(module, name, out var address)
            ? Marshal.GetDelegateForFunctionPointer<T>(address) : null;
        SessionHold = Bind<SessionHoldCall>("ifm_market_data_source_forwarding_hold_v1");
        SessionRelease = Bind<SessionReleaseCall>("ifm_market_data_source_forwarding_release_v1");
        SessionLease = Bind<SessionStatusCall>("ifm_market_data_source_forwarding_lease_v1");
        SessionActive = Bind<SessionStatusCall>("ifm_market_data_source_forwarding_active_v1");
        SessionPrefix = Bind<SessionPrefixCall>("ifm_market_data_source_prefix_v1");
        CheckpointExport=Bind<CheckpointExportCall>("ifm_market_data_native_checkpoint_export_v1");
        CheckpointImport=Bind<CheckpointImportCall>("ifm_market_data_native_checkpoint_import_v1");
        CheckpointBegin=Bind<CheckpointBeginCall>("ifm_market_data_native_checkpoint_begin_v1");
        CheckpointEnd=Bind<SessionReleaseCall>("ifm_market_data_native_checkpoint_end_v1");
        CheckpointRestoreRoot=Bind<CheckpointRestoreRootCall>("ifm_market_data_native_checkpoint_restore_root_v1");
        ProductionPause=Bind<ProductionPauseCall>("ifm_market_data_source_production_pause_v1");
        ProductionResume=Bind<SessionReleaseCall>("ifm_market_data_source_production_resume_v1");
        ProductionLease=Bind<SessionStatusCall>("ifm_market_data_source_production_lease_v1");
        ProductionActive=Bind<SessionStatusCall>("ifm_market_data_source_production_active_v1");
    }
}
