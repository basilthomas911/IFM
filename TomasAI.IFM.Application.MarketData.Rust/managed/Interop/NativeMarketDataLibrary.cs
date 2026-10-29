using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace TomasAI.IFM.Application.MarketData.Rust.Interop;

/// <summary>
/// Loads only the new library by explicit absolute path. Modules stay loaded for
/// process lifetime, including after a failed disposal; native work must never
/// execute against unloaded code. The legacy Databento resolver is not involved.
/// </summary>
internal sealed partial class NativeMarketDataLibrary
{
    static readonly ConcurrentDictionary<string, Lazy<NativeMarketDataLibrary>> Loaded = new(StringComparer.OrdinalIgnoreCase);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate uint VersionCall();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int CreateCall(uint abi, uint size, uint threads, out ulong handle);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int SubmitCall(ulong handle, ulong operation, double input, ulong budgetMs);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int CancelCall(ulong handle, ulong operation);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int HandleCall(ulong handle);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int WaitCall(ulong handle, out uint available);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal unsafe delegate int DrainCall(ulong handle, NativeCompletion* records, uint capacity, out uint count);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int SubmitPricingCall(ulong handle, ulong operation, in NativePricingRequest request, ulong budgetMs);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int TakePricingCall(ulong handle, ulong operation, uint size, out NativePricingResult result);

    internal readonly CreateCall Create;
    internal readonly SubmitCall Submit;
    internal readonly CancelCall Cancel;
    internal readonly HandleCall Stop;
    internal readonly HandleCall Release;
    internal readonly WaitCall Wait;
    internal readonly DrainCall Drain;
    internal readonly SubmitPricingCall SubmitPricing;
    internal readonly TakePricingCall TakePricing;

    unsafe NativeMarketDataLibrary(string path)
    {
        // Deliberately never Free this module: even construction/ABI failures
        // must not unload code another instance could already be executing.
        var module = NativeLibrary.Load(path);
        T Bind<T>(string symbol) where T : Delegate => Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(module, symbol));
        if (Bind<VersionCall>("ifm_market_data_get_abi_version")() != 1 || Marshal.SizeOf<NativeCompletion>() != 32)
            throw new InvalidDataException("Rust MarketData ABI version/layout mismatch.");
        Create = Bind<CreateCall>("ifm_market_data_runtime_create_v1");
        Submit = Bind<SubmitCall>("ifm_market_data_submit_treasury_v1");
        Cancel = Bind<CancelCall>("ifm_market_data_runtime_cancel_v1");
        Stop = Bind<HandleCall>("ifm_market_data_runtime_stop_v1");
        Release = Bind<HandleCall>("ifm_market_data_runtime_release_v1");
        Wait = Bind<WaitCall>("ifm_market_data_runtime_wait_v1");
        Drain = Bind<DrainCall>("ifm_market_data_runtime_drain_v1");
        if(Marshal.SizeOf<NativePricingRequest>()!=80 || Marshal.SizeOf<NativePricingResult>()!=64) throw new InvalidDataException("Rust pricing ABI layout mismatch.");
        SubmitPricing=Bind<SubmitPricingCall>("ifm_market_data_submit_pricing_v1");
        TakePricing=Bind<TakePricingCall>("ifm_market_data_take_pricing_v1");
        BindLive(module);
        BindCatalog(module);
        BindQuoteQualification(module);
        BindScopes(module);
        BindReference(module);
        BindHistorical(module);
        BindOwner(module);
    }

    internal static NativeMarketDataLibrary Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!Path.IsPathFullyQualified(path)) throw new ArgumentException("Native library path must be absolute.", nameof(path));
        return Loaded.GetOrAdd(Path.GetFullPath(path), static name => new(() => new(name), LazyThreadSafetyMode.ExecutionAndPublication)).Value;
    }

    internal static void Check(int status, string operation)
    {
        if (status != 0) throw new InvalidOperationException($"Rust MarketData {operation} failed (ABI status {status}).");
    }
}

/// <summary>Blittable copy contract; no native pointer or borrowed payload.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct NativeCompletion
{
    internal uint Size;
    internal int Outcome;
    internal ulong Operation;
    internal double Value;
    internal ulong Reserved;
}
