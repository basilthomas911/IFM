using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime;

/// <summary>Acknowledges the VX source event; completion owns downstream work.</summary>
public static class VixFuturesEodDataInserted
{
    /// <summary>Completes without duplicating event-actor processing.</summary>
    public static ValueTask ExecuteAsync(this VixFuturesEodDataInsertedEvent domainEvent, IFuturesEodDataRealtimeContext context)
        => ValueTask.CompletedTask;
}