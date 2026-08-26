using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Event.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketSignals.Observation;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Event.Actor;

/// <summary>Parses ATR events and dispatches each supported type to its dedicated extension handler.</summary>
/// <param name="actorContext">The typed ATR event context.</param>
public class FuturesAtrSignalEventActor(IEventActorContext<FuturesAtrSignalEventActor> actorContext)
    : BaseEventActor<FuturesAtrSignalEventActor>(actorContext, actorContext.Logger)
{
    /// <summary>Identifies the ATR event mailbox.</summary>
    public const string Actor = "FuturesAtrSignalEvent";
    /// <summary>Gets the typed event context supplied to this actor.</summary>
    protected IFuturesAtrSignalEventContext FuturesAtrSignalEventContext { get; } = IsArgumentNull.Set(
        actorContext as IFuturesAtrSignalEventContext, nameof(actorContext))!;

    readonly ILogger<FuturesAtrSignalEventActor> _logger = IsArgumentNull.Set(actorContext.Logger);
    readonly Dictionary<Type, Func<IEvent, IFuturesAtrSignalEventContext, ILogger, ValueTask<bool>>> _receiveMap = new()
    {
        [typeof(FuturesAtrSignalStartedEvent)] = async (@event, context, logger) =>
            await ((FuturesAtrSignalStartedEvent)@event).ExecuteAsync(context, logger).ConfigureAwait(false),
        [typeof(FuturesAtrSignalStoppedEvent)] = async (@event, context, logger) =>
            await ((FuturesAtrSignalStoppedEvent)@event).ExecuteAsync(context, logger).ConfigureAwait(false),
        [typeof(FuturesAtrSignalGeneratedCompleteEvent)] = async (@event, context, logger) =>
            await ((FuturesAtrSignalGeneratedCompleteEvent)@event).ExecuteAsync(context, logger).ConfigureAwait(false),
        [typeof(FuturesAtrDailySignalGeneratedCompleteEvent)] = async (@event, context, logger) =>
            await ((FuturesAtrDailySignalGeneratedCompleteEvent)@event).ExecuteAsync(context, logger).ConfigureAwait(false)
    };

    static readonly Dictionary<string, Func<IActorMessage, IEvent>> _parseMap = new()
    {
        [FuturesAtrSignalStartedEvent.Verb] = message => message.AsEvent<FuturesAtrSignalStartedEvent>()!,
        [FuturesAtrSignalStoppedEvent.Verb] = message => message.AsEvent<FuturesAtrSignalStoppedEvent>()!,
        [FuturesAtrSignalGeneratedCompleteEvent.Verb] = message => message.AsEvent<FuturesAtrSignalGeneratedCompleteEvent>()!,
        [FuturesAtrDailySignalGeneratedCompleteEvent.Verb] = message => message.AsEvent<FuturesAtrDailySignalGeneratedCompleteEvent>()!
    };

    /// <summary>Parses an ATR event message.</summary>
    protected override IEvent ParseMessage(IEventActorContext<FuturesAtrSignalEventActor> context, IActorMessage message)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(message);
        if (message.Subject is not { ActorType: ActorType.Event, Name: Actor } subject
            || !_parseMap.TryGetValue(subject.Verb, out var parser)) return default!;
        var @event = parser(message);
        @event.CheckForEmptyCommandId();
        return @event;
    }

    /// <summary>Dispatches an ATR event by runtime type.</summary>
    protected override async ValueTask ReceiveAsync(IEventActorContext<FuturesAtrSignalEventActor> context, IEvent @event)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(@event);
        if (!_receiveMap.TryGetValue(@event.GetType(), out var handler))
            throw new InvalidOperationException($"Unable to resolve {Actor} event from message: {@event.Subject}");
        _ = await handler(@event, FuturesAtrSignalEventContext, _logger).ConfigureAwait(false);
    }

    /// <summary>Clears ATR observation attachments during shutdown.</summary>
    protected override ValueTask OnShutdown(IEventActorContext<FuturesAtrSignalEventActor> context)
    {
        FuturesTradeSessionBarAttachmentRegistry<FuturesAtrSignalEntityId>.Clear();
        return ValueTask.CompletedTask;
    }

    /// <summary>Publishes the standard event-actor error event.</summary>
    protected override async ValueTask OnExceptionAsync(IEventActorContext<FuturesAtrSignalEventActor> context,
        ActorThreadId threadId, IEvent @event, Exception exception)
    {
        try
        {
            IsArgumentNull.Check(context);
            IsArgumentNull.Check(threadId);
            IsArgumentNull.Check(@event);
            await exception.SendErrorEventAsync<TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent,
                ActorEntityId>(ErrorType.EventService, context).ConfigureAwait(false);
        }
        catch (Exception innerException)
        {
            await innerException.SendErrorEventAsync<TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent,
                ActorEntityId>(ErrorType.EventService, context).ConfigureAwait(false);
            Context.Logger.LogError(innerException, "Failed to send EventExceptionEvent for {Actor} actor.", Actor);
        }
    }
}
