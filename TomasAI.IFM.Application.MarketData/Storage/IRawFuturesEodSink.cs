using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
namespace TomasAI.IFM.Application.MarketData.Storage;

public interface IRawFuturesEodSink
{
    void Observe(FuturesMarketPriceUpdatedRealtimeEvent price);
    void Observe(FuturesSessionStatisticsUpdatedRealtimeEvent statistics);
}
