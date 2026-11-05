using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Portfolio.GeneralLedger.FinancialBookPreparation.Function.Actor;

public sealed class FinancialBookPreparationFunctionContext(IActorSupervisor supervisor)
    : FunctionActorContext(supervisor, new ActorMailboxId(ActorType.Function, FinancialBookPreparationFunctionActor.ActorName)),
      IFunctionActorContext<FinancialBookPreparationFunctionActor>;
