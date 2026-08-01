using Microsoft.Win32.SafeHandles;
using TomasAI.IFM.Framework.MarketData.DataBento;

namespace TomasAI.IFM.Framework.MarketData.DataBento.Interop;

internal sealed class SafeDbFeedHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    private SafeDbFeedHandle()
        : base(ownsHandle: true)
    {
    }

    internal SafeDbFeedHandle(nint handle)
        : base(ownsHandle: true)
    {
        SetHandle(handle);
    }

    protected override bool ReleaseHandle() =>
        NativeMethods.FeedDestroy(handle) == DatabentoFeedStatus.Ok;
}
