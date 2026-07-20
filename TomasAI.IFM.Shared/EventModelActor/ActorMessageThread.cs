using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>
/// Represents a pairing of an actor message and the thread associated with processing that message.
/// </summary>
/// <param name="Message">The message to be processed by the actor. Cannot be null.</param>
/// <param name="Thread">The thread responsible for processing the message. Cannot be null.</param>
public record struct ActorMessageThread(IActorMessage Message, IActorThread Thread);
