using TomasAI.IFM.Framework.MarketData.DataBento.Interop;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

public sealed class MarketDataBatch64 : IDisposable
{
    private readonly MarketRecord64[] _records;
    private BoundedBatchChannel? _owner;
    private int _disposed = 1;

    internal MarketDataBatch64(int capacity)
    {
        _records = new MarketRecord64[capacity];
    }

    public int Count { get; private set; }

    public int Capacity => _records.Length;

    public ReadOnlySpan<MarketRecord64> Records
    {
        get
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
            return _records.AsSpan(0, Count);
        }
    }

    internal bool IsFull => Count == _records.Length;

    internal void BeginWrite()
    {
        _owner = null;
        Count = 0;
        Volatile.Write(ref _disposed, 0);
    }

    internal void Add(in MarketRecord64 record)
    {
        if (IsFull)
        {
            throw new InvalidOperationException("The managed market-data batch is full.");
        }
        _records[Count++] = record;
    }

    internal void PublishTo(BoundedBatchChannel owner)
    {
        _owner = owner;
    }

    internal void RetireUnpublished()
    {
        _owner = null;
        Volatile.Write(ref _disposed, 1);
        Count = 0;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }
        var owner = Interlocked.Exchange(ref _owner, null);
        if (owner is null)
        {
            throw new InvalidOperationException("An unpublished batch cannot be disposed by a consumer.");
        }
        owner.ReturnConsumerLease(this);
    }
}
