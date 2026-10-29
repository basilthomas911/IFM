using System.Runtime.InteropServices;

namespace TomasAI.IFM.Application.MarketData.Rust.Interop;

/// <summary>
/// Integer identity, never a native object pointer. Finalization requests stop
/// only and performs no wait/join. Explicit async disposal owns draining/release.
/// </summary>
internal sealed class MarketDataRuntimeHandle : SafeHandle
{
    readonly NativeMarketDataLibrary library;

    internal MarketDataRuntimeHandle(NativeMarketDataLibrary library, ulong identity) : base(nint.Zero, true)
    {
        this.library = library;
        SetHandle(unchecked((nint)identity));
    }

    internal ulong Identity => unchecked((ulong)DangerousGetHandle());

    public override bool IsInvalid => handle == nint.Zero;

    protected override bool ReleaseHandle()
    {
        // Abandoning the managed owner is a programming error. Request stop, but
        // never pretend this proves native cleanup or free a still-used runtime.
        // The native registry retains the identity, bounded by its hard limit.
        return library.Stop(unchecked((ulong)handle)) == 0;
    }
}
