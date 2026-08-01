namespace TomasAI.IFM.Framework.MarketData.DataBento;

internal sealed class BatchPool
{
    private readonly object _gate = new();
    private readonly MarketDataBatch64[] _available;
    private int _count;
    private ulong _misses;

    internal BatchPool(int batchCount, int recordsPerBatch)
    {
        _available = new MarketDataBatch64[batchCount];
        for (var index = 0; index < batchCount; index++)
        {
            _available[index] = new MarketDataBatch64(recordsPerBatch);
        }
        _count = batchCount;
    }

    internal ulong Misses
    {
        get
        {
            lock (_gate)
            {
                return _misses;
            }
        }
    }

    internal MarketDataBatch64 Rent(Func<bool> isStopping)
    {
        lock (_gate)
        {
            while (_count == 0)
            {
                _misses++;
                if (isStopping())
                {
                    throw new OperationCanceledException("Feed stopped while waiting for a batch lease.");
                }
                Monitor.Wait(_gate);
            }
            var batch = _available[--_count];
            _available[_count] = null!;
            batch.BeginWrite();
            return batch;
        }
    }

    internal void Return(MarketDataBatch64 batch)
    {
        batch.RetireUnpublished();
        lock (_gate)
        {
            if (_count == _available.Length)
            {
                throw new InvalidOperationException("A managed batch lease was returned twice.");
            }
            _available[_count++] = batch;
            Monitor.PulseAll(_gate);
        }
    }

    internal void WakeAll()
    {
        lock (_gate)
        {
            Monitor.PulseAll(_gate);
        }
    }
}
