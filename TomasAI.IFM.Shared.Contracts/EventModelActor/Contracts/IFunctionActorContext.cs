namespace TomasAI.IFM.Shared.EventModelActor.Contracts;

/// <summary>Provides the immutable runtime services owned by a Function actor.</summary>
public interface IFunctionActorContext
{
    ActorMailboxId ActorId { get; }
    IContainerInstance Container { get; }
    ISupervisorRuntimeContext SupervisorRuntime => null!;
}

/// <summary>Closed-generic Function actor context.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2326", Justification = "The invariant generic argument identifies the exact actor, state, result or handler contract in dependency registration and dispatch.")]
public interface IFunctionActorContext<TActor> : IFunctionActorContext
    where TActor : IActor;
