using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime;

/// <summary>Acknowledges VX snapshots already published by the worker-owned local cache.</summary>
public static class VixFuturesEodDataInserted
{

    extension(VixFuturesEodDataInsertedEvent domainEvent)
    {
        /// <summary>Reads the worker-owned snapshot without waiting for its persistence completion.</summary>
        public ValueTask ExecuteAsync(IFuturesEodDataRealtimeContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(domainEvent);
            return ValueTask.CompletedTask;
        }
    }
}
