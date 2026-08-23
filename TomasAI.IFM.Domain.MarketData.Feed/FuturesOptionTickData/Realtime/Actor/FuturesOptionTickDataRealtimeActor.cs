using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Realtime.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Event.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Command.Extensions;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Realtime.Actor;

/// <summary>Consumes the futures-option branch of normalized live trades over Core NATS.</summary>
public class FuturesOptionTickDataRealtimeActor(IRealtimeActorContext<FuturesOptionTickDataRealtimeActor> actorContext)
    : BaseEventActor<FuturesOptionTickDataRealtimeActor>(actorContext.Supervisor, actorContext.Logger, actorContext.ActorId)
{
    public const string ActorName = "FuturesOptionTickDataRealtime";

    /// <summary>Gets the typed realtime context supplied at construction.</summary>
    protected IFuturesOptionTickDataRealtimeContext RealtimeContext { get; } = IsArgumentNull.Set(actorContext as IFuturesOptionTickDataRealtimeContext, nameof(actorContext))!;

    static readonly ActorTypeId TickTradeRoute = new(
        ActorType.Realtime,
        FuturesTickTradeDataInsertedEvent.Actor,
        FuturesTickTradeDataInsertedEvent.Verb);

    protected override ValueTask OnStartup(IEventActorContext context)
    {
        context.AddRealtimeRouter(TickTradeRoute, Id);
        return ValueTask.CompletedTask;
    }

    protected override ValueTask OnShutdown(IEventActorContext context)
    {
        context.RemoveRealtimeRouter(TickTradeRoute, Id);
        return ValueTask.CompletedTask;
    }

    protected override IEvent ParseMessage(
        IEventActorContext context,
        IActorMessage message)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(message);
        if (message.Subject is not
            {
                ActorType: ActorType.Realtime,
                Name: ActorName,
                Verb: FuturesTickTradeDataInsertedEvent.Verb
            })
            return default!;

        var domainEvent = message.AsEvent<FuturesTickTradeDataInsertedEvent>();
        ArgumentNullException.ThrowIfNull(domainEvent);
        domainEvent.CheckForEmptyCommandId();
        return domainEvent;
    }

    protected override async ValueTask ReceiveAsync(
        IEventActorContext context,
        IEvent domainEvent)
    {
        if (domainEvent is not FuturesTickTradeDataInsertedEvent trade)
        {
            throw new InvalidOperationException(
                $"Unable to resolve {ActorName} realtime event from {domainEvent.Subject}.");
        }

        _ = await trade.ExecuteAsync(
                RealtimeContext,
                ((IFuturesOptionTickDataRealtimeContext)actorContext).MarketDataApi,
                ((IFuturesOptionTickDataRealtimeContext)actorContext).StatusConsoleWriter,
                actorContext.Logger)
            .ConfigureAwait(false);
    }

    protected override async ValueTask OnExceptionAsync(
        IEventActorContext context,
        ActorThreadId threadId,
        IEvent domainEvent,
        Exception exception) =>
        await exception.SendErrorEventAsync<
            TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent,
            ActorEntityId>(ErrorType.EventService, context).ConfigureAwait(false);
}
