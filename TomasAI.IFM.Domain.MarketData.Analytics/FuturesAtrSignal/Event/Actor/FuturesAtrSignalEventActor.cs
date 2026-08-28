using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Event.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
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
    readonly IReadOnlyDictionary<Type, Func<IEvent, IFuturesAtrSignalEventContext, ILogger, ValueTask<bool>>> _receiveMap = new Dictionary<Type, Func<IEvent, IFuturesAtrSignalEventContext, ILogger, ValueTask<bool>>>()
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

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IEvent>> _parseMap = new Dictionary<string, Func<IActorMessage, IEvent>>()
    {
        [FuturesAtrSignalStartedEvent.Verb] = message => message.AsEvent<FuturesAtrSignalStartedEvent>()!,
        [FuturesAtrSignalStoppedEvent.Verb] = message => message.AsEvent<FuturesAtrSignalStoppedEvent>()!,
        [FuturesAtrSignalGeneratedCompleteEvent.Verb] = message => message.AsEvent<FuturesAtrSignalGeneratedCompleteEvent>()!,
        [FuturesAtrDailySignalGeneratedCompleteEvent.Verb] = message => message.AsEvent<FuturesAtrDailySignalGeneratedCompleteEvent>()!
    };

    /// <summary>Parses an ATR event message.</summary>
    protected override IEvent ParseMessage(IEventActorContext<FuturesAtrSignalEventActor> context, IActorMessage message)
        => ParseMappedEvent(context, message, _parseMap);

    /// <summary>Dispatches an ATR event by runtime type.</summary>
    protected override async ValueTask ReceiveAsync(IEventActorContext<FuturesAtrSignalEventActor> context, IEvent @event)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(@event);
        var handler = ResolveMappedEventHandler(@event, _receiveMap);
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
