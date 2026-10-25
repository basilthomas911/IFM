using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;

namespace TomasAI.IFM.Shared.MarketData.Contracts.TickAggregation;

public interface ITickQuoteBufferPool : IDisposable
{
    ITickQuoteBufferLease Rent(ushort capacity = 64);
    ValueTask<ITickQuoteBufferLease> RentAsync(
        ushort capacity,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Rent(capacity));
    }
}
