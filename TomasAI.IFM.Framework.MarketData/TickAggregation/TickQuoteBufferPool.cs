using System.Buffers;
using System.Threading.Channels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Framework.MarketData.Contracts.TickAggregation;

namespace TomasAI.IFM.Framework.MarketData.TickAggregation;

/// <summary>
/// Owns exact-size quote buffers for one feed generation. The default constructor
/// retains the legacy shared-pool behavior for callers without a reservation.
/// </summary>
public sealed class TickQuoteBufferPool : ITickQuoteBufferPool
{
    private const long MaximumReservedBytes = 512L * 1024 * 1024;
    private readonly IReadOnlyDictionary<ushort, FixedBucket>? _fixedBuckets;
    private int _disposed;

    public TickQuoteBufferPool() { }

    /// <summary>Preallocates bounded, exact-size arrays for the generation lifetime.</summary>
    public TickQuoteBufferPool(IEnumerable<(ushort Capacity, int Slots)> reservations)
    {
        ArgumentNullException.ThrowIfNull(reservations);
        var grouped = reservations.GroupBy(static reservation => reservation.Capacity);
        var buckets = new Dictionary<ushort, FixedBucket>();
        long reservedBytes = 0;
        foreach (var group in grouped)
        {
            var slots = checked(group.Sum(static reservation => reservation.Slots));
            if (group.Key is 0 or > FuturesTickQuoteDataSegment.MaximumCount
                || slots is < 1 or > 65_536)
                throw new ArgumentOutOfRangeException(nameof(reservations));
            reservedBytes = checked(reservedBytes + (long)slots * group.Key
                * System.Runtime.CompilerServices.Unsafe.SizeOf<FuturesTickQuoteData>());
            if (reservedBytes > MaximumReservedBytes)
                throw new ArgumentOutOfRangeException(nameof(reservations),
                    "Quote-buffer reservations exceed the 512 MiB generation budget.");
            buckets.Add(group.Key, new FixedBucket(group.Key, slots));
        }
        if (buckets.Count == 0)
            throw new ArgumentException("At least one quote-buffer reservation is required.", nameof(reservations));
        _fixedBuckets = buckets;
    }

    /// <summary>Rents an immediately available slot, or uses the legacy shared pool.</summary>
    public ITickQuoteBufferLease Rent(ushort capacity = 64)
    {
        ValidateCapacity(capacity);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (_fixedBuckets is null)
            return new SharedLease(ArrayPool<FuturesTickQuoteData>.Shared.Rent(capacity));
        if (!_fixedBuckets.TryGetValue(capacity, out var bucket))
            throw new ArgumentOutOfRangeException(nameof(capacity), "No fixed quote-buffer bucket was reserved.");
        if (!bucket.Available.Reader.TryRead(out var buffer))
            throw new InvalidOperationException("The fixed quote-buffer pool is saturated; use RentAsync for backpressure.");
        return new FixedLease(bucket, buffer);
    }

    /// <summary>Waits asynchronously for a fixed slot when all are in flight.</summary>
    public async ValueTask<ITickQuoteBufferLease> RentAsync(
        ushort capacity,
        CancellationToken cancellationToken = default)
    {
        ValidateCapacity(capacity);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (_fixedBuckets is null)
            return new SharedLease(ArrayPool<FuturesTickQuoteData>.Shared.Rent(capacity));
        if (!_fixedBuckets.TryGetValue(capacity, out var bucket))
            throw new ArgumentOutOfRangeException(nameof(capacity), "No fixed quote-buffer bucket was reserved.");
        var buffer = await bucket.Available.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        return new FixedLease(bucket, buffer);
    }

    /// <summary>Retires the generation; outstanding slots remain owned until returned.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        if (_fixedBuckets is null) return;
        foreach (var bucket in _fixedBuckets.Values)
            bucket.Available.Writer.TryComplete();
    }

    private static void ValidateCapacity(ushort capacity)
    {
        if (capacity is 0 or > FuturesTickQuoteDataSegment.MaximumCount)
            throw new ArgumentOutOfRangeException(nameof(capacity));
    }

    private sealed class FixedBucket
    {
        public FixedBucket(ushort capacity, int slots)
        {
            Available = Channel.CreateBounded<FuturesTickQuoteData[]>(
                new BoundedChannelOptions(slots)
                {
                    SingleReader = false,
                    SingleWriter = false,
                    FullMode = BoundedChannelFullMode.Wait,
                    AllowSynchronousContinuations = false
                });
            for (var index = 0; index < slots; index++)
                if (!Available.Writer.TryWrite(new FuturesTickQuoteData[capacity]))
                    throw new InvalidOperationException("Quote-buffer preallocation failed.");
        }

        public Channel<FuturesTickQuoteData[]> Available { get; }
    }

    private abstract class Lease(FuturesTickQuoteData[] buffer) : ITickQuoteBufferLease
    {
        private int _returned;
        public FuturesTickQuoteData[] Buffer { get; } = buffer;
        public ushort Count { get; private set; }

        public void SetCount(ushort count)
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _returned) != 0, this);
            if (count is 0 or > FuturesTickQuoteDataSegment.MaximumCount || count > Buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(count));
            Count = count;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _returned, 1) != 0) return;
            Array.Clear(Buffer, 0, Count);
            Count = 0;
            Return(Buffer);
        }

        protected abstract void Return(FuturesTickQuoteData[] buffer);
    }

    private sealed class SharedLease(FuturesTickQuoteData[] buffer) : Lease(buffer)
    {
        protected override void Return(FuturesTickQuoteData[] buffer) =>
            ArrayPool<FuturesTickQuoteData>.Shared.Return(buffer);
    }

    private sealed class FixedLease(FixedBucket bucket, FuturesTickQuoteData[] buffer) : Lease(buffer)
    {
        protected override void Return(FuturesTickQuoteData[] buffer) =>
            bucket.Available.Writer.TryWrite(buffer);
    }
}
