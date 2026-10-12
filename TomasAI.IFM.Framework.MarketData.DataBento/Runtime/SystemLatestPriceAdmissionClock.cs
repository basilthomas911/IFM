using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using TomasAI.IFM.Framework.MarketData.DataBento.Interop;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

internal sealed class SystemLatestPriceAdmissionClock : ILatestPriceAdmissionClock
{
    internal static SystemLatestPriceAdmissionClock Instance { get; } = new();

    /// <summary>Initializes a new SystemLatestPriceAdmissionClock instance.</summary>
    private SystemLatestPriceAdmissionClock()
    {
    }

    /// <summary>Reads the monotonic clock timestamp.</summary>
    /// <returns>The timestamp result.</returns>
    public long GetTimestamp() => Stopwatch.GetTimestamp();

    /// <summary>Converts two monotonic timestamps into an elapsed duration.</summary>
    /// <param name="startingTimestamp">The monotonic timestamp at the beginning of the interval.</param>
    /// <param name="endingTimestamp">The monotonic timestamp at the end of the interval.</param>
    /// <returns>The elapsed time result.</returns>
    public TimeSpan GetElapsedTime(long startingTimestamp, long endingTimestamp) =>
        Stopwatch.GetElapsedTime(startingTimestamp, endingTimestamp);

    /// <summary>Waits for the admission synchronization gate for the specified duration.</summary>
    /// <param name="gate">The synchronization gate protecting the admission state.</param>
    /// <param name="timeout">The maximum time allowed for the operation.</param>
    public void Wait(object gate, TimeSpan timeout) => Monitor.Wait(gate, timeout);
}
