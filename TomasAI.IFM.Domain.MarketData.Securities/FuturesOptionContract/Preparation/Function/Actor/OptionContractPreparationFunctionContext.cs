using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Preparation.Function.Actor;

public sealed class OptionContractPreparationFunctionContext(IActorSupervisor supervisor)
    : FunctionActorContext(supervisor, new ActorMailboxId(ActorType.Function, OptionContractPreparationFunctionActor.ActorName)),
      IFunctionActorContext<OptionContractPreparationFunctionActor>;
