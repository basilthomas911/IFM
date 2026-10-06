using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
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
    /// Forwards a qualifying trade-driven market-price update to tick storage and EOD. Quote and VWAP
    /// checkpoint notifications do not generate trade changes.
    /// </summary>
    /// <param name="event">The normalized realtime futures market-price update.</param>
    /// <param name="context">The realtime actor context processing the event.</param>
    /// <param name="logger">The typed primary-actor logger.</param>
    /// <returns><see langword="true"/> when the event has been handled.</returns>
    public static async ValueTask<bool> ExecuteAsync(
        this FuturesMarketPriceUpdatedRealtimeEvent @event,
        IEventActorContext context,
        ILogger<FuturesMarketPriceRealtimeActor> logger)
    {
        IsArgumentNull.Check(logger);
        try
        {
            IsArgumentNull.Check(@event);
            IsArgumentNull.Check(context);
            if (@event.UpdateSource == FuturesMarketPriceUpdateSource.Trade
                && @event.VwapCheckpoint is null
                && @event.Subject.Name == FuturesMarketPriceUpdatedRealtimeEvent.Actor
                && @event.SourceTrade is { } trade)
            {
                await context.SendAsync<FuturesTickTradeDataChangedEvent, TickDataEntityId>(
                    trade with
                    {
                        SourceDataset = @event.SourceDataset,
                        SourceGenerationId = @event.SourceGenerationId
                    }).ConfigureAwait(false);
            }
            return true;
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
