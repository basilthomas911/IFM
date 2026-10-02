namespace TomasAI.IFM.Application.MarketData.Databento.Resiliency;

public sealed record DatabentoRecoveryHoldingSnapshot(int Capacity, int Count,
    long Accepted, long Dropped);

/// <summary>Holds a finite number of locally qualified records while downstream admission is fenced.</summary>
public sealed class DatabentoRecoveryHoldingBuffer<T>
{
    readonly object gate = new();
    readonly Queue<T> queue;
    readonly int capacity;
    long accepted;
    long dropped;

    public DatabentoRecoveryHoldingBuffer(int capacity)
    {
        if (capacity is < 1 or > 1_000_000)
            throw new ArgumentOutOfRangeException(nameof(capacity));
        this.capacity = capacity;
        queue = new Queue<T>(Math.Min(capacity, 1024));
    }

    public bool TryHold(T item)
    {
        lock (gate)
        {
            if (queue.Count == capacity)
            {
                dropped++;
                return false;
            }
            queue.Enqueue(item);
            accepted++;
            return true;
        }
    }

    public bool TryTake(out T item)
    {
        lock (gate)
        {
            if (queue.Count == 0)
            {
                item = default!;
                return false;
            }
            item = queue.Dequeue();
            return true;
        }
    }

    /// <summary>Takes the first matching item without discarding or reordering the others.</summary>
    public bool TryTakeWhere(Func<T, bool> predicate, out T item)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        lock (gate)
        {
            var matchIndex = -1;
            var scanned = 0;
            foreach (var candidate in queue)
            {
                if (predicate(candidate)) { matchIndex = scanned; break; }
                scanned++;
            }
            item = default!;
            if (matchIndex < 0) return false;
            var count = queue.Count;
            for (var index = 0; index < count; index++)
            {
                var candidate = queue.Dequeue();
                if (index == matchIndex) item = candidate;
                else queue.Enqueue(candidate);
            }
            return true;
        }
    }

    public DatabentoRecoveryHoldingSnapshot Capture()
    {
        lock (gate) return new(capacity, queue.Count, accepted, dropped);
    }
}
