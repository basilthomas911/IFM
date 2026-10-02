using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Feed.Event.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.Event.Extensions;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Shared.StatusConsole;

namespace TomasAI.IFM.Domain.MarketData.Feed.Event;

/// <summary>Handles the MarketDataFeedResetCompleteEvent message in the MarketDataFeedEventActor lifecycle.</summary>
public static class MarketDataFeedResetComplete
{
    static MarketDataFeedResetComplete()
    {
        ServiceId = $"{LogSourceType.MarketDataFeedEvent}";
    }

    static string ServiceId { get; } = default!;

    /// <summary>
    /// Announces a completed hard recovery. The recovery pipeline has already restored and
    /// qualified worker subscriptions, actors, and durable publication; this handler must not
    /// start another legacy in-process stream or lifecycle operation.
    /// </summary>
    /// <param name="e">The correlated event emitted only after hard recovery succeeds.</param>
    /// <param name="context">The receiving actor context.</param>
    /// <param name="eventApi">The context used to publish the final reset notification.</param>
    /// <param name="p">The actor's operational notification dependencies.</param>
    /// <param name="logger">The actor logger used for notification failures.</param>
    /// <returns>Whether the final reset notification was published.</returns>
    public static async ValueTask<bool> ExecuteAsync(
        this MarketDataFeedResetCompleteEvent e,
        IEventActorContext context,
        IEventActorContext eventApi,
        MarketDataFeedEventParameters p, ILogger<MarketDataFeedEventActor> logger)
    {
        var source = $"MarketDataFeedResetCompleteEvent for EntityId: {e.EntityId}";
        try
        {
            // Recovery owns restoration; completion only notifies its consumers.
            await eventApi.SendResetStreamingEventAsync(e);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogErrorEvent(ServiceId, ex, "{Source}: data feed reset notification failed", source);
            await p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.MarketDataFeedEvent, -1, ex.GetErrorMessage());
        }
        return false;
    }

}
