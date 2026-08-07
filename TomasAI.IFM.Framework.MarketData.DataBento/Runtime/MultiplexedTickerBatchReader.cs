using System.Diagnostics;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

internal sealed class MultiplexedTickerBatchReader(
    IReadOnlyList<(InstrumentKey Instrument, ISynchronousBatchReader<MarketDataBatch64> Reader)> readers,
    Action release,
    SemaphoreSlim? ready = null) : IMultiplexedTickerBatchReader
{
    private int _next;
    private int _disposed;

    public bool IsCompleted
    {
        get
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
            for (var index = 0; index < readers.Count; index++)
                if (!readers[index].Reader.IsCompleted)
                    return false;
            return true;
        }
    }

    public bool TryRead(out InstrumentBatch64 batch)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        for (var offset = 0; offset < readers.Count; offset++)
        {
            var index = (_next + offset) % readers.Count;
            var entry = readers[index];
            if (!entry.Reader.TryRead(out var leased))
                continue;
            _next = (index + 1) % readers.Count;
            ready?.Wait(0);
            batch = new InstrumentBatch64(entry.Instrument, leased!);
            return true;
        }
        batch = default;
        return false;
    }

    public InstrumentBatch64 Read(TimeSpan timeout)
    {
        if (timeout < TimeSpan.Zero && timeout != Timeout.InfiniteTimeSpan)
            throw new ArgumentOutOfRangeException(nameof(timeout));
        var started = Stopwatch.GetTimestamp();
        while (true)
        {
            if (TryRead(out var batch))
                return batch;
            if (IsCompleted)
                throw new EndOfStreamException("All ticker channels completed.");
            if (timeout == TimeSpan.Zero ||
                (timeout != Timeout.InfiniteTimeSpan && Stopwatch.GetElapsedTime(started) >= timeout))
                throw new TimeoutException("No ticker batch was available before the deadline.");
            if (ready is null)
            {
                Thread.Yield();
                continue;
            }
            var remaining = timeout == Timeout.InfiniteTimeSpan
                ? Timeout.InfiniteTimeSpan
                : timeout - Stopwatch.GetElapsedTime(started);
            if (remaining <= TimeSpan.Zero || !ready.Wait(remaining))
                throw new TimeoutException("No ticker batch was available before the deadline.");
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
            release();
    }
}
