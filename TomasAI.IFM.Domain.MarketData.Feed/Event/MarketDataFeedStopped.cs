using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Feed.Event.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.Event.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Command.Extensions;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;

namespace TomasAI.IFM.Domain.MarketData.Feed.Event;

/// <summary>Handles the MarketDataFeedStoppedEvent message in the MarketDataFeedEventActor lifecycle.</summary>
public static class MarketDataFeedStopped
{
    static MarketDataFeedStopped()
    {
        ServiceId = $"{LogSourceType.MarketDataFeedEvent}";
    }

    static string ServiceId { get; } 
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This actor/service boundary records the exception and contains the failure according to its existing lifecycle or message contract.")]     /// <summary>
    /// 
    /// </summary>
    /// <param name="e"></param>
    /// <param name="p"></param>
    /// <returns></returns>
    public static async ValueTask<bool> ExecuteAsync(
        this MarketDataFeedStoppedEvent e,
        IEventActorContext context,
        IEventActorContext eventApi,
        MarketDataFeedEventParameters p, ILogger<MarketDataFeedEventActor> logger)
    {
        ArgumentNullException.ThrowIfNull(e);
        ArgumentNullException.ThrowIfNull(eventApi);
        ArgumentNullException.ThrowIfNull(p);
        var source = $"MarketDataFeedStoppedEvent for EntityId: {e.EntityId}";
        try
        {
            await (p.MarketDataLifecycle.StopAsync(e.ValueDate)).ConfigureAwait(false);

            await (eventApi.SendMarketDataFeedStoppedCompleteAsync(e)).ConfigureAwait(false);
            await (p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.MarketDataFeedEvent, "Market data feed stopped")).ConfigureAwait(false);
            logger.LogInformationEvent(ServiceId, "{Source}: market data feed stopped", source);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogErrorEvent(ServiceId, ex, "{Source}: market data feed stop failed", source);
            await (eventApi.SendMarketDataFeedStoppedFailAsync(e, ex)).ConfigureAwait(false);
            await (p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.MarketDataFeedEvent, MarketDataFeedStoppedEvent.ErrorCode, ex.GetErrorMessage())).ConfigureAwait(false);
        }
        return false;
    }


}
