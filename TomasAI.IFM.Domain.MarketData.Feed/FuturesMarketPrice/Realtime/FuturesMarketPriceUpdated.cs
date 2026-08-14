using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesMarketPrice.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.StatusConsole;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesMarketPrice.Realtime;

/// <summary>
/// Handles the event family initiated by <see cref="FuturesMarketPriceUpdatedRealtimeEvent"/>.
/// </summary>
public static class FuturesMarketPriceUpdated
{
    /// <summary>Initializes the event-family service identifier from its dedicated log source.</summary>
    static FuturesMarketPriceUpdated()
    {
        ServiceId = $"{LogSourceType.FuturesMarketPriceUpdated}";
    }

    /// <summary>Gets the structured logging service identifier for this realtime event family.</summary>
    static string ServiceId { get; }

    /// <summary>
    /// Accepts a futures market-price realtime update. Domain behavior will be added when downstream
    /// signal realtime actors are introduced.
    /// </summary>
    /// <param name="event">The normalized realtime futures market-price update.</param>
    /// <param name="context">The realtime actor context processing the event.</param>
    /// <param name="logger">The typed primary-actor logger.</param>
    /// <returns><see langword="true"/> when the placeholder handler accepts the event.</returns>
    public static ValueTask<bool> ExecuteAsync(
        this FuturesMarketPriceUpdatedRealtimeEvent @event,
        IEventActorContext context,
        ILogger<FuturesMarketPriceRealtimeActor> logger)
    {
        IsArgumentNull.Check(logger);
        try
        {
            IsArgumentNull.Check(@event);
            IsArgumentNull.Check(context);
            return ValueTask.FromResult(true);
        }
        catch (Exception exception)
        {
            logger.LogErrorEvent(
                ServiceId,
                exception,
                "Futures market-price realtime update handling failed");
            throw;
        }
    }
}
