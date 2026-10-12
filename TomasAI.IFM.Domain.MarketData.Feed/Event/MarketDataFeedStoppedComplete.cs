using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Feed.Event.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Shared.StatusConsole;

namespace TomasAI.IFM.Domain.MarketData.Feed.Event;

/// <summary>Handles the MarketDataFeedStoppedCompleteEvent message in the MarketDataFeedEventActor lifecycle.</summary>
public static class MarketDataFeedStoppedComplete
{
    static MarketDataFeedStoppedComplete()
    {
        ServiceId = $"{LogSourceType.MarketDataFeedEvent}";
    }

    static string ServiceId { get; } 
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This actor/service boundary records the exception and contains the failure according to its existing lifecycle or message contract.")]     /// <summary>
    /// 
    /// </summary>
    /// <param name="e"></param>
    /// <param name="statusConsoleWriter"></param>
    /// <param name="logger"></param>
    /// <returns></returns>
    public static async ValueTask<bool> ExecuteAsync(
        this MarketDataFeedStoppedCompleteEvent e, IEventActorContext context, MarketDataFeedEventParameters p, ILogger<MarketDataFeedEventActor> logger)
    {
        ArgumentNullException.ThrowIfNull(e);
        ArgumentNullException.ThrowIfNull(p);
        var source = $"MarketDataFeedStoppedCompleteEvent for EntityId: {e.EntityId}";
        try
        {
            await (p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.MarketDataFeedEvent, "Futures bar data streaming stopped")).ConfigureAwait(false);
            logger.LogInformationEvent(ServiceId, "{Source}: futures bar data streaming stopped", source);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogErrorEvent(ServiceId, ex, "{Source}: stopping futures bar data feed failed", source);
            await (p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.MarketDataFeedEvent, MarketDataFeedStoppedEvent.ErrorCode, ex.GetErrorMessage())).ConfigureAwait(false);
        }
        return false;
    }
}
