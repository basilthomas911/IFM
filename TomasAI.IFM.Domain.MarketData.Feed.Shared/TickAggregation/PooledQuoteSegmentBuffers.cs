namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;

/// <summary>
/// Keeps exact-size decoder arrays for the process lifetime. Overflow preserves
/// market data instead of failing an actor message when all slots are in flight.
/// </summary>
public static class PooledQuoteSegmentBuffers
{
    private const int SlotLength = 512;
    private const int SlotCount = 128;
    private static readonly object Gate = new();
    private static readonly Stack<FuturesTickQuoteData[]> Available = new(SlotCount);
    private static long _overflowAllocations;

    static PooledQuoteSegmentBuffers()
    {
        for (var index = 0; index < SlotCount; index++)
            Available.Push(new FuturesTickQuoteData[SlotLength]);
    }

    /// <summary>Forces pool allocation during actor startup rather than on the first quote.</summary>
    public static void Warmup() { }

    /// <summary>Counts allocation fallbacks when all decoder slots were in flight.</summary>
    public static long OverflowAllocations => Interlocked.Read(ref _overflowAllocations);

    internal static Lease? Rent(int count)
    {
        if (count > SlotLength) return null;
        lock (Gate)
        {
            if (Available.Count != 0)
                return new Lease(Available.Pop(), count);
        }
        Interlocked.Increment(ref _overflowAllocations);
        return null;
    }

    internal sealed class Lease(FuturesTickQuoteData[] buffer, int count) : IDisposable
    {
        private int _returned;
        public FuturesTickQuoteData[] Buffer { get; } = buffer;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _returned, 1) != 0) return;
            Array.Clear(Buffer, 0, count);
            lock (Gate) Available.Push(Buffer);
        }
    }
}
