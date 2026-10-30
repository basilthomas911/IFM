using System.Runtime.InteropServices;

namespace TomasAI.IFM.Application.MarketData.Rust.Interop;

/// <summary>Copied native control evidence. No field is a resource handle.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct NativeOwnerHealth
{
    internal uint Size, Lane, Phase, Missing;
    internal ulong Episode, Revision, Generation, RemainingMilliseconds, PendingFailures, Reserved;
}

[StructLayout(LayoutKind.Sequential)]
internal struct NativeOwnerIdentity
{
    internal uint Size, Lane;
    internal int ValueDate;
    internal uint Flags;
    internal ulong WorkerLow, WorkerHigh, GenerationLow, GenerationHigh;
    internal long Revision;
}

[StructLayout(LayoutKind.Sequential)]
internal struct NativeRetiredEvidence
{
    internal uint Size,Reserved;
    internal ulong Retained,Generations,Records,Bytes;
}

internal sealed partial class NativeMarketDataLibrary
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal unsafe delegate int OwnerInputCall(ulong handle, byte* input, uint length);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal unsafe delegate int OwnerFuturesCall(ulong handle, uint lane, byte* input, uint length);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int OwnerHealthCall(ulong handle, uint lane, uint size, out NativeOwnerHealth health);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal unsafe delegate int OwnerFailuresCall(ulong handle, byte* output, uint capacity, out uint required);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int OwnerAckCall(ulong handle, uint lane, ulong ordinal);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int OwnerRecoverCall(ulong handle, uint lane);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int OwnerIdentityCall(ulong handle, uint lane, uint size, out NativeOwnerIdentity identity);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate ulong OwnerFeaturesCall();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int OwnerRetiredEvidenceCall(ulong handle,uint size,out NativeRetiredEvidence value);
    internal OwnerRetiredEvidenceCall? OwnerRetiredEvidence;
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal unsafe delegate int OwnerBackendStatusCall(ulong handle,uint ready,byte* output,uint capacity,out uint required);
    internal OwnerBackendStatusCall? OwnerBackendStatus, OwnerReadSnapshot;
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal unsafe delegate int OwnerQueryCall(ulong handle,byte* input,uint length,byte* output,uint capacity,out uint required);
    internal OwnerQueryCall? OwnerQuery;
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal unsafe delegate int OwnerRouteCall(ulong handle,byte* input,uint length,out byte changed);
    internal OwnerRouteCall? OwnerRoute;
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal unsafe delegate int OwnerRecoveryRequestCall(ulong handle,ulong operation,byte* input,uint length);
    internal OwnerRecoveryRequestCall? OwnerRecoveryRequest,OwnerCapture;
    internal ulong OwnerFeatures;
    internal OwnerIdentityCall? OwnerIdentity;
    internal HandleCall? CreateOwner;
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int OwnerReinitializeCall(ulong handle, ulong operation, uint date);
    internal OwnerReinitializeCall? OwnerReinitialize;
    internal OwnerInputCall? OwnerCatalog, OwnerCommand, OwnerStart;
    internal OwnerFuturesCall? OwnerFutures;
    internal OwnerHealthCall? OwnerHealth;
    internal OwnerFailuresCall? OwnerFailures;
    internal OwnerAckCall? OwnerAck;
    internal OwnerRecoverCall? OwnerRecover;
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal unsafe delegate int OwnerCacheCall(ulong handle, byte* scope, uint scopeLength, byte* output, uint capacity, out uint required);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal unsafe delegate int OwnerQuoteGreeksCall(ulong handle, uint instrument, uint publisher, byte* output, uint capacity, out uint required);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal unsafe delegate int OwnerRawPricesCall(ulong handle, uint lane, byte* contract, uint contractLength, uint date, byte* output, uint capacity, out uint required);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal unsafe delegate int OwnerCurrentCall(ulong handle,uint lane,long after,uint maximum,byte* output,uint capacity,out uint required);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int OwnerWaitCall(ulong handle,ulong operation,uint lane,uint requireActive);
    internal OwnerWaitCall? OwnerWait;
    internal OwnerCurrentCall? OwnerCurrent;
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal unsafe delegate int OwnerContractsCall(ulong handle,uint lane,byte* output,uint capacity,out uint required);
    internal OwnerContractsCall? OwnerContracts;
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal unsafe delegate int OwnerPreparedCacheCall(ulong handle,byte* input,uint length,byte* output,uint capacity,out uint required);
    internal OwnerPreparedCacheCall? OwnerPreparedCache;
    internal OwnerRawPricesCall? OwnerRawPrices;
    internal OwnerQuoteGreeksCall? OwnerQuoteGreeks, OwnerTradeGreeks;
    internal OwnerCacheCall? OwnerCache;
    internal StateLiveCall? OwnerState;

    void BindOwner(nint module)
    {
        T? Optional<T>(string name) where T : Delegate => NativeLibrary.TryGetExport(module, name, out var address)
            ? Marshal.GetDelegateForFunctionPointer<T>(address) : null;
        OwnerRetiredEvidence=Optional<OwnerRetiredEvidenceCall>("ifm_market_data_owner_retired_evidence_v1");
        OwnerReadSnapshot=Optional<OwnerBackendStatusCall>("ifm_market_data_owner_read_snapshot_v1");
        OwnerRoute=Optional<OwnerRouteCall>("ifm_market_data_owner_route_v1");
        OwnerQuery=Optional<OwnerQueryCall>("ifm_market_data_owner_query_v1");
        OwnerBackendStatus=Optional<OwnerBackendStatusCall>("ifm_market_data_owner_backend_status_v1");
        OwnerCapture=Optional<OwnerRecoveryRequestCall>("ifm_market_data_owner_capture_v1");
        OwnerRecoveryRequest=Optional<OwnerRecoveryRequestCall>("ifm_market_data_owner_recovery_request_v1");
        OwnerFeatures=Optional<OwnerFeaturesCall>("ifm_market_data_owner_features_v1")?.Invoke()??0;
        CreateOwner = Optional<HandleCall>("ifm_market_data_owner_create_v1");
        OwnerReinitialize = Optional<OwnerReinitializeCall>("ifm_market_data_owner_reinitialize_v1");
        OwnerStart = Optional<OwnerInputCall>("ifm_market_data_owner_start_v1");
        OwnerCatalog = Optional<OwnerInputCall>("ifm_market_data_owner_catalog_v1");
        OwnerCommand = Optional<OwnerInputCall>("ifm_market_data_owner_command_v1");
        OwnerFutures = Optional<OwnerFuturesCall>("ifm_market_data_owner_futures_v1");
        OwnerIdentity = Optional<OwnerIdentityCall>("ifm_market_data_owner_identity_v1");
        OwnerHealth = Optional<OwnerHealthCall>("ifm_market_data_owner_health_v1");
        OwnerFailures = Optional<OwnerFailuresCall>("ifm_market_data_owner_failures_v1");
        OwnerAck = Optional<OwnerAckCall>("ifm_market_data_owner_failure_ack_v1");
        OwnerRecover = Optional<OwnerRecoverCall>("ifm_market_data_owner_recover_v1");
        OwnerWait=Optional<OwnerWaitCall>("ifm_market_data_owner_wait_v1");
        OwnerCurrent=Optional<OwnerCurrentCall>("ifm_market_data_owner_current_v1");
        OwnerContracts=Optional<OwnerContractsCall>("ifm_market_data_owner_contracts_v1");
        OwnerPreparedCache=Optional<OwnerPreparedCacheCall>("ifm_market_data_owner_prepared_cache_v1");
        OwnerRawPrices = Optional<OwnerRawPricesCall>("ifm_market_data_owner_raw_prices_v1");
        OwnerQuoteGreeks = Optional<OwnerQuoteGreeksCall>("ifm_market_data_owner_quote_greeks_v1");
        OwnerTradeGreeks = Optional<OwnerQuoteGreeksCall>("ifm_market_data_owner_trade_greeks_v1");
        OwnerCache = Optional<OwnerCacheCall>("ifm_market_data_owner_cache_v1");
        OwnerState = Optional<StateLiveCall>("ifm_market_data_owner_state_v1");
        if (CreateOwner is not null && (Marshal.SizeOf<NativeOwnerHealth>() != 64 || Marshal.SizeOf<NativeOwnerIdentity>() != 56))
            throw new InvalidDataException("Native dataset owner health ABI layout mismatch.");
    }
}
