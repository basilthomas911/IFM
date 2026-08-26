using Microsoft.Win32.SafeHandles;

namespace TomasAI.IFM.Framework.MarketData.DataBento.Interop;

/// <summary>
/// Owns one native historical result and releases it exactly once.
/// </summary>
internal sealed class SafeHistoricalResultHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    private static int activeHandleCount;

    private SafeHistoricalResultHandle()
        : base(ownsHandle: true)
    {
    }

    internal SafeHistoricalResultHandle(nint result)
        : base(ownsHandle: true)
    {
        SetHandle(result);
        Interlocked.Increment(ref activeHandleCount);
    }

    /// <summary>
    /// Gets the number of historical results currently owned by managed code.
    /// </summary>
    internal static int ActiveHandleCount => Volatile.Read(ref activeHandleCount);

    protected override bool ReleaseHandle()
    {
        var released = NativeMethods.HistoricalResultDestroy(handle) == DatabentoFeedStatus.Ok;
        Interlocked.Decrement(ref activeHandleCount);
        return released;
    }
}
