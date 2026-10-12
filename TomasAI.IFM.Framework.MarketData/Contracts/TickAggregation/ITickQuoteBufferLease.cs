using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;

namespace TomasAI.IFM.Framework.MarketData.Contracts.TickAggregation;

public interface ITickQuoteBufferLease : IDisposable
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserves the existing array-shaped provider snapshot contract.")]
    FuturesTickQuoteData[] Buffer { get; }
    ushort Count { get; }
    void SetCount(ushort count);
}
