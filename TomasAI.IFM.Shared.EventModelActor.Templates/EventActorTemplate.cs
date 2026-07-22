using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Shared.EventModelActor.Templates;

/// <summary>
/// Template for an event actor. Add event parsers and handlers to the empty maps.
/// </summary>
public class EventActorTemplate(
    IActorSupervisor supervisor,
    ILogger<EventActorTemplate> logger)
    : BaseEventActor<EventActorTemplate>(
        supervisor,
        logger,
        new ActorMailboxId(ActorType.Event, ActorName))
{
    public const string ActorName = "EventActorTemplate";

    static readonly Dictionary<string, Func<NatsMsg<byte[]>, IEvent>> _parseMap = [];

    readonly Dictionary<string, Func<
        IEvent,
        IEventActorContext,
        ILogger,
        ValueTask<bool>>> _receiveMap = [];

    protected override IEvent ParseMessage(IEventActorContext context, NatsMsg<byte[]> message)
    {
        IsArgumentNull.Check(context);
        var subject = message.Subject.ToSubject();
        if (subject is not { ActorType: ActorType.Event, Name: ActorName }
            || !_parseMap.TryGetValue(subject.Verb, out var parser))
            return default!;

        var @event = parser.Invoke(message);
        IsArgumentNull.Check(@event);
        @event.CheckForEmptyCommandId();
        return @event;
    }

    protected override async ValueTask ReceiveAsync(IEventActorContext context, IEvent @event)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(@event);

        if (!_receiveMap.TryGetValue(@event.GetType().Name, out var handler))
            throw new InvalidOperationException(
                $"Unable to resolve {ActorName} event from message: {@event.Subject}");

        _ = await handler.Invoke(@event, context, logger);
    }

    protected override async ValueTask OnExceptionAsync(
        IEventActorContext context,
        ActorThreadId threadId,
        IEvent @event,
        Exception exception)
    {
        try
        {
            IsArgumentNull.Check(context);
            IsArgumentNull.Check(threadId);
            IsArgumentNull.Check(@event);

            await exception.SendErrorEventAsync<Events.EventExceptionEvent, ActorEntityId>(
                ErrorType.EventService,
                context);
        }
        catch (Exception innerException)
        {
            await innerException.SendErrorEventAsync<Events.EventExceptionEvent, ActorEntityId>(
                ErrorType.EventService,
                context);
            logger.LogError(
                innerException,
                "Failed to send EventExceptionEvent for {ActorName} actor.",
                ActorName);
        }
    }
}
