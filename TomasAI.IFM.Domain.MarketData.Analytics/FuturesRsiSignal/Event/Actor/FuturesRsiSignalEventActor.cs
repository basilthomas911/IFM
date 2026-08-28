using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Event.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Event.Actor;

/// <summary>Parses RSI events and dispatches each supported type to its dedicated extension handler.</summary>
/// <param name="actorContext">The typed RSI event context.</param>
public class FuturesRsiSignalEventActor(IEventActorContext<FuturesRsiSignalEventActor> actorContext)
    : BaseEventActor<FuturesRsiSignalEventActor>(actorContext, actorContext.Logger)
{
    /// <summary>Identifies the RSI event mailbox.</summary>
    public const string Actor = "FuturesRsiSignalEvent";
    /// <summary>Gets the typed event context supplied to this actor.</summary>
    protected IFuturesRsiSignalEventContext FuturesRsiSignalEventContext { get; } = IsArgumentNull.Set(
        actorContext as IFuturesRsiSignalEventContext, nameof(actorContext))!;
    readonly ILogger<FuturesRsiSignalEventActor> _logger = IsArgumentNull.Set(actorContext.Logger);
    readonly IReadOnlyDictionary<Type, Func<IEvent, IFuturesRsiSignalEventContext, ILogger, ValueTask<bool>>> _receiveMap = new Dictionary<Type, Func<IEvent, IFuturesRsiSignalEventContext, ILogger, ValueTask<bool>>>()
    {
        [typeof(FuturesRsiSignalStartedEvent)] = async (@event, context, logger) =>
            await ((FuturesRsiSignalStartedEvent)@event).ExecuteAsync(context, logger).ConfigureAwait(false),
        [typeof(FuturesRsiSignalStoppedEvent)] = async (@event, context, logger) =>
            await ((FuturesRsiSignalStoppedEvent)@event).ExecuteAsync(context, logger).ConfigureAwait(false),
        [typeof(FuturesRsiSignalGeneratedEvent)] = async (@event, context, logger) =>
            await ((FuturesRsiSignalGeneratedEvent)@event).ExecuteAsync(context, logger).ConfigureAwait(false),
        [typeof(FuturesRsiDailySignalGeneratedEvent)] = async (@event, context, logger) =>
            await ((FuturesRsiDailySignalGeneratedEvent)@event).ExecuteAsync(context, logger).ConfigureAwait(false),
        [typeof(FuturesRsiDailySignalGeneratedCompleteEvent)] = async (@event, context, logger) =>
            await ((FuturesRsiDailySignalGeneratedCompleteEvent)@event).ExecuteAsync(context, logger).ConfigureAwait(false)
    };
    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IEvent>> _parseMap = new Dictionary<string, Func<IActorMessage, IEvent>>()
    {
        [FuturesRsiSignalStartedEvent.Verb] = message => message.AsEvent<FuturesRsiSignalStartedEvent>()!,
        [FuturesRsiSignalStoppedEvent.Verb] = message => message.AsEvent<FuturesRsiSignalStoppedEvent>()!,
        [FuturesRsiSignalGeneratedEvent.Verb] = message => message.AsEvent<FuturesRsiSignalGeneratedEvent>()!,
        [FuturesRsiDailySignalGeneratedEvent.Verb] = message => message.AsEvent<FuturesRsiDailySignalGeneratedEvent>()!,
        [FuturesRsiDailySignalGeneratedCompleteEvent.Verb] = message => message.AsEvent<FuturesRsiDailySignalGeneratedCompleteEvent>()!
    };

    /// <summary>Parses an RSI event message.</summary>
    protected override IEvent ParseMessage(IEventActorContext<FuturesRsiSignalEventActor> context, IActorMessage message)
        => ParseMappedEvent(context, message, _parseMap);

    /// <summary>Dispatches an RSI event by runtime type.</summary>
    protected override async ValueTask ReceiveAsync(IEventActorContext<FuturesRsiSignalEventActor> context, IEvent @event)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(@event);
        var handler = ResolveMappedEventHandler(@event, _receiveMap);
        _ = await handler(@event, FuturesRsiSignalEventContext, _logger).ConfigureAwait(false);
    }

    /// <summary>Clears RSI observation attachments during shutdown.</summary>
    protected override ValueTask OnShutdown(IEventActorContext<FuturesRsiSignalEventActor> context)
    {
        FuturesTradeSessionBarAttachmentRegistry<FuturesRsiSignalEntityId>.Clear();
        return ValueTask.CompletedTask;
    }

    /// <summary>Publishes the standard event-actor error event.</summary>
    protected override async ValueTask OnExceptionAsync(IEventActorContext<FuturesRsiSignalEventActor> context,
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
