using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime;

/// <summary>Acknowledges the EOD session-statistics notification.</summary>
public static class FuturesEodSessionStatisticsUpdated
{
    /// <summary>Completes after the owning session-statistics handler has processed the observation.</summary>
    public static ValueTask ExecuteAsync(this FuturesEodSessionStatisticsUpdatedEvent domainEvent, IFuturesEodDataRealtimeContext context)
        => ValueTask.CompletedTask;
}