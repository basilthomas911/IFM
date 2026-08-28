using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Shared.EventModelActor.Templates;

/// <summary>
/// Template for a one-way, non-durable realtime actor. Add matching concrete event
/// parsers and exact-type handlers to the empty maps.
/// </summary>
public sealed class RealtimeActorTemplate(
    IRealtimeActorContext<RealtimeActorTemplate> actorContext)
    : BaseEventActor<RealtimeActorTemplate>(actorContext, actorContext.Logger)
{
    /// <summary>Gets the realtime mailbox name.</summary>
    public const string ActorName = "RealtimeActorTemplate";

    /// <summary>Gets the typed realtime context owned by this actor.</summary>
    IRealtimeActorTemplateContext ActorContext { get; } =
        actorContext as IRealtimeActorTemplateContext
        ?? throw new ArgumentException(
            $"{nameof(actorContext)} must implement {nameof(IRealtimeActorTemplateContext)}.",
            nameof(actorContext));

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IEvent>> _parseMap =
        new Dictionary<string, Func<IActorMessage, IEvent>>(StringComparer.Ordinal);

    static readonly IReadOnlyDictionary<Type, Func<IEvent,
        IRealtimeActorTemplateContext, ValueTask>> _receiveMap =
        new Dictionary<Type, Func<IEvent, IRealtimeActorTemplateContext, ValueTask>>();

    protected override IEvent ParseMessage(
        IEventActorContext<RealtimeActorTemplate> context,
        IActorMessage message) => ParseMappedRealtimeEvent(context, message, _parseMap);

    protected override async ValueTask ReceiveAsync(
        IEventActorContext<RealtimeActorTemplate> context,
        IEvent @event)
    {
        ArgumentNullException.ThrowIfNull(context);
        var handler = ResolveMappedEventHandler(@event, _receiveMap);
        await handler(@event, ActorContext).ConfigureAwait(false);
    }

    protected override async ValueTask OnExceptionAsync(
        IEventActorContext<RealtimeActorTemplate> context,
        ActorThreadId threadId,
        IEvent @event,
        Exception exception) =>
        await exception.SendErrorEventAsync<Events.EventExceptionEvent, ActorEntityId>(
            ErrorType.EventService,
            context).ConfigureAwait(false);
}
