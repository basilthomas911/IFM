using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Domain.MarketData.YieldCurveRate.Event;
using TomasAI.IFM.Domain.MarketData.YieldCurveRate.Event.Extensions;

namespace TomasAI.IFM.Domain.MarketData.YieldCurveRate.Event.Actor;

public class YieldCurveRateEventActor(
    IEventActorContext<YieldCurveRateEventActor> actorContext)
    : BaseEventActor<YieldCurveRateEventActor>(
        actorContext,
        actorContext.YieldCurveRateContext.Logger)
{
    public const string Actor = "YieldCurveRateEvent";
    readonly IYieldCurveRateEventContext _context = actorContext.YieldCurveRateContext;
    readonly IReadOnlyDictionary<Type, Func<IEvent, IYieldCurveRateEventContext, ValueTask<bool>>> _receiveMap = new Dictionary<Type, Func<IEvent, IYieldCurveRateEventContext, ValueTask<bool>>>()
    {
        [typeof(YieldCurveRatesImportedEvent)] = (@event, context) =>
            ((YieldCurveRatesImportedEvent)@event).ExecuteAsync(context, context.ReferenceDataApi, context.DbFactory, context.Logger),
        [typeof(YieldCurveRatesImportedCompleteEvent)] = (@event, context) =>
            ((YieldCurveRatesImportedCompleteEvent)@event).ExecuteAsync(context, context.Logger),
        [typeof(YieldCurveRatesImportedFailEvent)] = (@event, context) =>
            ((YieldCurveRatesImportedFailEvent)@event).ExecuteAsync(context, context.Logger)
    };

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IEvent>> _parseMap = new Dictionary<string, Func<IActorMessage, IEvent>>()
    {
        [YieldCurveRatesImportedEvent.Verb] = message => message.AsEvent<YieldCurveRatesImportedEvent>()!,
        [YieldCurveRatesImportedCompleteEvent.Verb] = message => message.AsEvent<YieldCurveRatesImportedCompleteEvent>()!,
        [YieldCurveRatesImportedFailEvent.Verb] = message => message.AsEvent<YieldCurveRatesImportedFailEvent>()!
    };

    /// <summary>
    /// Parses an incoming NATS message and resolves it to a corresponding event based on the message
    /// subject and verb.
    /// </summary>
    /// <param name="context">The actor context used for event processing. Cannot be null.</param>
    /// <param name="message">The NATS message containing the event data to parse. Cannot be null.</param>
    /// <returns>An event object representing the parsed event corresponding to the message and verb, or <see langword="null"/> if the message subject
    /// does not correspond to a known event (indicating the message should be ignored).</returns>
    protected override IEvent ParseMessage(IEventActorContext<YieldCurveRateEventActor> context, IActorMessage message)
        => ParseMappedEvent(context, message, _parseMap);

    /// <summary>
    /// Receives an event and dispatches it to the appropriate handler based on the event's type. 
    /// If no handler is found for the event type, an <see cref="InvalidOperationException"/> is thrown.
    /// </summary>
    /// <param name="context">The event actor context in which the event is being processed.</param>
    /// <param name="event">The event to be processed.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException"></exception>
    protected override async ValueTask ReceiveAsync(IEventActorContext<YieldCurveRateEventActor> context, IEvent @event)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(@event);
        var receiveFunc = ResolveMappedEventHandler(@event, _receiveMap);
        _ = await receiveFunc.Invoke(@event, _context).ConfigureAwait(false);
    }

    /// <summary>
    /// Handles exceptions that occur during event processing by sending an error event to the event service.
    /// </summary>
    /// <param name="context">The event actor context in which the exception occurred.</param>
    /// <param name="threadId">The identifier of the actor thread where the exception was raised.</param>
    /// <param name="event">The event being processed when the exception was thrown.</param>
    /// <param name="ex">The exception that was thrown during actor processing.</param>
    /// <returns>A task that represents the asynchronous exception handling operation.</returns>
    protected override async ValueTask OnExceptionAsync(IEventActorContext<YieldCurveRateEventActor> context, ActorThreadId threadId, IEvent @event, Exception ex)
    {
        if (ex is TomasAI.IFM.Domain.MarketData.DownloadLog.DownloadLogDeliveryException
            && @event is YieldCurveRatesImportedCompleteEvent or YieldCurveRatesImportedFailEvent)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex).Throw();
        try
        {
            IsArgumentNull.Check(context);
            IsArgumentNull.Check(threadId);
            IsArgumentNull.Check(@event);
            await ex.SendErrorEventAsync<global::TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent, ActorEntityId>(ErrorType.EventService, context);
        }
        catch (Exception innerEx)
        {
            await innerEx.SendErrorEventAsync<global::TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent, ActorEntityId>(ErrorType.EventService, context);
            _context.Logger.LogError(innerEx, "Failed to send EventExceptionEvent for {Actor} actor.", Actor);
        }
    }
}
