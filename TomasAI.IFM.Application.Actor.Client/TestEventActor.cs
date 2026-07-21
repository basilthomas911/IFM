using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Application.Actor;

/// <summary>
/// Represents an actor responsible for processing <see cref="TestEvent"/> messages within the actor system.
/// </summary>
/// <remarks>This actor is designed to handle <see cref="TestEvent"/> messages and operates within the context of
/// the actor system. It uses the provided <see cref="IActorSupervisor"/> for supervision and an <see
/// cref="ILogger{TCategoryName}"/> for logging.</remarks>
/// <param name="superisor"></param>
/// <param name="logger"></param>
public class TestEventActor(IActorSupervisor superisor, ILogger<TestEventActor> logger)
    : BaseEventActor<TestEventActor>(superisor, logger, new ActorMailboxId(ActorType.Event, "Test"))
{
    public const string ActorName = "TestEvent";

    static readonly Dictionary<string, Func<NatsMsg<byte[]>, IEvent>> _parseMap = new()
    {
        ["TestEvent"] = msg => msg.AsEvent<TestEvent>()!
    };

    protected override IEvent ParseMessage(IEventActorContext context, NatsMsg<byte[]> message)
    {
        IsArgumentNull.Check(context);
        var msgSubject = message.Subject.ToSubject();
        if (msgSubject is not { ActorType: ActorType.Event, Name: ActorName }
            || !_parseMap.ContainsKey(msgSubject.Verb))
            return default!;
        var @event = _parseMap[msgSubject.Verb](message);
        IsArgumentNull.Check(@event);
        @event.CheckForEmptyCommandId();
        return @event;
    }
    protected override async ValueTask ReceiveAsync(IEventActorContext context, IEvent @event)
    {
        await Task.CompletedTask;
    }

    protected override async ValueTask OnExceptionAsync(IEventActorContext context, ActorThreadId threadId, IEvent @event, Exception ex)
        => await ValueTask.CompletedTask;

}
