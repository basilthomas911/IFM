namespace TomasAI.IFM.Shared.EventModelActor.Contracts;

/// <summary>Provides the immutable runtime services owned by a Function actor.</summary>
public interface IFunctionActorContext
{
    ActorMailboxId ActorId { get; }
    IContainerInstance Container { get; }
}

/// <summary>Closed-generic Function actor context.</summary>
public interface IFunctionActorContext<TActor> : IFunctionActorContext
    where TActor : IActor;
