using System.Diagnostics;
using System.Text;
using TomasAI.IFM.Framework.MarketData.DataBento.Interop;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly struct MonotonicDeadline
{
    private readonly long _started;
    private readonly TimeSpan _timeout;

    /// <summary>Initializes a new MonotonicDeadline instance.</summary>
    /// <param name="timeout">The maximum time allowed for the operation.</param>
    internal MonotonicDeadline(TimeSpan timeout)
    {
        _started = Stopwatch.GetTimestamp();
        _timeout = timeout;
    }

    internal TimeSpan Remaining
    {
        get
        {
            if (_timeout == Timeout.InfiniteTimeSpan)
            {
                return Timeout.InfiniteTimeSpan;
            }
            var remaining = _timeout - Stopwatch.GetElapsedTime(_started);
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }
    }

    internal uint RemainingMilliseconds
    {
        get
        {
            var remaining = Remaining;
            if (remaining == Timeout.InfiniteTimeSpan)
            {
                return NativeConstants.WaitInfinite;
            }
            if (remaining <= TimeSpan.Zero)
            {
                return 0;
            }
            return checked((uint)Math.Clamp(
                Math.Ceiling(remaining.TotalMilliseconds),
                1,
                uint.MaxValue - 1d));
        }
    }
}
