using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Application.Actor.Client.Extensions;

namespace TomasAI.IFM.Application.Actor.Client;

/// <summary>
/// Represents an actor responsible for processing <see cref="TestEvent"/> messages within the actor system.
/// </summary>
/// <remarks>This actor is designed to handle <see cref="TestEvent"/> messages and operates within the context of
/// the actor system. It uses the provided <see cref="IActorSupervisor"/> for supervision and an <see
/// cref="ILogger{TCategoryName}"/> for logging.</remarks>
/// <param name="actorContext">The typed event context resolved through open-generic registration.</param>
public class TestEventActor(IEventActorContext<TestEventActor> actorContext)
    : BaseEventActor<TestEventActor>(actorContext, actorContext.Logger)
{
    /// <summary>Gets the typed context owned by this actor.</summary>
    protected ITestEventContext ActorContext =>
        IsArgumentNull.Set(Context as ITestEventContext, nameof(Context))!;

    /// <summary>Gets the event name parsed by this actor.</summary>
    public const string ActorName = "TestEvent";

    /// <summary>Gets the actor mailbox name retained for compatibility with the existing sample protocol.</summary>
    public const string MailboxName = "Test";

    static readonly Dictionary<string, Func<IActorMessage, IEvent>> _parseMap = new()
    {
        ["TestEvent"] = msg => msg.AsEvent<TestEvent>()!
    };

    protected override IEvent ParseMessage(IEventActorContext<TestEventActor> context, IActorMessage message)
    {
        IsArgumentNull.Check(context);
        var msgSubject = message.Subject;
        if (msgSubject is not { ActorType: ActorType.Event, Name: ActorName }
            || !_parseMap.ContainsKey(msgSubject.Verb))
            return default!;
        var @event = _parseMap[msgSubject.Verb](message);
        IsArgumentNull.Check(@event);
        @event.CheckForEmptyCommandId();
        return @event;
    }
    protected override async ValueTask ReceiveAsync(IEventActorContext<TestEventActor> context, IEvent @event)
    {
        await Task.CompletedTask;
    }

    protected override async ValueTask OnExceptionAsync(IEventActorContext<TestEventActor> context, ActorThreadId threadId, IEvent @event, Exception ex)
        => await ValueTask.CompletedTask;

}
