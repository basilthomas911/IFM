using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Event;

/// <summary>Publishes the best-effort external notification for a durable EOD insert completion.</summary>
public static class FuturesEodDataInsertedComplete
{
    static readonly string ServiceId = $"{LogSourceType.FuturesEodDataEvent}";

    public static async ValueTask<bool> ExecuteAsync(
        this FuturesEodDataInsertedCompleteEvent @event,
        IEventActorContext context,
        IActorMarketDataFeedEventApi eventApi,
        FuturesEodDataEventParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(eventApi);
        ArgumentNullException.ThrowIfNull(parameters);

        try
        {
            await eventApi.SendFuturesEodDataUpdatedNotifyEventAsync(@event).ConfigureAwait(false);
            if (string.Equals(@event.FuturesEodData.Symbol, "ES", StringComparison.Ordinal))
            {
                var entityId = new MarketOutlookEntityId(
                    @event.EntityId.ContractId,
                    @event.EntityId.ValueDate);
                await context.SendAsync<MarketOutlookEodUpdatedRealtimeEvent, MarketOutlookEntityId>(
                    new MarketOutlookEodUpdatedRealtimeEvent
                    {
                        Subject = new ActorSubject(
                            ActorType.Realtime,
                            MarketOutlookEodUpdatedRealtimeEvent.Actor,
                            MarketOutlookEodUpdatedRealtimeEvent.Verb,
                            entityId.Format()),
                        Id = Guid.NewGuid(),
                        EntityId = entityId,
                        CommandId = @event.CommandId,
                        AggregateId = @event.AggregateId ?? string.Empty,
                        EventSource = nameof(FuturesEodDataInsertedCompleteEvent),
                        ReceivedOn = DateTime.UtcNow,
                        FuturesEodData = @event.FuturesEodData
                    }).ConfigureAwait(false);
            }
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
