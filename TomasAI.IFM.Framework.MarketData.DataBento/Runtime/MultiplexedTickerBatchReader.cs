using System.Diagnostics;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

internal sealed class MultiplexedTickerBatchReader(
    IReadOnlyList<(InstrumentKey Instrument, ISynchronousBatchReader<MarketDataBatch64> Reader)> readers,
    Action release,
    SemaphoreSlim? ready = null) : IMultiplexedTickerBatchReader
{
    private enum ReadResult : byte
    {
        Success,
        TimedOut,
        Completed
    }

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

    public bool TryRead(TimeSpan timeout, out InstrumentBatch64 batch) =>
        TryReadCore(timeout, CancellationToken.None, out batch) == ReadResult.Success;

    public bool TryRead(
        TimeSpan timeout,
        CancellationToken cancellationToken,
        out InstrumentBatch64 batch) =>
        TryReadCore(timeout, cancellationToken, out batch) == ReadResult.Success;

    public InstrumentBatch64 Read(TimeSpan timeout)
    {
        return TryReadCore(timeout, CancellationToken.None, out var batch) switch
        {
            ReadResult.Success => batch,
            ReadResult.Completed => throw new EndOfStreamException(
                "All ticker channels completed."),
            _ => throw new TimeoutException(
                "No ticker batch was available before the deadline.")
        };
    }

    private ReadResult TryReadCore(
        TimeSpan timeout,
        CancellationToken cancellationToken,
        out InstrumentBatch64 batch)
    {
        if (timeout < TimeSpan.Zero && timeout != Timeout.InfiniteTimeSpan)
            throw new ArgumentOutOfRangeException(nameof(timeout));
        var started = Stopwatch.GetTimestamp();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (TryRead(out var leased))
            {
                batch = leased;
                return ReadResult.Success;
            }
            if (IsCompleted)
            {
                batch = default;
                return ReadResult.Completed;
            }
            if (timeout == TimeSpan.Zero ||
                (timeout != Timeout.InfiniteTimeSpan && Stopwatch.GetElapsedTime(started) >= timeout))
            {
                batch = default;
                return ReadResult.TimedOut;
            }
            if (ready is null)
            {
                Thread.Yield();
                continue;
            }
            var remaining = timeout == Timeout.InfiniteTimeSpan
                ? Timeout.InfiniteTimeSpan
                : timeout - Stopwatch.GetElapsedTime(started);
            if (remaining <= TimeSpan.Zero || !ready.Wait(remaining, cancellationToken))
            {
                batch = default;
                return ReadResult.TimedOut;
            }
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
            release();
    }
}
