using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Portfolio.ReferencePreparation.Function.Actor;

public sealed class PortfolioReferenceValidationFunctionContext(IActorSupervisor supervisor)
    : FunctionActorContext(supervisor, new ActorMailboxId(ActorType.Function, PortfolioReferenceValidationFunctionActor.ActorName)),
      IFunctionActorContext<PortfolioReferenceValidationFunctionActor>;
