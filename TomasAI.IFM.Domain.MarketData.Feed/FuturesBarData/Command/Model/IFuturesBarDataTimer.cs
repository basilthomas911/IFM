using System.Collections.Concurrent;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Command.Model;

public interface IFuturesBarDataTimer
{
    bool IsRunning(FuturesBarDataStreamingId entityId) => false;
    bool Start(FuturesBarDataStreamingId entityId, Func<ValueTask> timerAction);
    ValueTask<bool> StopAsync(FuturesBarDataStreamingId entityId);
    ValueTask StopAllAsync();
}
