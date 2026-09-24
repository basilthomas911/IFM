using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Event.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Domain.MarketData.Feed.Event.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Command.Extensions;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Event.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Event;

/// <summary>Handles stopping the periodic futures-bar insertion callback.</summary>
public static class FuturesBarDataStreamingStopped
{
    static FuturesBarDataStreamingStopped()
    {
        ServiceId = $"{LogSourceType.FuturesBarDataEvent}";
    }
    static string ServiceId { get; } = default!;

    /// <summary>Stops the bar timer and publishes a correlated completion or failure event.</summary>
    public static async ValueTask<bool> ExecuteAsync(
    this FuturesBarDataStreamingStoppedEvent e,
    IEventActorContext context,
    IEventActorContext eventApi,
    FuturesBarDataEventParameters p, ILogger<FuturesBarDataEventActor> logger)
    {
        var source = $"FuturesBarDataStreamingStoppedEvent for EntityId: {e.EntityId}";
        var stopped = false;
        try
        {
            await p.FuturesBarDataTimer.StopAsync(e.EntityId);
            await eventApi.FuturesBarDataStreamingStoppedCompleteAsync(e);
            await p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.MarketDataFeedEvent, source);
            logger.LogInformationEvent(ServiceId, "{Source}", source);
            stopped = true;
        }
        catch (Exception ex)
        {
            logger.LogErrorEvent(ServiceId, ex, "{Source}: futures bar data streaming stop failed", source);
            await eventApi.FuturesBarDataStreamingStoppedFailAsync(e, ex);
            await p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.MarketDataFeedEvent, FuturesBarDataStreamingStoppedEvent.ErrorCode, ex.GetErrorMessage());
        }
        return stopped;
    }

}
