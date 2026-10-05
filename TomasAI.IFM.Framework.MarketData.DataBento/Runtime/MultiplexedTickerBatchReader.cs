using System.Diagnostics;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

/// <summary>Initializes a new MultiplexedTickerBatchReader instance.</summary>
/// <param name="readers">The instrument readers combined by the multiplexed reader.</param>
/// <param name="release">The callback releasing the multiplexed reader's registration.</param>
/// <param name="ready">The signal notifying the reader of available data.</param>
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

    /// <summary>Attempts to read the next available batch or current snapshot.</summary>
    /// <param name="batch">When a read succeeds, receives the next batch.</param>
    /// <returns>True when the operation succeeds or the requested condition holds; otherwise, false.</returns>
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

    /// <summary>Attempts to read the next available batch or current snapshot.</summary>
    /// <param name="timeout">The maximum time allowed for the operation.</param>
    /// <param name="batch">When a read succeeds, receives the next batch.</param>
    /// <returns>True when the operation succeeds or the requested condition holds; otherwise, false.</returns>
    public bool TryRead(TimeSpan timeout, out InstrumentBatch64 batch) =>
        TryReadCore(timeout, CancellationToken.None, out batch) == ReadResult.Success;

    /// <summary>Attempts to read the next available batch or current snapshot.</summary>
    /// <param name="timeout">The maximum time allowed for the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <param name="batch">When a read succeeds, receives the next batch.</param>
    /// <returns>True when the operation succeeds or the requested condition holds; otherwise, false.</returns>
    public bool TryRead(
        TimeSpan timeout,
        CancellationToken cancellationToken,
        out InstrumentBatch64 batch) =>
        TryReadCore(timeout, cancellationToken, out batch) == ReadResult.Success;

    /// <summary>Reads the next batch or current native watchdog snapshot.</summary>
    /// <param name="timeout">The maximum time allowed for the operation.</param>
    /// <returns>The read result.</returns>
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

    /// <summary>Releases the resources owned by this instance.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
            release();
    }
}
