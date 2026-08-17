using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.StatusConsole;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Event;

/// <summary>Publishes the best-effort external notification for a durable EOD insert completion.</summary>
public static class FuturesEodDataInsertedComplete
{
    static readonly string ServiceId = $"{LogSourceType.FuturesEodDataEvent}";

    public static async ValueTask<bool> ExecuteAsync(
        this FuturesEodDataInsertedCompleteEvent @event,
        IActorMarketDataFeedEventApi eventApi,
        FuturesEodDataEventParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(eventApi);
        ArgumentNullException.ThrowIfNull(parameters);

        try
        {
            await eventApi.SendFuturesEodDataUpdatedNotifyEventAsync(@event).ConfigureAwait(false);
            return true;
        }
        catch (Exception exception)
        {
            // Notify is an external, best-effort observation boundary. A Core NATS publication failure
            // must not convert the already-completed durable insert into a failed domain operation.
            parameters.Logger.LogErrorEvent(
                ServiceId,
                exception,
                "Unable to publish futures EOD notification for {EntityId}",
                @event.EntityId);
            return false;
        }
    }
}
