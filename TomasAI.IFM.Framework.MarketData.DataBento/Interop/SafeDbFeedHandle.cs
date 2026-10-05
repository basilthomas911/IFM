using Microsoft.Win32.SafeHandles;
using TomasAI.IFM.Framework.MarketData.DataBento;

namespace TomasAI.IFM.Framework.MarketData.DataBento.Interop;

internal sealed class SafeDbFeedHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    /// <summary>Initializes a new SafeDbFeedHandle instance.</summary>
    private SafeDbFeedHandle()
        : base(ownsHandle: true)
    {
    }

    /// <summary>Initializes a new SafeDbFeedHandle instance.</summary>
    /// <param name="handle">The native handle whose lifetime this instance owns.</param>
    internal SafeDbFeedHandle(nint handle)
        : base(ownsHandle: true)
    {
        SetHandle(handle);
    }

    protected override bool ReleaseHandle() =>
        NativeMethods.FeedDestroy(handle) == DatabentoFeedStatus.Ok;
}
