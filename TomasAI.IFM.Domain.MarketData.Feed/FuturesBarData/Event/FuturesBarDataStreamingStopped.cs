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
    static string ServiceId { get; } 
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This actor/service boundary records the exception and contains the failure according to its existing lifecycle or message contract.")]     /// <summary>Stops the bar timer and publishes a correlated completion or failure event.</summary>
    public static async ValueTask<bool> ExecuteAsync(
    this FuturesBarDataStreamingStoppedEvent e,
    IEventActorContext context,
    IEventActorContext eventApi,
    FuturesBarDataEventParameters p, ILogger<FuturesBarDataEventActor> logger)
    {
        ArgumentNullException.ThrowIfNull(e);
        ArgumentNullException.ThrowIfNull(eventApi);
        ArgumentNullException.ThrowIfNull(p);
        var source = $"FuturesBarDataStreamingStoppedEvent for EntityId: {e.EntityId}";
        var stopped = false;
        try
        {
            await (p.FuturesBarDataTimer.StopAsync(e.EntityId)).ConfigureAwait(false);
            await (eventApi.FuturesBarDataStreamingStoppedCompleteAsync(e)).ConfigureAwait(false);
            await (p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.MarketDataFeedEvent, source)).ConfigureAwait(false);
            logger.LogInformationEvent(ServiceId, "{Source}", source);
            stopped = true;
        }
        catch (Exception ex)
        {
            logger.LogErrorEvent(ServiceId, ex, "{Source}: futures bar data streaming stop failed", source);
            await (eventApi.FuturesBarDataStreamingStoppedFailAsync(e, ex)).ConfigureAwait(false);
            await (p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.MarketDataFeedEvent, FuturesBarDataStreamingStoppedEvent.ErrorCode, ex.GetErrorMessage())).ConfigureAwait(false);
        }
        return stopped;
    }

}
