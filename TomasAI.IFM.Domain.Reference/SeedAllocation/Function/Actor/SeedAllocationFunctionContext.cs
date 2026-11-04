using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Reference.SeedAllocation.Function.Actor;

public sealed class SeedAllocationFunctionContext(IActorSupervisor supervisor)
    : FunctionActorContext(supervisor, new ActorMailboxId(ActorType.Function, SeedAllocationFunctionActor.ActorName)),
      IFunctionActorContext<SeedAllocationFunctionActor>;
