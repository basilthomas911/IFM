using System.Buffers;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Framework.MarketData.Contracts.TickAggregation;

namespace TomasAI.IFM.Framework.MarketData.TickAggregation;

public sealed class TickQuoteBufferPool : ITickQuoteBufferPool
{
    public ITickQuoteBufferLease Rent() => new Lease(ArrayPool<FuturesTickQuoteData>.Shared.Rent(64));

    private sealed class Lease(FuturesTickQuoteData[] buffer) : ITickQuoteBufferLease
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
            if (Interlocked.Exchange(ref _returned, 1) != 0)
                return;
            Array.Clear(Buffer, 0, Count);
            Count = 0;
            ArrayPool<FuturesTickQuoteData>.Shared.Return(Buffer);
        }
    }
}
