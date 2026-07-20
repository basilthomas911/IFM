using NATS.Client.Core;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>
/// Represents information about an actor message, including the associated command and event.
/// </summary>
/// <remarks>This is a zero-allocation value type that avoids heap allocation per message.</remarks>
public readonly struct ActorMessageInfo
{
    public ActorMessageInfo(NatsMsg<byte[]> actorMessage, ICommand command)
    {
        ActorMessage = actorMessage;
        Command = command;
    }

    public ActorMessageInfo(NatsMsg<byte[]> actorMessage, IEvent @event)
    {
        ActorMessage = actorMessage;
        Event = @event;
    }

    public ActorMessageInfo(NatsMsg<byte[]> actorMessage, IQuery query)
    {
        ActorMessage = actorMessage;
        Query = query;
    }

    public NatsMsg<byte[]> ActorMessage { get; } = default;
    public ICommand Command { get; }
    public IEvent Event { get; }
    public IQuery Query { get; }
}
