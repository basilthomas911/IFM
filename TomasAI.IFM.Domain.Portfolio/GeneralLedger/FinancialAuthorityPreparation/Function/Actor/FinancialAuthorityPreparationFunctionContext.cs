using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Portfolio.GeneralLedger.FinancialAuthorityPreparation.Function.Actor;

public sealed class FinancialAuthorityPreparationFunctionContext(IActorSupervisor supervisor)
    : FunctionActorContext(supervisor, new ActorMailboxId(ActorType.Function, FinancialAuthorityPreparationFunctionActor.ActorName)),
      IFunctionActorContext<FinancialAuthorityPreparationFunctionActor>;
