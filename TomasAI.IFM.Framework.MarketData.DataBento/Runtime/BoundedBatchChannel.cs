using System.Diagnostics;
using TomasAI.IFM.Framework.MarketData.DataBento.Interop;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

internal sealed class BoundedBatchChannel : ISynchronousBatchReader<MarketDataBatch64>
{
    private readonly object _gate = new();
    private readonly MarketDataBatch64?[] _slots;
    private readonly BatchPool _pool;
    private readonly Action? _signalReader;
    private int _head;
    private int _tail;
    private int _count;
    private bool _consumerLeaseOutstanding;
    private bool _completed;
    private Exception? _completionError;
    private ulong _fullCount;
    private long _maximumFullWaitTicks;

    internal BoundedBatchChannel(
        int channelBatchSlots,
        int batchRecordCapacity,
        Action? signalReader = null)
    {
        _slots = new MarketDataBatch64[channelBatchSlots];
        _pool = new BatchPool(channelBatchSlots + 2, batchRecordCapacity);
        _signalReader = signalReader;
    }

    internal int Count
    {
        get
        {
            lock (_gate)
            {
                return _count;
            }
        }
    }

    internal ulong FullCount
    {
        get
        {
            lock (_gate)
            {
                return _fullCount;
            }
        }
    }

    internal ulong PoolMisses => _pool.Misses;

    internal int Capacity => _slots.Length;

    internal int PoolCapacity => _pool.Capacity;

    internal int PoolFreeCount => _pool.FreeCount;

    internal TimeSpan MaximumFullWait => TimeSpan.FromTicks(
        Interlocked.Read(ref _maximumFullWaitTicks));

    public bool IsCompleted
    {
        get
        {
            lock (_gate)
            {
                return _completed && _count == 0 && !_consumerLeaseOutstanding;
            }
        }
    }

    internal MarketDataBatch64 RentBatch(Func<bool> isStopping) => _pool.Rent(isStopping);

    internal bool Publish(MarketDataBatch64 batch, Func<bool> isStopping)
    {
        lock (_gate)
        {
            var waitStarted = 0L;
            while (_count == _slots.Length && !_completed)
            {
                _fullCount++;
                waitStarted = waitStarted == 0 ? Stopwatch.GetTimestamp() : waitStarted;
                if (isStopping())
                {
                    return false;
                }
                Monitor.Wait(_gate);
            }
            if (waitStarted != 0)
            {
                UpdateMaximumFullWait(Stopwatch.GetElapsedTime(waitStarted));
            }
            if (_completed || isStopping())
            {
                return false;
            }
            batch.PublishTo(this);
            _slots[_head] = batch;
            _head = (_head + 1) % _slots.Length;
            _count++;
            Monitor.PulseAll(_gate);
        }
        _signalReader?.Invoke();
        return true;
    }

    private void UpdateMaximumFullWait(TimeSpan elapsed)
    {
        var candidate = elapsed.Ticks;
        var observed = Interlocked.Read(ref _maximumFullWaitTicks);
        while (candidate > observed)
        {
            var prior = Interlocked.CompareExchange(
                ref _maximumFullWaitTicks,
                candidate,
                observed);
            if (prior == observed)
            {
                return;
            }
            observed = prior;
        }
    }

    public bool TryRead(out MarketDataBatch64? batch)
    {
        lock (_gate)
        {
            ThrowIfLeaseOutstanding();
            if (_count == 0)
            {
                ThrowIfTerminalError();
                batch = null;
                return false;
            }
            batch = TakeBatch();
            return true;
        }
    }

    public MarketDataBatch64 Read(TimeSpan timeout)
    {
        if (timeout < TimeSpan.Zero && timeout != Timeout.InfiniteTimeSpan)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }
        lock (_gate)
        {
            ThrowIfLeaseOutstanding();
            var started = Stopwatch.GetTimestamp();
            while (_count == 0 && !_completed)
            {
                if (timeout == TimeSpan.Zero)
                {
                    throw new TimeoutException("No market-data batch was available before the deadline.");
                }
                if (timeout == Timeout.InfiniteTimeSpan)
                {
                    Monitor.Wait(_gate);
                    continue;
                }
                var remaining = timeout - Stopwatch.GetElapsedTime(started);
                if (remaining <= TimeSpan.Zero || !Monitor.Wait(_gate, remaining))
                {
                    throw new TimeoutException("No market-data batch was available before the deadline.");
                }
            }
            if (_count == 0)
            {
                ThrowIfTerminalError();
                throw new EndOfStreamException("The market-data feed completed.");
            }
            return TakeBatch();
        }
    }

    internal void Complete(Exception? error = null)
    {
        var completed = false;
        lock (_gate)
        {
            if (_completed)
            {
                return;
            }
            _completed = true;
            _completionError = error;
            completed = true;
            Monitor.PulseAll(_gate);
        }
        _pool.WakeAll();
        if (completed)
            _signalReader?.Invoke();
    }

    internal void ReturnUnpublished(MarketDataBatch64 batch) => _pool.Return(batch);

    internal void ReturnConsumerLease(MarketDataBatch64 batch)
    {
        lock (_gate)
        {
            if (!_consumerLeaseOutstanding)
            {
                throw new InvalidOperationException("No consumer lease is outstanding for this channel.");
            }
            _consumerLeaseOutstanding = false;
            Monitor.PulseAll(_gate);
        }
        _pool.Return(batch);
    }

    internal void DrainUnread()
    {
        while (true)
        {
            MarketDataBatch64? batch;
            lock (_gate)
            {
                if (_count == 0)
                {
                    return;
                }
                batch = _slots[_tail];
                _slots[_tail] = null;
                _tail = (_tail + 1) % _slots.Length;
                _count--;
                Monitor.PulseAll(_gate);
            }
            _pool.Return(batch!);
        }
    }

    private MarketDataBatch64 TakeBatch()
    {
        var batch = _slots[_tail]!;
        _slots[_tail] = null;
        _tail = (_tail + 1) % _slots.Length;
        _count--;
        _consumerLeaseOutstanding = true;
        Monitor.PulseAll(_gate);
        return batch;
    }

    private void ThrowIfLeaseOutstanding()
    {
        if (_consumerLeaseOutstanding)
        {
            throw new InvalidOperationException(
                "Dispose the current market-data batch before reading the next batch.");
        }
    }

    private void ThrowIfTerminalError()
    {
        if (_completed && _completionError is not null)
        {
            throw new DatabentoFeedException(
                DatabentoFeedStatus.InternalError,
                "The managed market-data channel completed with an error: "
                + _completionError.Message);
        }
    }
}
