using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Portfolio.Identity.PortfolioBusinessIdAllocation.Function.Actor;

public sealed class PortfolioBusinessIdAllocationFunctionContext(IActorSupervisor supervisor)
    : FunctionActorContext(supervisor, new ActorMailboxId(ActorType.Function, PortfolioBusinessIdAllocationFunctionActor.ActorName)),
      IFunctionActorContext<PortfolioBusinessIdAllocationFunctionActor>;
