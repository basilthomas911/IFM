using System.Runtime.InteropServices;
namespace TomasAI.IFM.Application.MarketData.Rust.Interop;

internal sealed partial class NativeMarketDataLibrary
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int ScopeGenerationCall(ulong handle, out ulong low, out ulong high);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal unsafe delegate int ScopeSubmitCall(ulong handle, ulong operation, uint action, ulong low, ulong high, byte* input, uint length, ulong budgetMs);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal unsafe delegate int ScopeStateCall(ulong handle, byte* scope, uint scopeLength, uint instrument, uint publisher, uint size, out Delivery.LivePublication result);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal unsafe delegate int ScopePublicationsCall(ulong handle, byte* scope, uint scopeLength, Delivery.LivePublication* output, uint capacity, out uint count);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal unsafe delegate int ScopeFailureCall(ulong handle, byte* scope, uint scopeLength, uint size, out NativeControlResult result);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal unsafe delegate int ScopeStatusCall(ulong handle, byte* scope, uint scopeLength, uint size, out NativeLaneStatus result);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int ScopeStopSignalCall(ulong handle,ulong low,ulong high);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal unsafe delegate int ScopeClaimCall(ulong handle, ulong generationLow, ulong generationHigh,
        ulong ownerLow, ulong ownerHigh, byte* input, uint length, ulong ttlMs, uint size, out NativeScopeClaimRecord result);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int ScopeClaimRenewCall(ulong handle, in NativeScopeClaimRecord claim, ulong ttlMs);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int ScopeClaimReleaseCall(ulong handle, in NativeScopeClaimRecord claim);
    internal ScopeClaimCall? ScopeClaim;
    internal ScopeClaimRenewCall? ScopeClaimRenew;
    internal ScopeClaimReleaseCall? ScopeClaimRelease;
    internal ScopeStopSignalCall ScopeStopSignal=null!;
    internal ScopeGenerationCall ScopeGeneration = null!;
    internal ScopeSubmitCall ScopeSubmit = null!;
    internal ScopeStateCall ScopeState = null!;
    internal ScopePublicationsCall ScopePublications = null!;
    internal ScopeFailureCall ScopeFailure = null!;
    internal ScopeStatusCall ScopeStatus = null!;
    void BindScopes(nint module)
    {
        T Bind<T>(string name) where T : Delegate => Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(module, name));
        ScopeStopSignal=Bind<ScopeStopSignalCall>("ifm_market_data_scope_stop_signal_v1");
        ScopeGeneration = Bind<ScopeGenerationCall>("ifm_market_data_scope_generation_v1");
        ScopeSubmit = Bind<ScopeSubmitCall>("ifm_market_data_scope_submit_v1");
        ScopeState = Bind<ScopeStateCall>("ifm_market_data_scope_state_v1");
        ScopePublications = Bind<ScopePublicationsCall>("ifm_market_data_scope_publications_v1");
        ScopeFailure = Bind<ScopeFailureCall>("ifm_market_data_scope_failure_v1");
        ScopeStatus = Bind<ScopeStatusCall>("ifm_market_data_scope_status_v1");
        // Additive owned-path commands. Older qualification packages remain
        // loadable; callers must explicitly require this complete capability.
        if (NativeLibrary.TryGetExport(module, "ifm_market_data_scope_claim_v1", out var acquire)
            && NativeLibrary.TryGetExport(module, "ifm_market_data_scope_claim_renew_v1", out var renew)
            && NativeLibrary.TryGetExport(module, "ifm_market_data_scope_claim_release_v1", out var release))
        {
            ScopeClaim = Marshal.GetDelegateForFunctionPointer<ScopeClaimCall>(acquire);
            ScopeClaimRenew = Marshal.GetDelegateForFunctionPointer<ScopeClaimRenewCall>(renew);
            ScopeClaimRelease = Marshal.GetDelegateForFunctionPointer<ScopeClaimReleaseCall>(release);
        }
        BindScopePricing(module);
        BindScopeTrades(module);
    }
}

/// <summary>Copied command token with no pointer, finalizer or native ownership.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct NativeScopeClaimRecord
{
    internal uint Size, Flags;
    internal ulong GenerationLow, GenerationHigh, Serial, OwnerLow, OwnerHigh, Reserved0, Reserved1;
}
