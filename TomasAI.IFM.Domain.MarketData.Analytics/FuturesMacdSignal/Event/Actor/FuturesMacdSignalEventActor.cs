using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Event.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Event.Actor;

/// <summary>Parses MACD events and dispatches each supported type to its dedicated extension handler.</summary>
/// <param name="actorContext">The typed MACD event context.</param>
public class FuturesMacdSignalEventActor(IEventActorContext<FuturesMacdSignalEventActor> actorContext)
    : BaseEventActor<FuturesMacdSignalEventActor>(actorContext, actorContext.Logger)
{
    /// <summary>Identifies the MACD event mailbox.</summary>
    public const string Actor = "FuturesMacdSignalEvent";
    /// <summary>Gets the typed event context supplied to this actor.</summary>
    protected IFuturesMacdSignalEventContext FuturesMacdSignalEventContext { get; } = IsArgumentNull.Set(
        actorContext as IFuturesMacdSignalEventContext, nameof(actorContext))!;
    readonly ILogger<FuturesMacdSignalEventActor> _logger = IsArgumentNull.Set(actorContext.Logger);
    readonly IReadOnlyDictionary<Type, Func<IEvent, IFuturesMacdSignalEventContext, ILogger, ValueTask<bool>>> _receiveMap = new Dictionary<Type, Func<IEvent, IFuturesMacdSignalEventContext, ILogger, ValueTask<bool>>>()
    {
        [typeof(FuturesMacdSignalStartedEvent)] = async (@event, context, logger) =>
            await ((FuturesMacdSignalStartedEvent)@event).ExecuteAsync(context, logger).ConfigureAwait(false),
        [typeof(FuturesMacdSignalStoppedEvent)] = async (@event, context, logger) =>
            await ((FuturesMacdSignalStoppedEvent)@event).ExecuteAsync(context, logger).ConfigureAwait(false),
        [typeof(FuturesMacdSignalGeneratedCompleteEvent)] = async (@event, context, logger) =>
            await ((FuturesMacdSignalGeneratedCompleteEvent)@event).ExecuteAsync(context, logger).ConfigureAwait(false),
        [typeof(FuturesMacdDailySignalGeneratedCompleteEvent)] = async (@event, context, logger) =>
            await ((FuturesMacdDailySignalGeneratedCompleteEvent)@event).ExecuteAsync(context, logger).ConfigureAwait(false)
    };
    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IEvent>> _parseMap = new Dictionary<string, Func<IActorMessage, IEvent>>()
    {
        [FuturesMacdSignalStartedEvent.Verb] = message => message.AsEvent<FuturesMacdSignalStartedEvent>()!,
        [FuturesMacdSignalStoppedEvent.Verb] = message => message.AsEvent<FuturesMacdSignalStoppedEvent>()!,
        [FuturesMacdSignalGeneratedCompleteEvent.Verb] = message => message.AsEvent<FuturesMacdSignalGeneratedCompleteEvent>()!,
        [FuturesMacdDailySignalGeneratedCompleteEvent.Verb] = message => message.AsEvent<FuturesMacdDailySignalGeneratedCompleteEvent>()!
    };

    /// <summary>Parses a MACD event message.</summary>
    protected override IEvent ParseMessage(IEventActorContext<FuturesMacdSignalEventActor> context, IActorMessage message)
        => ParseMappedEvent(context, message, _parseMap);

    /// <summary>Dispatches a MACD event by runtime type.</summary>
    protected override async ValueTask ReceiveAsync(IEventActorContext<FuturesMacdSignalEventActor> context, IEvent @event)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(@event);
        var handler = ResolveMappedEventHandler(@event, _receiveMap);
        _ = await handler(@event, FuturesMacdSignalEventContext, _logger).ConfigureAwait(false);
    }

    /// <summary>Clears MACD observation attachments during shutdown.</summary>
    protected override ValueTask OnShutdown(IEventActorContext<FuturesMacdSignalEventActor> context)
    {
        FuturesTradeSessionBarAttachmentRegistry<FuturesMacdSignalEntityId>.Clear();
        return ValueTask.CompletedTask;
    }

    /// <summary>Publishes the standard event-actor error event.</summary>
    protected override async ValueTask OnExceptionAsync(IEventActorContext<FuturesMacdSignalEventActor> context,
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
